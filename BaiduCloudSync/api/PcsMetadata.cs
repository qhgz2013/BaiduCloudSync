using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.api
{
    /// <summary>
    /// PCS文件系统的文件元信息
    /// </summary>
    public class PcsMetadata
    {

        #region properties
        public long FSID { get; set; }
        /// <summary>
        /// 文件路径信息
        /// </summary>
        public PcsPath PathInfo { get; set; }
        /// <summary>
        /// 文件MD5
        /// </summary>
        public string MD5 { get; set; }
        /// <summary>
        /// 是否为文件夹
        /// </summary>
        public bool IsDirectory { get; set; }
        /// <summary>
        /// 服务器的文件/文件夹创建时间
        /// </summary>
        public DateTime ServerCreationTime { get; set; }
        /// <summary>
        /// 服务器的文件/文件夹修改时间
        /// </summary>
        public DateTime ServerModificationTime { get; set; }
        /// <summary>
        /// 本地的文件/文件夹创建时间
        /// </summary>
        public DateTime LocalCreationTime { get; set; }
        /// <summary>
        /// 本地的文件/文件夹修改时间
        /// </summary>
        public DateTime LocalModificationTime { get; set; }
        /// <summary>
        /// 文件大小
        /// </summary>
        public long Size { get; set; }
        #endregion

        public PcsMetadata(string path = null, long fs_id = 0, string md5 = null, bool is_dir = false, long size = 0, DateTime? server_ctime = null, DateTime? server_mtime = null, DateTime? local_ctime = null, DateTime? local_mtime = null)
        {
            PathInfo = path == null ? null : new PcsPath(path);
            FSID = fs_id;
            MD5 = md5;
            IsDirectory = is_dir;
            Size = size;
            ServerCreationTime = server_ctime.HasValue ? server_ctime.Value : DateTime.MinValue;
            ServerModificationTime = server_mtime.HasValue ? server_mtime.Value : DateTime.MinValue;
            LocalCreationTime = local_ctime.HasValue ? local_ctime.Value : DateTime.MinValue;
            LocalModificationTime = local_mtime.HasValue ? local_mtime.Value : DateTime.MinValue;
        }
        public PcsMetadata(PcsPath path, long fs_id = 0, string md5 = null, bool is_dir = false, long size = 0, DateTime? server_ctime = null, DateTime? server_mtime = null, DateTime? local_ctime = null, DateTime? local_mtime = null)
        {
            PathInfo = path;
            FSID = fs_id;
            MD5 = md5;
            IsDirectory = is_dir;
            Size = size;
            ServerCreationTime = server_ctime.HasValue ? server_ctime.Value : DateTime.MinValue;
            ServerModificationTime = server_mtime.HasValue ? server_mtime.Value : DateTime.MinValue;
            LocalCreationTime = local_ctime.HasValue ? local_ctime.Value : DateTime.MinValue;
            LocalModificationTime = local_mtime.HasValue ? local_mtime.Value : DateTime.MinValue;
        }

        public override string ToString()
        {
            if (PathInfo == null) return base.ToString();
            return PathInfo.FullPath;
        }
    }
}
