using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.task
{
    /// <summary>
    /// 任务状态非法时引发的异常
    /// </summary>
    [Serializable]
    public class InvalidTaskStateException : Exception
    {
        public InvalidTaskStateException() : base() { }
        public InvalidTaskStateException(string message) : base(message) { }
        public InvalidTaskStateException(string message, Exception innerException) : base(message, innerException) { }
    }
}
