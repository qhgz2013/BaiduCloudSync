using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BaiduCloudSync_Test.util.net_util
{
    [TestClass]
    public class CookieParserTest
    {
        [TestMethod]
        public void TestParseCookieBasic1()
        {
            var cookie = GlobalUtil.CookieParser.ParseCookie("Set-Cookie: id=23|3?");
            Assert.AreEqual(cookie.Name, "id");
            Assert.AreEqual(cookie.Value, "23|3?");
        }
        [TestMethod]
        public void TestParseCookieBasic2()
        {
            var cookie = GlobalUtil.CookieParser.ParseCookie("Set-Cookie: id=\"23|3?\"");
            Assert.AreEqual(cookie.Name, "id");
            Assert.AreEqual(cookie.Value, "23|3?");
        }
        [TestMethod]
        public void TestParseCookieWithExpires1()
        {
            var cookie = GlobalUtil.CookieParser.ParseCookie("Set-Cookie: id=2333; Expires=Sun, 21 Oct 2018 22:39:00 GMT");
            Assert.AreEqual(cookie.Name, "id");
            Assert.AreEqual(cookie.Value, "2333");
            Assert.AreEqual(cookie.Expires, new DateTime(2018, 10, 21, 22, 39, 0));
        }

        [TestMethod]
        public void TestParseCookieWithMaxAge()
        {
            var dt_start = DateTime.Now;
            var cookie = GlobalUtil.CookieParser.ParseCookie("Set-Cookie: oh_no=yeah; Max-Age=1000");
            Assert.AreEqual(cookie.Name, "oh_no");
            Assert.AreEqual(cookie.Value, "yeah");
            var dt_end = DateTime.Now;
            var expected_expire_time = dt_start.AddSeconds(1000) + (dt_end - dt_start);
            Assert.IsTrue(Math.Abs((cookie.Expires - expected_expire_time).TotalMilliseconds) < 1000);
        }

        [TestMethod]
        public void TestParseCookieWithExpiresAndMaxAge1()
        {
            var dt_start = DateTime.Now;
            var cookie = GlobalUtil.CookieParser.ParseCookie("Set-Cookie: oh_no=yeah; Max-Age=1000; Expires=Sun, 21 Oct 2018 22:39:00 GMT");
            Assert.AreEqual(cookie.Name, "oh_no");
            Assert.AreEqual(cookie.Value, "yeah");
            var dt_end = DateTime.Now;
            var expected_expire_time = dt_start.AddSeconds(1000) + (dt_end - dt_start);
            Assert.IsTrue(Math.Abs((cookie.Expires - expected_expire_time).TotalMilliseconds) < 1000);
        }
        [TestMethod]
        public void TestParseCookieWithExpiresAndMaxAge2()
        {
            var dt_start = DateTime.Now;
            var cookie = GlobalUtil.CookieParser.ParseCookie("Set-Cookie: oh_no=yeah; Expires=Sun, 21 Oct 2018 22:39:00 GMT; Max-Age=1000");
            Assert.AreEqual(cookie.Name, "oh_no");
            Assert.AreEqual(cookie.Value, "yeah");
            var dt_end = DateTime.Now;
            var expected_expire_time = dt_start.AddSeconds(1000) + (dt_end - dt_start);
            Assert.IsTrue(Math.Abs((cookie.Expires - expected_expire_time).TotalMilliseconds) < 1000);
        }
        [TestMethod]
        public void TestParseCookieWithDomain()
        {
            var cookie = GlobalUtil.CookieParser.ParseCookie("Set-Cookie: foo=bar; Domain=.baidu.com; Max-Age=2333");
            Assert.AreEqual(cookie.Name, "foo");
            Assert.AreEqual(cookie.Value, "bar");
            Assert.AreEqual(cookie.Domain, ".baidu.com");
        }
        [TestMethod]
        public void TestParseCookieWithSecure()
        {
            var cookie = GlobalUtil.CookieParser.ParseCookie("Set-Cookie: foo=bar; Secure");
            Assert.AreEqual(cookie.Name, "foo");
            Assert.AreEqual(cookie.Value, "bar");
            Assert.IsTrue(cookie.Secure);
        }
        [TestMethod]
        public void TestParseCookieWithHttpOnly()
        {
            var cookie = GlobalUtil.CookieParser.ParseCookie("Set-Cookie: foo=bar; HttpOnly");
            Assert.AreEqual(cookie.Name, "foo");
            Assert.AreEqual(cookie.Value, "bar");
            Assert.IsTrue(cookie.HttpOnly);
        }
        [TestMethod]
        public void TestParseCookieWithExtensionSupport()
        {
            var cookie = GlobalUtil.CookieParser.ParseCookie("Set-Cookie: foo=bar; ThisisExtension");
            Assert.AreEqual(cookie.Name, "foo");
            Assert.AreEqual(cookie.Value, "bar");
        }
        [TestMethod]
        public void TestParseCookieWithExpire2()
        {
            var cookie = GlobalUtil.CookieParser.ParseCookie("Set-Cookie: id=2333; Expires=Monday, 21-Oct-18 22:39:00 GMT");
            Assert.AreEqual(cookie.Name, "id");
            Assert.AreEqual(cookie.Value, "2333");
            Assert.AreEqual(cookie.Expires, new DateTime(2318, 10, 21, 22, 39, 0));
        }
        [TestMethod]
        public void TestParseCookieWithExpire3()
        {
            var cookie = GlobalUtil.CookieParser.ParseCookie("Set-Cookie: id=2333; Expires=Sun Oct 21 22:39:00 2018");
            Assert.AreEqual(cookie.Name, "id");
            Assert.AreEqual(cookie.Value, "2333");
            Assert.AreEqual(cookie.Expires, new DateTime(2018, 10, 21, 22, 39, 0));
            cookie = GlobalUtil.CookieParser.ParseCookie("Set-Cookie: id=2333; Expires=Sun Oct  1 22:39:00 2018");
            Assert.AreEqual(cookie.Name, "id");
            Assert.AreEqual(cookie.Value, "2333");
            Assert.AreEqual(cookie.Expires, new DateTime(2018, 10, 1, 22, 39, 0));
        }
        //error test
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void TestParseCookieWithEmptyKey()
        {
            GlobalUtil.CookieParser.ParseCookie("Set-Cookie: =2333");
            Assert.Fail();
        }
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void TestParseCookieWithEmptyValue()
        {
            GlobalUtil.CookieParser.ParseCookie("Set-Cookie: id=");
            Assert.Fail();
        }
        //here ignore the attribute value without throwing the error
        [TestMethod]
        public void TestParseCookieWithEmptyDomain()
        {
            var cookie = GlobalUtil.CookieParser.ParseCookie("Set-Cookie: id=2333; Domain=");
            Assert.IsTrue(string.IsNullOrEmpty(cookie.Domain));
        }
        [TestMethod]
        public void TestParseCookieWithEmptyPath()
        {
            var cookie = GlobalUtil.CookieParser.ParseCookie("Set-Cookie: id=2333; Path=");
            Assert.IsTrue(string.IsNullOrEmpty(cookie.Path));
        }
        [TestMethod]
        public void TestParseCookieWithEmptyExipres()
        {
            var cookie = GlobalUtil.CookieParser.ParseCookie("Set-Cookie: id=2333; Expires=");
            Assert.AreEqual(cookie.Expires, DateTime.MinValue);
        }
        [TestMethod]
        public void TestParseCookieWithEmptyMaxAge()
        {
            var cookie = GlobalUtil.CookieParser.ParseCookie("Set-Cookie: id=2333; Max-Age=");
            Assert.AreEqual(cookie.Expires, DateTime.MinValue);
        }
        [TestMethod]
        public void TestParseCookieWithExpires4()
        {
            var cookie = GlobalUtil.CookieParser.ParseCookie("Set-Cookie: id=2333; Expires=Sunday, 21 Oct 2018 22:39:00 GMT; HttpOnly");
            Assert.AreEqual(cookie.Name, "id");
            Assert.AreEqual(cookie.Value, "2333");
            Assert.AreEqual(cookie.Expires, new DateTime(2018, 10, 21, 22, 39, 0));
            Assert.IsTrue(cookie.HttpOnly);
        }
        [TestMethod]
        public void TestParseCookieWithExpires5()
        {
            var cookie = GlobalUtil.CookieParser.ParseCookie("Set-Cookie: id=2333; Expires=Sun, 21-Oct-2018 22:39:00 GMT; HttpOnly");
            Assert.AreEqual(cookie.Name, "id");
            Assert.AreEqual(cookie.Value, "2333");
            Assert.AreEqual(cookie.Expires, new DateTime(2018, 10, 21, 22, 39, 0));
            Assert.IsTrue(cookie.HttpOnly);
        }
        [TestMethod]
        public void TestParseCookieWithInvalidExpires2()
        {
            var cookie = GlobalUtil.CookieParser.ParseCookie("Set-Cookie: id=2333; Expires=blah_blah_blah; Domain=youtube.com");
            Assert.AreEqual(cookie.Name, "id");
            Assert.AreEqual(cookie.Value, "2333");
            Assert.AreEqual(cookie.Expires, DateTime.MinValue);
            Assert.AreEqual(cookie.Domain, "youtube.com");
        }
    }
}
