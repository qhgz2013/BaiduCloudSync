﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.api.callbackargs
{
    /// <summary>
    /// IPcsAsyncAPI回调函数中，结果的类型
    /// </summary>
    public enum PcsApiCallbackType
    {
        /// <summary>
        /// 未指定类型
        /// </summary>
        Unspecified,
        /// <summary>
        /// 操作结果，通常为true或false
        /// </summary>
        OperationResult,
        /// <summary>
        /// 网盘配额
        /// </summary>
        Quota,
        /// <summary>
        /// 单个文件元数据
        /// </summary>
        SingleObjectMetadata,
        /// <summary>
        /// 多个文件元数据
        /// </summary>
        MultiObjectMetadata,
        /// <summary>
        /// 文件预创建结果
        /// </summary>
        PreCreateResult,
        /// <summary>
        /// 文件分段上传结果
        /// </summary>
        SegmentUploadResult,
        /// <summary>
        /// 一个或多个URL地址
        /// </summary>
        URL
    }
}