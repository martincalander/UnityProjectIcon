using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Essentials.EditorIcon.Editor
{
    internal static class EditorIconNative
    {
#if UNITY_EDITOR_OSX
        internal const bool IsSupported = true;
        internal const string PlatformSupportMessage =
            "Badge and Icon Replacement apply to the current Unity Editor process on macOS. They do not modify the Unity installation, project icon assets, or Player builds.";

        const string PluginName = "EssentialsEditorIcon";

        [DllImport(PluginName, EntryPoint = "EssentialsEditorIconSetApplicationIcon")]
        static extern int SetApplicationIconLegacy(IntPtr utf8Path);

        [DllImport(PluginName, EntryPoint = "EssentialsEditorIconSetApplicationIconWithMode")]
        static extern int SetApplicationIconWithMode(IntPtr utf8Path, int mode);

        [DllImport(PluginName, EntryPoint = "EssentialsEditorIconResetApplicationIcon")]
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

        internal static bool TrySetApplicationIcon(
            string imagePath,
            EditorIconMode applicationIconMode,
            out string error)
        {
#if UNITY_EDITOR_OSX
            if (string.IsNullOrEmpty(imagePath))
            {
                error = "Icon path is empty.";
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
                    result = SetApplicationIconLegacy(pathPointer);
                }

                if (result == 1)
                {
                    error = null;
                    return true;
                }

                error = "The native macOS plugin could not load the icon image or rejected the icon mode.";
                return false;
            }
            catch (Exception exception) when (exception is DllNotFoundException ||
                                              exception is EntryPointNotFoundException ||
                                              exception is BadImageFormatException)
            {
                error = exception.Message;
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
            try
            {
                ResetApplicationIcon();
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is DllNotFoundException ||
                                              exception is EntryPointNotFoundException ||
                                              exception is BadImageFormatException)
            {
                error = exception.Message;
                return false;
            }
#else
            error = PlatformSupportMessage;
            return false;
#endif
        }
    }
}
