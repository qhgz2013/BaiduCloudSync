using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GlobalUtil.http
{
    /// <summary>
    /// HTTP请求重试次数超过限定次数时引发的异常，其中包含了每次请求引发的异常
    /// </summary>
    [Serializable]
    public class StackedHttpException : HttpException, ICollection<Exception>
    {
        private List<Exception> _exceptions;

        public StackedHttpException() : base() { _exceptions = new List<Exception>(); }
        public StackedHttpException(string message): base(message) { _exceptions = new List<Exception>(); }
        public StackedHttpException(string message, Exception innerException):base(message, innerException)
        {
            _exceptions = new List<Exception>();
            _exceptions.Add(innerException);
        }
        public StackedHttpException(string message, IEnumerable<Exception> innerExceptions) : base(message, innerExceptions.LastOrDefault())
        {
            _exceptions = new List<Exception>();
            _exceptions.AddRange(innerExceptions);
        }

        public int Count
        {
            get
            {
                return ((ICollection<Exception>)_exceptions).Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return ((ICollection<Exception>)_exceptions).IsReadOnly;
            }
        }

        public void Add(Exception item)
        {
            ((ICollection<Exception>)_exceptions).Add(item);
        }

        public void Clear()
        {
            ((ICollection<Exception>)_exceptions).Clear();
        }

        public bool Contains(Exception item)
        {
            return ((ICollection<Exception>)_exceptions).Contains(item);
        }

        public void CopyTo(Exception[] array, int arrayIndex)
        {
            ((ICollection<Exception>)_exceptions).CopyTo(array, arrayIndex);
        }

        public IEnumerator<Exception> GetEnumerator()
        {
            return ((ICollection<Exception>)_exceptions).GetEnumerator();
        }

        public bool Remove(Exception item)
        {
            return ((ICollection<Exception>)_exceptions).Remove(item);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((ICollection<Exception>)_exceptions).GetEnumerator();
        }
    }
}
