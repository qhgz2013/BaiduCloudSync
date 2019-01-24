using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.api
{
    /// <summary>
    /// PCS的文件排序依据
    /// </summary>
    public enum PcsFileOrder
    {
        /// <summary>
        /// 按名称排序
        /// </summary>
        Name,
        /// <summary>
        /// 按时间排序
        /// </summary>
        Time,
        /// <summary>
        /// 按大小排序
        /// </summary>
        Size
    }
}
