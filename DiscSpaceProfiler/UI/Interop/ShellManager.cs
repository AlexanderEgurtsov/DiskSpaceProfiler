using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;

namespace DiscSpaceProfiler.UI.Interop
{
    public enum FileAttribute : uint
    {
        Directory = 16,
        File = 256
    }
    public enum ItemType
    {
        Drive,
        Folder,
        File
    }
    [Flags]
    public enum ShellAttribute : uint
    {
        LargeIcon = 0,
        SmallIcon = 1,
        OpenIcon = 2,
        ShellIconSize = 4,
        Pidl = 8,
        UseFileAttributes = 16,
        AddOverlays = 32,
        OverlayIndex = 64,
        Others = 128,
        Icon = 256,
        DisplayName = 512,
        TypeName = 1024,
        Attributes = 2048,
        IconLocation = 4096,
        ExeType = 8192,
        SystemIconIndex = 16384,
        LinkOverlay = 32768,
        Selected = 65536,
        AttributeSpecified = 131072
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct ShellFileInfo
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    public static class Interop
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyIcon(IntPtr pointer);
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SHGetFileInfo(string path,
            uint attributes,
            out ShellFileInfo fileInfo,
            uint size,
            uint flags);
    }
    public class ShellManager
    {
        public static Icon GetIcon(string path, ItemType type)
        {
            var attributes = (uint)(type == ItemType.Folder ? FileAttribute.Directory : FileAttribute.File);
            var flags = (uint)(ShellAttribute.Icon | ShellAttribute.UseFileAttributes);

            flags = flags | (uint)ShellAttribute.SmallIcon;

            var fileInfo = new ShellFileInfo();
            var size = (uint)Marshal.SizeOf(fileInfo);
            var result = Interop.SHGetFileInfo(path, attributes, out fileInfo, size, flags);

            if (result == IntPtr.Zero)
                return null;
            try
            {
                return (Icon)Icon.FromHandle(fileInfo.hIcon).Clone();
            }
            catch
            {
                return null;
            }
            finally
            {
                Interop.DestroyIcon(fileInfo.hIcon);
            }
        }
    }
}
