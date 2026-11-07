using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEditor;

namespace ProjectIcon.Editor
{
#if UNITY_6000_5_OR_NEWER
    // Native availability belongs to the Editor session and must survive Play mode.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static class EditorIconNative
    {
#if UNITY_EDITOR_OSX
        internal const bool IsSupported = true;
        internal const string PlatformSupportMessage =
            "Badge and Icon Replacement apply to the current Unity Editor process on macOS. They do not modify the Unity installation, project icon assets, or Player builds.";

        const string PluginName = "ProjectIcon";

        // A missing or incompatible bundle cannot recover until code reloads, so later calls skip probing it.
        static string s_unavailableError;

        [DllImport(PluginName, EntryPoint = "ProjectIconSetApplicationIcon")]
        static extern int SetApplicationIconBadge(IntPtr utf8Path);

        [DllImport(PluginName, EntryPoint = "ProjectIconSetApplicationIconWithMode")]
        static extern int SetApplicationIconWithMode(IntPtr utf8Path, int mode);

        [DllImport(PluginName, EntryPoint = "ProjectIconResetApplicationIcon")]
        static extern void ResetApplicationIcon();
#elif UNITY_EDITOR_WIN
        internal const bool IsSupported = false;
        internal const string PlatformSupportMessage =
            "Project Icon does not currently change Unity Editor icons on Windows because no tested Windows backend is included. Your preference is still saved for use on supported platforms; see the README for details.";
#elif UNITY_EDITOR_LINUX
        internal const bool IsSupported = false;
        internal const string PlatformSupportMessage =
            "Project Icon does not currently change Unity Editor icons on Linux. X11 and Wayland do not provide one reliable cross-desktop integration path; see the README for details.";
#else
        internal const bool IsSupported = false;
        internal const string PlatformSupportMessage =
            "Project Icon is not supported on this Unity Editor platform. Your preference is still saved for use on supported platforms.";
#endif

        const string AppliedSessionKey = "ProjectIcon.AppliedThisSession";

        internal static bool AppliedThisSession => SessionState.GetBool(AppliedSessionKey, false);

        internal static bool TrySetApplicationIcon(
            string imagePath,
            EditorIconMode applicationIconMode,
            out string error)
        {
            return TrySetApplicationIcon(imagePath, applicationIconMode, out error, out _);
        }

        internal static bool TrySetApplicationIcon(
            string imagePath,
            EditorIconMode applicationIconMode,
            out string error,
            out bool imageRejected)
        {
            imageRejected = false;
#if UNITY_EDITOR_OSX
            if (string.IsNullOrEmpty(imagePath))
            {
                error = "Icon path is empty.";
                return false;
            }

            if (s_unavailableError != null)
            {
                error = s_unavailableError;
                return false;
            }

            IntPtr pathPointer = IntPtr.Zero;
            try
            {
                byte[] pathBytes = Encoding.UTF8.GetBytes(imagePath + "\0");
                pathPointer = Marshal.AllocHGlobal(pathBytes.Length);
                Marshal.Copy(pathBytes, 0, pathPointer, pathBytes.Length);

                int result;
                try
                {
                    result = SetApplicationIconWithMode(pathPointer, (int)applicationIconMode);
                }
                catch (EntryPointNotFoundException) when (applicationIconMode == EditorIconMode.Badge)
                {
                    result = SetApplicationIconBadge(pathPointer);
                }

                if (result == 1)
                {
                    SessionState.SetBool(AppliedSessionKey, true);
                    error = null;
                    return true;
                }

                imageRejected = true;
                error = "The native macOS plugin could not apply the icon image or rejected the icon mode.";
                return false;
            }
            catch (Exception exception) when (exception is DllNotFoundException ||
                                              exception is EntryPointNotFoundException ||
                                              exception is BadImageFormatException)
            {
                error = s_unavailableError = exception.Message;
                return false;
            }
            finally
            {
                if (pathPointer != IntPtr.Zero)
                    Marshal.FreeHGlobal(pathPointer);
            }
#else
            error = PlatformSupportMessage;
            return false;
#endif
        }

        internal static bool TryResetApplicationIcon(out string error)
        {
#if UNITY_EDITOR_OSX
            if (s_unavailableError != null)
            {
                error = s_unavailableError;
                return false;
            }

            try
            {
                ResetApplicationIcon();
                SessionState.SetBool(AppliedSessionKey, false);
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is DllNotFoundException ||
                                              exception is EntryPointNotFoundException ||
                                              exception is BadImageFormatException)
            {
                error = s_unavailableError = exception.Message;
                return false;
            }
#else
            error = PlatformSupportMessage;
            return false;
#endif
        }
    }
}
