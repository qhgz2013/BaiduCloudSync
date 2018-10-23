using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BaiduCloudSync_Test.util.net_util
{
    [TestClass]
    public class HttpWebRequestHelperTest
    {
        [TestMethod]
        public void TestMergeParameters()
        {
            var request = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create("https://baidu.com/");
            var param = new GlobalUtil.Parameters();
            param.Add("Accept", "*.*");
            param.Add("Connection", "close");
            param.Add("Content-Length", "23333");
            param.Add("Content-Type", "text/html");
            param.Add("Expect", "100-Continue");
            param.Add("Date", new DateTime(2018, 1, 1, 10, 30, 0));
            param.Add("Host", "baidu.com");
            param.Add("If-Modified-Since", new DateTime(2017, 12, 1, 23, 59, 59));
            param.Add("Range", "bytes=0-");
            param.Add("Referer", "https://www.baidu.com/");
            param.Add("Transfer-Encoding", "chunked");
            param.Add("User-Agent", "NyaNya");
            param.Add("Origin", "https://www.baidu.com");
            GlobalUtil.HttpWebRequestHelper.MergeParametersToWebRequestHeader(param, request);
            Assert.AreEqual(request.Accept, "*.*");
            Assert.AreEqual(request.KeepAlive, false);
            Assert.AreEqual(request.ContentLength, 23333);
            Assert.AreEqual(request.ContentType, "text/html");
            Assert.AreEqual(request.ServicePoint.Expect100Continue, true);
            Assert.AreEqual(request.Date, new DateTime(2018, 1, 1, 10, 30, 0));
            Assert.AreEqual(request.Host, "baidu.com");
            Assert.AreEqual(request.IfModifiedSince, new DateTime(2017, 12, 1, 23, 59, 59));
            Assert.AreEqual(request.Headers[System.Net.HttpRequestHeader.Range], "bytes=0-");
            Assert.AreEqual(request.Referer, "https://www.baidu.com/");
            Assert.AreEqual(request.SendChunked, true);
            Assert.AreEqual(request.UserAgent, "NyaNya");
            Assert.AreEqual(request.Headers["Origin"], "https://www.baidu.com");
        }
    }
}
