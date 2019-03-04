using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GlobalUtil.hash
{
    /// <summary>
    /// 在序列化或逆序列化时出现IO错误或格式错误时引发的异常
    /// </summary>
    [Serializable]
    public class SerializationException : Exception
    {
        public SerializationException(): base() { }
        public SerializationException(string message): base(message) { }
        public SerializationException(string message, Exception innerException) : base(message, innerException) { }

    }
}
