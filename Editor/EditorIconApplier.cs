using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Essentials.EditorIcon.Editor
{
#if UNITY_6000_5_OR_NEWER
    // Icon timers and cached state belong to the Editor session and must survive Play mode.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class EditorIconApplier
    {
        const string CacheDirectory = "Library/Essentials/EditorIcon";
        const double StartupForceReapplyDurationSeconds = 120d;
        const double StartupForceReapplyIntervalSeconds = 2d;
        const double FocusForceReapplyDurationSeconds = 6d;
        const double FocusForceReapplyIntervalSeconds = 0.25d;
        const double MaintenanceForceReapplyIntervalSeconds = 30d;

        static double s_lastApplyTime = -MaintenanceForceReapplyIntervalSeconds;
        static double s_forceReapplyUntilTime;
        static double s_fastReapplyUntilTime;
        static bool s_applyScheduled;
        static bool s_forceNextApply;
        static bool s_autoApplyWasEnabled;
        static bool s_hasLastAppliedIcon;
        static EditorIconMode s_lastAppliedMode;
#if UNITY_6000_2_OR_NEWER
        static UnityEngine.EntityId s_lastAppliedIconId;
#else
        static int s_lastAppliedIconId;
#endif
#if UNITY_EDITOR_OSX && !UNITY_2022_3_OR_NEWER
        static bool s_editorHadFocus = UnityEditorInternal.InternalEditorUtility.isApplicationActive;
#endif

#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeInitializing]
#else
        [InitializeOnLoadMethod]
#endif
        internal static void Initialize()
        {
            Cleanup();
#if !UNITY_6000_5_OR_NEWER
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
#endif
#if UNITY_EDITOR_OSX
            s_autoApplyWasEnabled = AutoApplyEnabled;
            if (s_autoApplyWasEnabled)
            {
                StartForceReapplyWindow();
                ScheduleApply(force: true);
            }

            EditorApplication.delayCall += ScheduleForcedApply;
            EditorApplication.delayCall += ResetIconIfAutoApplyDisabled;
            EditorApplication.projectChanged += ScheduleForcedApply;
#if UNITY_2022_3_OR_NEWER
            EditorApplication.focusChanged += OnEditorFocusChanged;
#endif
            EditorApplication.update += ReapplyAfterDelay;
#endif
        }

#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        internal static void Cleanup()
        {
#if !UNITY_6000_5_OR_NEWER
            AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;
#endif
#if UNITY_EDITOR_OSX
            EditorApplication.delayCall -= ScheduleForcedApply;
            EditorApplication.delayCall -= ResetIconIfAutoApplyDisabled;
            EditorApplication.projectChanged -= ScheduleForcedApply;
#if UNITY_2022_3_OR_NEWER
            EditorApplication.focusChanged -= OnEditorFocusChanged;
#endif
            EditorApplication.update -= ReapplyAfterDelay;
#endif
            ClearScheduledApplyState();
        }

        static void ScheduleForcedApply()
        {
            if (!AutoApplyEnabled)
                return;

            StartForceReapplyWindow();
            ScheduleApply(force: true);
        }

        static void ResetIconIfAutoApplyDisabled()
        {
            if (AutoApplyEnabled)
                return;

            EditorIconNative.TryResetApplicationIcon(out _);
            ClearScheduledApplyState();
        }

        static void OnEditorFocusChanged(bool hasFocus)
        {
            if (!AutoApplyEnabled)
                return;

            StartFastReapplyWindow();
            ScheduleApply(force: true);
            TryApplyPlayerIconSilently(force: true);
        }

        static void ScheduleApply(bool force)
        {
            if (!AutoApplyEnabled)
                return;

            s_applyScheduled = true;
            s_forceNextApply |= force;
        }

        static void StartForceReapplyWindow()
        {
            s_forceReapplyUntilTime = Math.Max(
                s_forceReapplyUntilTime,
                EditorApplication.timeSinceStartup + StartupForceReapplyDurationSeconds);
        }

        static void StartFastReapplyWindow()
        {
            s_fastReapplyUntilTime = Math.Max(
                s_fastReapplyUntilTime,
                EditorApplication.timeSinceStartup + FocusForceReapplyDurationSeconds);
        }

        static void ReapplyAfterDelay()
        {
#if UNITY_EDITOR_OSX && !UNITY_2022_3_OR_NEWER
            // Older Editors keep focusChanged internal, so detect transitions during the existing update.
            bool editorHasFocus = UnityEditorInternal.InternalEditorUtility.isApplicationActive;
            if (editorHasFocus != s_editorHadFocus)
            {
                s_editorHadFocus = editorHasFocus;
                OnEditorFocusChanged(editorHasFocus);
            }
#endif

            bool autoApplyEnabled = AutoApplyEnabled;
            if (!autoApplyEnabled)
            {
                if (s_autoApplyWasEnabled)
                {
                    EditorIconNative.TryResetApplicationIcon(out _);
                    ClearScheduledApplyState();
                }

                s_autoApplyWasEnabled = false;
                return;
            }

            if (!s_autoApplyWasEnabled)
            {
                s_autoApplyWasEnabled = true;
                StartForceReapplyWindow();
                ScheduleApply(force: true);
            }

            double now = EditorApplication.timeSinceStartup;
            bool inFastForceWindow = now <= s_fastReapplyUntilTime;
            bool inStartupForceWindow = now <= s_forceReapplyUntilTime;
            double interval = inFastForceWindow
                ? FocusForceReapplyIntervalSeconds
                : inStartupForceWindow
                ? StartupForceReapplyIntervalSeconds
                : MaintenanceForceReapplyIntervalSeconds;

            if (!s_applyScheduled && now - s_lastApplyTime < interval)
                return;

            if (now - s_lastApplyTime < interval)
                return;

            bool force = s_forceNextApply || inFastForceWindow || inStartupForceWindow || !s_applyScheduled;
            s_applyScheduled = false;
            s_forceNextApply = false;
            TryApplyPlayerIconSilently(force);
        }

        internal static bool AutoApplyEnabled
        {
            get => EditorIconPreferences.AutoApplyPlayerIcon;
            set => EditorIconPreferences.AutoApplyPlayerIcon = value;
        }

        internal static EditorIconMode ApplicationIconMode
        {
            get => EditorIconPreferences.ApplicationIconMode;
            set
            {
                EditorIconMode previousMode = EditorIconPreferences.ApplicationIconMode;
                EditorIconPreferences.ApplicationIconMode = value;
                EditorIconMode currentMode = EditorIconPreferences.ApplicationIconMode;
                if (currentMode == previousMode)
                    return;

                ClearScheduledApplyState();
#if UNITY_EDITOR_OSX
                if (AutoApplyEnabled)
                {
                    StartFastReapplyWindow();
                    ScheduleApply(force: true);
                    TryApplyPlayerIconSilently(force: true);
                }
#endif
            }
        }

        static void ClearScheduledApplyState()
        {
            s_applyScheduled = false;
            s_forceNextApply = false;
            s_hasLastAppliedIcon = false;
            s_lastAppliedIconId = default;
            s_lastAppliedMode = default;
        }

        static void TryApplyPlayerIconSilently(bool force)
        {
            TryApplyPlayerIcon(force, out _);
        }

        static bool TryApplyPlayerIcon(bool force, out string error)
        {
            s_lastApplyTime = EditorApplication.timeSinceStartup;

            Texture2D icon = GetCurrentPlayerIcon();
            if (icon == null)
            {
                error = "No Player icon is assigned for this project.";
                return false;
            }

            var iconId = GetIconId(icon);
            EditorIconMode applicationIconMode = ApplicationIconMode;
            if (!force &&
                s_hasLastAppliedIcon &&
                iconId == s_lastAppliedIconId &&
                applicationIconMode == s_lastAppliedMode)
            {
                error = null;
                return true;
            }

            if (!TryGetIconImagePath(icon, out string iconPath, out error))
                return false;

            if (!EditorIconNative.TrySetApplicationIcon(iconPath, applicationIconMode, out error))
                return false;

            s_lastAppliedIconId = iconId;
            s_lastAppliedMode = applicationIconMode;
            s_hasLastAppliedIcon = true;
            return true;
        }

#if UNITY_6000_2_OR_NEWER
        static UnityEngine.EntityId GetIconId(Texture2D icon)
        {
            return icon.GetEntityId();
        }
#else
        static int GetIconId(Texture2D icon)
        {
            return icon.GetInstanceID();
        }
#endif

        static Texture2D GetCurrentPlayerIcon()
        {
            BuildTargetGroup buildTargetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            Texture2D icon = GetFirstIconForBuildTargetGroup(buildTargetGroup);
            if (icon != null)
                return icon;

            icon = GetFirstIconForBuildTargetGroup(BuildTargetGroup.Unknown);
            if (icon != null)
                return icon;

            foreach (BuildTargetGroup targetGroup in (BuildTargetGroup[])Enum.GetValues(typeof(BuildTargetGroup)))
            {
                if (targetGroup == buildTargetGroup || targetGroup == BuildTargetGroup.Unknown)
                    continue;

                icon = GetFirstIconForBuildTargetGroup(targetGroup);
                if (icon != null)
                    return icon;
            }

            return null;
        }

        static Texture2D GetFirstIconForBuildTargetGroup(BuildTargetGroup buildTargetGroup)
        {
#pragma warning disable 618
            try
            {
                return FirstAssignedIcon(PlayerSettings.GetIconsForTargetGroup(buildTargetGroup, IconKind.Application)) ??
                       FirstAssignedIcon(PlayerSettings.GetIconsForTargetGroup(buildTargetGroup, IconKind.Any)) ??
                       FirstAssignedIcon(PlayerSettings.GetIconsForTargetGroup(buildTargetGroup));
            }
            catch
            {
                return null;
            }
#pragma warning restore 618
        }

        static Texture2D FirstAssignedIcon(Texture2D[] icons)
        {
            if (icons == null)
                return null;

            foreach (Texture2D icon in icons)
            {
                if (icon != null)
                    return icon;
            }

            return null;
        }

        static bool TryGetIconImagePath(Texture2D icon, out string iconPath, out string error)
        {
            string assetPath = AssetDatabase.GetAssetPath(icon);
            if (!string.IsNullOrEmpty(assetPath))
            {
                string fullPath = GetProjectRelativeFullPath(assetPath);
                if (File.Exists(fullPath))
                {
                    iconPath = fullPath;
                    error = null;
                    return true;
                }
            }

            return TryWriteReadableIconCopy(icon, out iconPath, out error);
        }

        static bool TryWriteReadableIconCopy(Texture2D icon, out string iconPath, out string error)
        {
            try
            {
                string directory = GetProjectRelativeFullPath(CacheDirectory);
                Directory.CreateDirectory(directory);

                iconPath = Path.Combine(directory, "EditorIcon.png");
                byte[] pngBytes = TryEncodeTexture(icon);
                File.WriteAllBytes(iconPath, pngBytes);
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                iconPath = null;
                error = $"Could not prepare the Player icon image for macOS: {exception.Message}";
                return false;
            }
        }

        static byte[] TryEncodeTexture(Texture2D icon)
        {
            try
            {
                byte[] pngBytes = icon.EncodeToPNG();
                if (pngBytes != null && pngBytes.Length > 0)
                    return pngBytes;
            }
            catch
            {
            }

            return EncodeTextureViaRenderTexture(icon);
        }

        static string GetProjectRelativeFullPath(string relativePath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot))
                return Path.GetFullPath(relativePath);

            return Path.GetFullPath(Path.Combine(projectRoot, relativePath));
        }

        static byte[] EncodeTextureViaRenderTexture(Texture2D icon)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture renderTexture = RenderTexture.GetTemporary(icon.width, icon.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D readableTexture = null;

            try
            {
                Graphics.Blit(icon, renderTexture);
                RenderTexture.active = renderTexture;

                readableTexture = new Texture2D(icon.width, icon.height, TextureFormat.RGBA32, false);
                readableTexture.ReadPixels(new Rect(0, 0, icon.width, icon.height), 0, 0);
                readableTexture.Apply();
                return readableTexture.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTexture);

                if (readableTexture != null)
                    UnityEngine.Object.DestroyImmediate(readableTexture);
            }
        }
    }
}
