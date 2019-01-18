
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.api
{
    /// <summary>
    /// PCS API接口
    /// </summary>
    public interface IPcsAPI
    {
        /// <summary>
        /// 列出指定路径下的所有文件
        /// </summary>
        /// <param name="path">指定的路径</param>
        /// <returns>该路径下的文件/文件夹元数据</returns>
        PcsMetadata[] ListDir(string path);
        /// <summary>
        /// 创建文件夹
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        bool CreateDirectory(string path);

        bool Delete(string path);

        void Rename(string path, string new_name);

        void Copy(string src_path, string dst_path, PcsOverwriteType overwrite_type);
        void Move(string src_path, string dst_path, PcsOverwriteType overwrite_type);
    }

}
