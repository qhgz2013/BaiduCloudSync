using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.api.webimpl
{
    [Serializable]
    public sealed class PCSApiUnexpectedResponseException : Exception
    {
        public PCSApiUnexpectedResponseException() : base() { }
        public PCSApiUnexpectedResponseException(string message) : base(message) { }
        public PCSApiUnexpectedResponseException(string message, Exception innerException) : base(message, innerException) { }
    }
}
