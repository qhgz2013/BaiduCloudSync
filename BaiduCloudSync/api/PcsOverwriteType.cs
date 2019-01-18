using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.api
{
    /// <summary>
    /// PCS文件系统的文件同名覆盖方式
    /// </summary>
    public enum PcsOverwriteType
    {
        /// <summary>
        /// 覆盖原文件
        /// </summary>
        Overwrite,
        /// <summary>
        /// 重命名为新的副本（根据目前时间重命名）
        /// </summary>
        Newcopy
    }
}
