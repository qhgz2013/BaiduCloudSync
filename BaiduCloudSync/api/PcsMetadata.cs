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
        private PcsPath _path;
        private string _md5;
        private bool _isdir;
        private DateTime _server_ctime, _server_mtime;
        private DateTime _local_ctime, _local_mtime;
        private long _size;

        #region properties
        /// <summary>
        /// 文件路径信息
        /// </summary>
        public PcsPath PathInfo { get { return _path; } set { _path = value; } }
        /// <summary>
        /// 文件MD5
        /// </summary>
        public string MD5 { get { return _md5; } set { _md5 = value; } }
        /// <summary>
        /// 是否为文件夹
        /// </summary>
        public bool IsDirectory { get { return _isdir; } set { _isdir = value; } }
        /// <summary>
        /// 服务器的文件/文件夹创建时间
        /// </summary>
        public DateTime ServerCreationTime { get { return _server_ctime; } set { _server_ctime = value; } }
        /// <summary>
        /// 服务器的文件/文件夹修改时间
        /// </summary>
        public DateTime ServerModificationTime { get { return _server_mtime; } set { _server_mtime = value; } }
        /// <summary>
        /// 本地的文件/文件夹创建时间
        /// </summary>
        public DateTime LocalCreationTime { get { return _local_ctime; } set { _local_ctime = value; } }
        /// <summary>
        /// 本地的文件/文件夹修改时间
        /// </summary>
        public DateTime LocalModificationTime { get { return _local_mtime; } set { _local_mtime = value; } }
        /// <summary>
        /// 文件大小
        /// </summary>
        public long Size { get { return _size; } set { _size = value; } }
        #endregion
        
        public PcsMetadata(string path = null, string md5 = null, bool is_dir = false, long size = 0, DateTime? server_ctime = null, DateTime? server_mtime = null, DateTime? local_ctime = null, DateTime? local_mtime = null)
        {
            _path = new PcsPath(path);
            _md5 = md5;
            _isdir = is_dir;
            _size = size;
            _server_ctime = server_ctime.HasValue ? server_ctime.Value : DateTime.MinValue;
            _server_mtime = server_mtime.HasValue ? server_mtime.Value : DateTime.MinValue;
            _local_ctime = local_ctime.HasValue ? local_ctime.Value : DateTime.MinValue;
            _local_mtime = local_mtime.HasValue ? local_mtime.Value : DateTime.MinValue;
        }
        public PcsMetadata(PcsPath path = null, string md5 = null, bool is_dir = false, long size = 0, DateTime? server_ctime = null, DateTime? server_mtime = null, DateTime? local_ctime = null, DateTime? local_mtime = null)
        {
            _path = path;
            _md5 = md5;
            _isdir = is_dir;
            _size = size;
            _server_ctime = server_ctime.HasValue ? server_ctime.Value : DateTime.MinValue;
            _server_mtime = server_mtime.HasValue ? server_mtime.Value : DateTime.MinValue;
            _local_ctime = local_ctime.HasValue ? local_ctime.Value : DateTime.MinValue;
            _local_mtime = local_mtime.HasValue ? local_mtime.Value : DateTime.MinValue;
        }
    }
}
