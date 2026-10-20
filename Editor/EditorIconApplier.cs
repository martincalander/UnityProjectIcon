using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace ProjectIcon.Editor
{
#if UNITY_6000_5_OR_NEWER
    // Icon timers and cached state belong to the Editor session and must survive Play mode.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class EditorIconApplier
    {
        const string CacheDirectory = "Library/com.martincalander.projecticon";
        const double StartupForceReapplyDurationSeconds = 120d;
        const double StartupForceReapplyIntervalSeconds = 2d;
        const double FocusForceReapplyDurationSeconds = 6d;
        const double FocusForceReapplyIntervalSeconds = 0.25d;
        const double MaintenanceForceReapplyIntervalSeconds = 30d;

        static readonly BuildTargetGroup[] s_buildTargetGroups =
            (BuildTargetGroup[])Enum.GetValues(typeof(BuildTargetGroup));

        static double s_lastApplyTime = -MaintenanceForceReapplyIntervalSeconds;
        static double s_forceReapplyUntilTime;
        static double s_fastReapplyUntilTime;
        static bool s_autoApplyWasEnabled;
        static Texture2D s_readableIconSource;
        static uint s_readableIconUpdateCount;
        static Hash128 s_readableIconContentsHash;
        static string s_readableIconPath;
        static DateTime s_readableIconWriteTime;
        static long s_readableIconLength;
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
                StartForceReapplyWindow();

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
        }

        static void ScheduleForcedApply()
        {
            s_readableIconSource = null;
            if (!AutoApplyEnabled)
                return;

            StartForceReapplyWindow();
        }

        static void ResetIconIfAutoApplyDisabled()
        {
            if (AutoApplyEnabled)
                return;

            EditorIconNative.TryResetApplicationIcon(out _);
        }

        static void OnEditorFocusChanged(bool hasFocus)
        {
            if (!AutoApplyEnabled)
                return;

            StartFastReapplyWindow();
            TryApplyPlayerIconSilently();
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
                    EditorIconNative.TryResetApplicationIcon(out _);

                s_autoApplyWasEnabled = false;
                return;
            }

            if (!s_autoApplyWasEnabled)
            {
                s_autoApplyWasEnabled = true;
                StartForceReapplyWindow();
            }

            double now = EditorApplication.timeSinceStartup;
            bool inFastForceWindow = now <= s_fastReapplyUntilTime;
            bool inStartupForceWindow = now <= s_forceReapplyUntilTime;
            double interval = inFastForceWindow
                ? FocusForceReapplyIntervalSeconds
                : inStartupForceWindow
                ? StartupForceReapplyIntervalSeconds
                : MaintenanceForceReapplyIntervalSeconds;

            if (now - s_lastApplyTime < interval)
                return;

            TryApplyPlayerIconSilently();
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

#if UNITY_EDITOR_OSX
                if (AutoApplyEnabled)
                {
                    StartFastReapplyWindow();
                    TryApplyPlayerIconSilently();
                }
#endif
            }
        }

        static void TryApplyPlayerIconSilently()
        {
            TryApplyPlayerIcon(out _);
        }

        static bool TryApplyPlayerIcon(out string error)
        {
            s_lastApplyTime = EditorApplication.timeSinceStartup;

            Texture2D icon = GetCurrentPlayerIcon();
            if (icon == null)
            {
                error = "No Player icon is assigned for this project.";
                return false;
            }

            return TryApplyIcon(icon, ApplicationIconMode, out error);
        }

        internal static bool TryApplyIcon(Texture2D icon, EditorIconMode mode, out string error)
        {
            if (icon == null)
            {
                error = "No Player icon is assigned for this project.";
                return false;
            }

            if (!TryGetIconImagePath(icon, out string iconPath, out bool isSourceFile, out error))
                return false;

            if (EditorIconNative.TrySetApplicationIcon(iconPath, mode, out error, out bool imageRejected))
                return true;

            // Unity can import textures whose source files AppKit cannot decode, such as .asset files.
            // Missing native libraries cannot recover by converting the texture.
            if (!isSourceFile || !imageRejected || !TryWriteReadableIconCopy(icon, out iconPath, out error))
                return false;

            return EditorIconNative.TrySetApplicationIcon(iconPath, mode, out error);
        }

        static Texture2D GetCurrentPlayerIcon()
        {
            BuildTargetGroup buildTargetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            Texture2D icon = GetFirstIconForBuildTargetGroup(buildTargetGroup);
            if (icon != null)
                return icon;

            icon = GetFirstIconForBuildTargetGroup(BuildTargetGroup.Unknown);
            if (icon != null)
                return icon;

            foreach (BuildTargetGroup targetGroup in s_buildTargetGroups)
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
                       FirstAssignedIcon(PlayerSettings.GetIconsForTargetGroup(buildTargetGroup, IconKind.Any));
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

        static bool TryGetIconImagePath(
            Texture2D icon, out string iconPath, out bool isSourceFile, out string error)
        {
            isSourceFile = false;
            if (TryGetReadableIconCopy(icon, out iconPath))
            {
                error = null;
                return true;
            }

            string assetPath = AssetDatabase.GetAssetPath(icon);
            if (!string.IsNullOrEmpty(assetPath))
            {
                string fullPath = GetProjectRelativeFullPath(assetPath);
                if (!File.Exists(fullPath) && assetPath.StartsWith("Packages/", StringComparison.Ordinal))
                {
                    var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);
                    if (package != null)
                        fullPath = Path.Combine(package.resolvedPath, assetPath.Substring(package.assetPath.Length + 1));
                }
                if (File.Exists(fullPath))
                {
                    iconPath = fullPath;
                    isSourceFile = true;
                    error = null;
                    return true;
                }
            }

            return TryWriteReadableIconCopy(icon, out iconPath, out error);
        }

        static bool TryGetReadableIconCopy(Texture2D icon, out string iconPath)
        {
            iconPath = s_readableIconPath;
            if (icon == null || icon != s_readableIconSource ||
                icon.updateCount != s_readableIconUpdateCount ||
                icon.imageContentsHash != s_readableIconContentsHash)
                return false;

            try
            {
                var file = new FileInfo(iconPath);
                return file.Exists &&
                    file.LastWriteTimeUtc == s_readableIconWriteTime &&
                    file.Length == s_readableIconLength;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                // The next conversion reports any persistent access failure to the caller.
                return false;
            }
        }

        internal static bool TryWriteReadableIconCopy(Texture2D icon, out string iconPath, out string error)
        {
            try
            {
                if (TryGetReadableIconCopy(icon, out iconPath))
                {
                    error = null;
                    return true;
                }

                string directory = GetProjectRelativeFullPath(CacheDirectory);
                Directory.CreateDirectory(directory);

                iconPath = Path.Combine(directory, "ProjectIcon.png");
                byte[] pngBytes = TryEncodeTexture(icon);
                if (pngBytes == null || pngBytes.Length == 0)
                    throw new InvalidOperationException("The texture could not be encoded as PNG.");

                // Invalidate before replacing the shared file so a failed write cannot reuse stale contents.
                s_readableIconSource = null;
                File.WriteAllBytes(iconPath, pngBytes);
                var file = new FileInfo(iconPath);
                s_readableIconWriteTime = file.LastWriteTimeUtc;
                s_readableIconLength = file.Length;
                s_readableIconUpdateCount = icon.updateCount;
                s_readableIconContentsHash = icon.imageContentsHash;
                s_readableIconPath = iconPath;
                s_readableIconSource = icon;
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
            // Unsupported encodes can log errors without throwing. Convert those textures directly.
            TextureFormat format = icon.format;
            bool isHdr = format == TextureFormat.RGBAHalf || format == TextureFormat.RGBAFloat ||
                format == TextureFormat.RGHalf || format == TextureFormat.RGFloat ||
                format == TextureFormat.RGB9e5Float;
            // Single-channel RHalf and RFloat are supported grayscale PNG inputs.
            if (icon.isReadable && !isHdr && !GraphicsFormatUtility.IsCompressedFormat(icon.graphicsFormat))
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
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                throw new InvalidOperationException("A graphics device is required to read this texture.");

            RenderTexture previous = RenderTexture.active;
            RenderTexture renderTexture = RenderTexture.GetTemporary(icon.width, icon.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D readableTexture = null;

            try
            {
                Graphics.Blit(icon, renderTexture);
                RenderTexture.active = renderTexture;

                readableTexture = new Texture2D(icon.width, icon.height, TextureFormat.RGBA32, false);
                readableTexture.ReadPixels(new Rect(0, 0, icon.width, icon.height), 0, 0, false);
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
