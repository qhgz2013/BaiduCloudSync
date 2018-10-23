using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GlobalUtil.http
{
    /// <summary>
    /// HTTP请求错误时引发的异常
    /// </summary>
    [Serializable]
    public class HttpException : Exception
    {
        public HttpException() : base() { }
        public HttpException(string message) : base(message) { }
        public HttpException(string message, Exception innerException) : base(message, innerException) { }
    }
}
