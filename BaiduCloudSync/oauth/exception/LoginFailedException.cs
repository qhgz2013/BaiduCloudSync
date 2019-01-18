using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.oauth.exception
{
    /// <summary>
    /// 登陆失败时引发的异常
    /// </summary>
    [Serializable]
    public class LoginFailedException : Exception
    {
        public int FailCode { get; private set; }
        public LoginFailedException(int fail_code = -1) : base() { FailCode = fail_code; }
        public LoginFailedException(string message, int fail_code = -1) : base(message) { FailCode = fail_code; }
        public LoginFailedException(string message, Exception innerException, int fail_code = -1) : base(message, innerException) { FailCode = fail_code; }

        public override string ToString()
        {
            return "[" + FailCode + "] " + base.ToString();
        }
    }
}
