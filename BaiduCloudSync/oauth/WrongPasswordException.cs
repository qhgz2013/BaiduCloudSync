using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.oauth
{
    public class WrongPasswordException : LoginFailedException
    {
        public WrongPasswordException() : base() { }
        public WrongPasswordException(string message) : base(message) { }
        public WrongPasswordException(string message, Exception innerException) : base(message, innerException) { }
    }
}
