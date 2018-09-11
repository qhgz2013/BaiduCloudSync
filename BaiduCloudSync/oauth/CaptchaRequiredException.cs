using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.oauth
{
    /// <summary>
    /// 登陆需要验证码时引发的异常
    /// </summary>
    public class CaptchaRequiredException : LoginFailedException
    {
        public CaptchaRequiredException() : base() { }
        public CaptchaRequiredException(string message) : base(message) { }
        public CaptchaRequiredException(string message, Exception innerException) : base(message, innerException) { }
    }
}
