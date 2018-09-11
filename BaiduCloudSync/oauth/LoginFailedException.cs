using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.oauth
{
    /// <summary>
    /// 登陆失败时引发的异常
    /// </summary>
    public class LoginFailedException : Exception
    {
        public LoginFailedException() : base() { }
        public LoginFailedException(string message) : base(message) { }
        public LoginFailedException(string message, Exception innerException) : base(message, innerException) { }
    }
}
