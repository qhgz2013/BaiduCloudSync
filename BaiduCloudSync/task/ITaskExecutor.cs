using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.task
{
    /// <summary>
    /// 提供异步执行的逻辑调用接口
    /// </summary>
    public interface ITaskExecutor
    {
        /// <summary>
        /// 异步线程的回调函数，Started状态时执行的函数体
        /// </summary>
        void Run();
        /// <summary>
        /// 用户请求从就绪/暂停状态变为开始状态时的回调函数
        /// </summary>
        void OnStartRequested();
        /// <summary>
        /// 用户请求从就绪/开始状态变为暂停状态的回调函数
        /// 注：无论执行线程是否被创建，只要调用Pause()就会触发该回调函数
        /// </summary>
        void OnPauseRequested();
        /// <summary>
        /// 用户请求从就绪/开始/暂停状态变为取消状态时的回调函数
        /// 注：无论执行线程是否被创建，只要调用Pause()就会触发该回调函数
        /// </summary>
        void OnCancelRequested();
        /// <summary>
        /// 用户请求从失败状态变为就绪状态的回调函数
        /// </summary>
        void OnRetryRequested();
        /// <summary>
        /// 发送目前请求的响应信息，在Started状态下发送则视为任务完成
        /// </summary>
        event EventHandler EmitResponse;
        /// <summary>
        /// 发送执行失败信息，发送该消息后视为线程已退出，Wait操作不再等待该线程结束
        /// </summary>
        event EventHandler EmitFailure;

    }
}
