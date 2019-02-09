using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.task
{
    /// <summary>
    /// 任务的状态
    /// </summary>
    public enum TaskState
    {
        /// <summary>
        /// 就绪
        /// </summary>
        Ready,
        /// <summary>
        /// 已经请求开始，等待调度算法分配工作线程以开始任务
        /// </summary>
        StartRequested,
        /// <summary>
        /// 任务已开始
        /// </summary>
        Started,
        /// <summary>
        /// 已经请求暂停，等待调度算法分配工作线程以暂停任务
        /// </summary>
        PauseRequested,
        /// <summary>
        /// 任务已暂停
        /// </summary>
        Paused,
        /// <summary>
        /// 已经请求取消，等待调度算法分配工作线程以取消任务
        /// </summary>
        CancelRequested,
        /// <summary>
        /// 任务已取消
        /// </summary>
        Cancelled,
        /// <summary>
        /// 任务已完成
        /// </summary>
        Finished,
        /// <summary>
        /// 任务因未知异常而失败
        /// </summary>
        Failed,
        /// <summary>
        /// 任务因取消或失败后请求重试，等待调度算法分配工作线程以重试任务
        /// </summary>
        RetryRequested
    }
}
