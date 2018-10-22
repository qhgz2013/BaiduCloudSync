using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.oauth
{
    /// <summary>
    /// 未登陆的情况下尝试获取登陆参数时抛出的异常
    /// </summary>
    [Serializable]
    public class NotLoggedInException : Exception
    {
        public NotLoggedInException() : base() { }
        public NotLoggedInException(string message) : base(message) { }
        public NotLoggedInException(string message, Exception innerException) : base(message, innerException) { }
        public override string ToString()
        {
            return base.ToString();
        }
    }
}
