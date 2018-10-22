using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync_Test
{
    /// <summary>
    /// 多类型异常的捕获器，在捕获到其他异常时，会引发 UnexpectedExceptionTypeException
    /// </summary>
    internal class MultipleExceptionTypeCatcher
    {
        private List<Type> _exception_types;

        public MultipleExceptionTypeCatcher(IEnumerable<Type> types)
        {
            _exception_types = new List<Type>();
            foreach (Type t in types)
            {
                if (!t.IsSubclassOf(typeof(Exception)))
                    throw new InvalidCastException("type " + t.ToString() + " is not a subclass of Exception");
                _exception_types.Add(t);
            }
        }

        /// <summary>
        /// 执行可能引发异常的代码，并返回引发的异常类型（未引发异常时返回null），捕获到其他异常会抛出 UnexpectedExceptionTypeException
        /// </summary>
        /// <param name="execute_action"></param>
        /// <returns></returns>
        public Type Throws(Action execute_action)
        {
            try
            {
                execute_action.Invoke();
                return null;
            }
            catch (Exception ex)
            {
                var exception_type = ex.GetType();
                if (!_exception_types.Contains(exception_type))
                    throw new UnexpectedExceptionTypeException("Exception type " + exception_type.ToString() + " is unexpected");
                return exception_type;
            }
        }
    }

    /// <summary>
    /// 在期望引发的异常列表内，未包含该种异常时，引发的异常
    /// </summary>
    [Serializable]
    internal class UnexpectedExceptionTypeException : Exception
    {
        public UnexpectedExceptionTypeException() : base() { }
        public UnexpectedExceptionTypeException(string message) : base(message) { }
        public UnexpectedExceptionTypeException(string message, Exception innerException) : base(message, innerException) { }
    }

    [TestClass]
    public class MultipleExceptionTypeCatcherTest
    {
        [TestMethod]
        public void ThrowsTest_NoException()
        {
            var test_obj = new MultipleExceptionTypeCatcher(new Type[] { typeof(ArithmeticException) });
            Assert.IsNull(test_obj.Throws(delegate { }));
        }

        [TestMethod]
        public void ThrowsTest_ExpectedException()
        {
            var test_obj = new MultipleExceptionTypeCatcher(new Type[] { typeof(ArithmeticException), typeof(ArgumentException) });
            Assert.IsTrue(test_obj.Throws(delegate { throw new ArithmeticException(); }) == typeof(ArithmeticException));
            Assert.IsTrue(test_obj.Throws(delegate { throw new ArgumentException(); }) == typeof(ArgumentException));
        }

        [TestMethod]
        [ExpectedException(typeof(UnexpectedExceptionTypeException))]
        public void ThrowsTest_UnexpectedException()
        {
            var test_obj = new MultipleExceptionTypeCatcher(new Type[] { typeof(ArithmeticException) });
            test_obj.Throws(delegate { throw new IndexOutOfRangeException(); });
        }
    }
}
