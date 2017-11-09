using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GlobalUtil;

namespace BaiduCloudSync
{

    /// <summary>
    /// 缓存的网盘文件数据
    /// </summary>
    public struct TrackingData
    {
        /// <summary>
        /// FS ID
        /// </summary>
        public long fs_id;
        /// <summary>
        /// 文件路径
        /// </summary>
        public string path;
        /// <summary>
        /// 服务器返回的MD5
        /// </summary>
        public string md5_remote;
        /// <summary>
        /// 本地文件计算出的MD5
        /// </summary>
        public string md5_local;
        /// <summary>
        /// 服务器返回的CRC32
        /// </summary>
        public string crc32_remote;
        /// <summary>
        /// 本地文件计算出的CRC32
        /// </summary>
        public string crc32_local;
        /// <summary>
        /// 文件名
        /// </summary>
        public string filename;
        /// <summary>
        /// 文件大小
        /// </summary>
        public long size;
        /// <summary>
        /// 文件前256K分段的MD5
        /// </summary>
        public string slice_md5;
        /// <summary>
        /// 服务器文件修改时间
        /// </summary>
        public DateTime remote_mtime;
        /// <summary>
        /// 服务器文件创建时间
        /// </summary>
        public DateTime remote_ctime;
        /// <summary>
        /// 本地文件修改时间
        /// </summary>
        public DateTime local_mtime;
        /// <summary>
        /// 本地文件创建时间
        /// </summary>
        public DateTime local_ctime;
        /// <summary>
        /// 是否为目录
        /// </summary>
        public bool isdir;
        /// <summary>
        /// 文件是否可被下载（0则为被和谐）
        /// </summary>
        public bool dl_available;
        /// <summary>
        /// 文件所属的账号id（多账号辨识）
        /// </summary>
        public int account_id;
        /// <summary>
        /// 文件分类
        /// </summary>
        public int category;
        public int oper_id;
        public int unlist;
        public BaiduPCS.ObjectMetadata to_pcs_meta()
        {
            return new BaiduPCS.ObjectMetadata
            {
                FS_ID = (ulong)fs_id,
                Category = (uint)category,
                IsDelete = false,
                IsDir = isdir,
                LocalCTime = (ulong)util.ToUnixTimestamp(local_ctime),
                LocalMTime = (ulong)util.ToUnixTimestamp(local_mtime),
                MD5 = md5_remote,
                OperID = (uint)oper_id,
                Path = path,
                ServerCTime = (ulong)util.ToUnixTimestamp(remote_ctime),
                ServerFileName = filename,
                ServerMTime = (ulong)util.ToUnixTimestamp(remote_mtime),
                Size = (ulong)size,
                Unlist = (uint)unlist
            };
        }
        public TrackingData(BaiduPCS.ObjectMetadata data)
        {
            fs_id = (long)data.FS_ID;
            path = data.Path;
            slice_md5 = string.Empty;
            md5_local = string.Empty;
            md5_remote = data.MD5;
            crc32_local = string.Empty;
            crc32_remote = string.Empty;
            filename = data.ServerFileName;
            size = (long)data.Size;
            remote_ctime = util.FromUnixTimestamp(data.ServerCTime);
            remote_mtime = util.FromUnixTimestamp(data.ServerMTime);
            local_ctime = util.FromUnixTimestamp(data.LocalCTime);
            local_mtime = util.FromUnixTimestamp(data.LocalMTime);
            isdir = data.IsDir;
            dl_available = true;
            account_id = 0;
            category = (int)data.Category;
            oper_id = (int)data.OperID;
            unlist = (int)data.Unlist;
        }

    }
}
