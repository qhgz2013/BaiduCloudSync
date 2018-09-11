using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.oauth
{
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
