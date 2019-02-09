using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.task
{
    /// <summary>
    /// 提供异步任务的一些基本同步操作的抽象接口
    /// </summary>
    internal interface ITaskOperator
    {
        /// <summary>
        /// 开始任务，直到任务完成或被用户取消、暂停，或是引发了内部异常为止。
        /// </summary>
        void Start();
        /// <summary>
        /// 暂停已开始的任务
        /// </summary>
        void Pause();
        /// <summary>
        /// 取消已经开始或者是暂停中的任务
        /// </summary>
        void Cancel();

        /// <summary>
        /// 重试失败任务
        /// </summary>
        void Retry();
        /// <summary>
        /// 等待任务进入稳定状态，即中止工作线程的状态，包括Paused、Cancelled、Failed、Ready和Finished状态
        /// </summary>
        /// <param name="timeout">等待的超时时间（毫秒），若需要无限时等待，则设为-1</param>
        /// <returns>等待成功则返回true，等待超时则返回false</returns>
        bool Wait(int timeout);
    }
}
