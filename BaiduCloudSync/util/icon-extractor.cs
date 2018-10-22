// icon-extractor.cs
//
// 用于提取指定文件后缀名/文件夹的图标
//
using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using static GlobalUtil.Win32API;

namespace BaiduCloudSync
{
    public class IconExtractor
    {
        /// <summary>
        /// 提取无后缀的文件的图标
        /// </summary>
        /// <returns></returns>
        public static Icon GetIconExt()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".cache", ".icon-cache");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            var ext_path = Path.Combine(path, "file");
            if (!File.Exists(ext_path))
            {
                File.Create(ext_path);
            }
            return get_icon_internal(ext_path, true);
        }
        /// <summary>
        /// 提取指定后缀的文件的图标
        /// </summary>
        /// <param name="ext">文件后缀，后缀的“.”可无</param>
        /// <returns></returns>
        public static Icon GetIconExt(string ext)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".cache", ".icon-cache");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            if (string.IsNullOrEmpty(ext)) return GetIconExt();
            if (ext.StartsWith(".")) ext = ext.Substring(1);
            var ext_path = Path.Combine(path, "file." + ext);
            if (!File.Exists(ext_path))
            {
                File.Create(ext_path);
            }
            return get_icon_internal(ext_path, true);
        }
        /// <summary>
        /// 提取文件夹的图标
        /// </summary>
        /// <returns></returns>
        public static Icon GetIconDir()
        {
            var ext_path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".cache", ".icon-cache", "dir");
            if (!Directory.Exists(ext_path)) Directory.CreateDirectory(ext_path);
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
            if (SHGetFileInfo(path, 0, ref shinfo, Marshal.SizeOf(shinfo), flags) == IntPtr.Zero)
                return null;
            Icon ico = Icon.FromHandle(shinfo.hIcon);
            ret = (Icon)ico.Clone();
            ico.Dispose();
            DestroyIcon(shinfo.hIcon);
            return ret;
        }
    }
}
