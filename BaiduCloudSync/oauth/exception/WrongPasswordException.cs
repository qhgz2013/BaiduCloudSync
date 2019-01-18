using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.oauth.exception
{
    /// <summary>
    /// 登陆密码错误时引发的异常
    /// </summary>
    [Serializable]
    public class WrongPasswordException : LoginFailedException
    {
        public WrongPasswordException() : base() { }
        public WrongPasswordException(string message) : base(message) { }
        public WrongPasswordException(string message, Exception innerException) : base(message, innerException) { }
    }
}
