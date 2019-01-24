using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.api.callbackargs
{
    /// <summary>
    /// PCS API回调函数的参数
    /// </summary>
    public abstract class PcsApiCallbackArgs : EventArgs
    {
        /// <summary>
        /// 结果的类型
        /// </summary>
        public PcsApiCallbackType EventType { get; private set; }
        /// <summary>
        /// 调用API是否成功
        /// </summary>
        public bool Success { get; private set; }
        /// <summary>
        /// 调用API失败后可能包含的失败信息
        /// </summary>
        public string FailureMessage { get; protected set; }
        /// <summary>
        /// 调用API时的附加state参数
        /// </summary>
        public object State { get; protected set; }
        protected PcsApiCallbackArgs(PcsApiCallbackType eventType = PcsApiCallbackType.Unspecified, bool success = false, string failure_msg = null, object state = null)
        {
            EventType = eventType;
            Success = success;
            FailureMessage = failure_msg;
            State = state;
        }
    }
}
