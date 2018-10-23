using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GlobalUtil;

namespace BaiduCloudSync_Test.util.net_util
{
    [TestClass]
    public class RangeTest
    {
        [TestMethod]
        public void RangeTest1()
        {
            var range = new Range();
            Assert.IsNull(range.From);
            Assert.IsNull(range.To);
        }

        [TestMethod]
        public void RangeTest2()
        {
            var range = new Range(5);
            Assert.IsNull(range.To);
            Assert.AreEqual(range.From, 5);
        }

        [TestMethod]
        public void RangeTest3()
        {
            var range = new Range(-5);
            Assert.IsNull(range.From);
            Assert.AreEqual(range.To, 5);
        }

        [TestMethod]
        public void RangeTest4()
        {
            var range = new Range(-5, 5);
            Assert.IsNull(range.From);
            Assert.AreEqual(range.To, 5);
        }
        [TestMethod]
        public void RangeTest5()
        {
            var range = new Range(null, 233);
            Assert.IsNull(range.From);
            Assert.AreEqual(range.To, 233);
        }
        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void RangeTest6()
        {
            var range = new Range(null, -666);
            Assert.Fail();
        }
        [TestMethod]
        public void RangeTest7()
        {
            var range = new Range(123, 456);
            Assert.AreEqual(range.From, 123);
            Assert.AreEqual(range.To, 456);
        }
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void RangeTest8()
        {
            var range = new Range(456, 123);
            Assert.Fail();
        }

        [TestMethod]
        public void ParseRangeTest1()
        {
            var range = Range.Parse("");
            Assert.IsNull(range.From);
            Assert.IsNull(range.To);
        }
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ParseRangeTest2()
        {
            Range.Parse(null);
            Assert.Fail();
        }
        [TestMethod]
        public void ParseRangeTest3()
        {
            var range = Range.Parse("123-");
            Assert.AreEqual(range.From, 123);
            Assert.IsNull(range.To);
        }
        [TestMethod]
        public void ParseRangeTest4()
        {
            var range = Range.Parse("-456");
            Assert.IsNull(range.From);
            Assert.AreEqual(range.To, 456);
        }
        [TestMethod]
        public void ParseRangeTest5()
        {
            var range = Range.Parse("123-456");
            Assert.AreEqual(range.From, 123);
            Assert.AreEqual(range.To, 456);
        }
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ParseRangeTest6()
        {
            var range = Range.Parse("456-123");
            Assert.Fail();
        }
    }
}
