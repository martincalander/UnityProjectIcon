using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Essentials.EditorIcon.Editor
{
    internal static class EditorIconNative
    {
#if UNITY_EDITOR_OSX
        const string PluginName = "EssentialsEditorIcon";

        [DllImport(PluginName, EntryPoint = "EssentialsEditorIconSetApplicationIcon")]
        static extern int SetApplicationIcon(IntPtr utf8Path);

        [DllImport(PluginName, EntryPoint = "EssentialsEditorIconResetApplicationIcon")]
        static extern void ResetApplicationIcon();
#endif

        internal static bool TrySetApplicationIcon(string imagePath, out string error)
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

                if (SetApplicationIcon(pathPointer) == 1)
                {
                    error = null;
                    return true;
                }

                error = "The native macOS plugin could not load the icon image.";
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
            error = "Editor icon changes are only implemented for the macOS Unity Editor.";
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
            error = "Editor icon changes are only implemented for the macOS Unity Editor.";
            return false;
#endif
        }
    }
}
