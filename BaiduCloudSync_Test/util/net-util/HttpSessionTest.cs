using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BaiduCloudSync_Test.util.net_util
{
    [TestClass]
    public class HttpSessionTest
    {
        [TestMethod]
        public void HttpGetAsyncTest()
        {
            var wait_event = new System.Threading.ManualResetEventSlim();
            var test_obj = new GlobalUtil.http.HttpSession();
            test_obj.HttpGetAsync("https://www.baidu.com", (s, e) =>
            {
                wait_event.Set();
            }, wait_event);
            wait_event.Wait();

        }
    }
}
