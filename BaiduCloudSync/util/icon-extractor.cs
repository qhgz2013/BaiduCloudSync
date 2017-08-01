// icon-extractor.cs
//
// 用于提取指定文件后缀名/文件夹的图标
//
using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace BaiduCloudSync
{
    public class IconExtractor
    {
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }
        [DllImport("user32.dll")]
        private static extern int DestroyIcon(IntPtr hIcon);
        [DllImport("shell32.dll", CharSet = CharSet.Auto, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);
        [DllImport("shell32.dll", BestFitMapping = false, ThrowOnUnmappableChar = true)]
        private static extern int SHGetFileInfo(string pszPath, int dwszAttributes, ref SHFILEINFO psfi, int cbFileInfo, SHGFI_Flag uFlags);
        [DllImport("shell32.dll", BestFitMapping = false, ThrowOnUnmappableChar = true)]
        private static extern int SHGetFileInfo(IntPtr pszPath, uint dwszAttributes, ref SHFILEINFO psfi, int cbFileInfo, SHGFI_Flag uFlags);

        private enum SHGFI_Flag
        {
            SHGFI_ATTR_SPECIFIED = 0x000020000,
            SHGFI_OPENICON = 0x000000002,
            SHGFI_USEFILEATTRIBUTES = 0x000000010,
            SHGFI_ADDOVERLAYS = 0x000000020,
            SHGFI_DISPLAYNAME = 0x000000200,
            SHGFI_EXETYPE = 0x000002000,
            SHGFI_ICON = 0x000000100,
            SHGFI_ICONLOCATION = 0x000001000,
            SHGFI_LARGEICON = 0x000000000,
            SHGFI_SMALLICON = 0x000000001,
            SHGFI_SHELLICONSIZE = 0x000000004,
            SHGFI_LINKOVERLAY = 0x000008000,
            SHGFI_SYSICONINDEX = 0x000004000,
            SHGFI_TYPENAME = 0x000000400
        }
        public static Icon GetIconExt()
        {
            var path = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, ".cache", ".icon-cache");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            var ext_path = Path.Combine(path, "file");
            if (!File.Exists(ext_path))
            {
                File.Create(ext_path);
            }
            return get_icon_internal(ext_path, true);
        }
        public static Icon GetIconExt(string ext)
        {
            var path = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, ".cache", ".icon-cache");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            if (string.IsNullOrEmpty(ext)) return null;
            if (ext.StartsWith(".")) ext = ext.Substring(1);
            var ext_path = Path.Combine(path, "file." + ext);
            if (!File.Exists(ext_path))
            {
                File.Create(ext_path);
            }
            return get_icon_internal(ext_path, true);
        }
        public static Icon GetIconDir()
        {
            var ext_path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".cache", ".icon-cache", "dir");
            if (!Directory.Exists(ext_path)) Directory.CreateDirectory(ext_path);
            //return Icon.ExtractAssociatedIcon(ext_path);
            return get_icon_internal(ext_path, true);
        }
        private static Icon get_icon_internal(string path, bool large_icon)
        {
            Icon ret;
            SHGFI_Flag flags;
            SHFILEINFO shinfo = new SHFILEINFO();
            if (large_icon)
                flags = SHGFI_Flag.SHGFI_ICON | SHGFI_Flag.SHGFI_LARGEICON;
            else
                flags = SHGFI_Flag.SHGFI_ICON | SHGFI_Flag.SHGFI_SMALLICON;
            //flags |= SHGFI_Flag.SHGFI_USEFILEATTRIBUTES;
            //flags |= SHGFI_Flag.SHGFI_LINKOVERLAY;
            if (SHGetFileInfo(path, 0, ref shinfo, Marshal.SizeOf(shinfo), flags) == 0)
                return null;
            Icon ico = Icon.FromHandle(shinfo.hIcon);
            ret = (Icon)ico.Clone();
            ico.Dispose();
            DestroyIcon(shinfo.hIcon);
            return ret;
        }
    }
}
