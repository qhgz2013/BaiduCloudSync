﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.oauth.exception
{
    /// <summary>
    /// 验证码错误时引发的异常
    /// </summary>
    [Serializable]
    public class InvalidCaptchaException : LoginFailedException
    {
        public InvalidCaptchaException() : base() { }
        public InvalidCaptchaException(string message) : base(message) { }
        public InvalidCaptchaException(string message, Exception innerException) : base(message, innerException) { }
    }
}
