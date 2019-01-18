using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync_Test.api.webimpl
{
    [TestClass]
    public class PcsAPIWebImplTest
    {
        [TestMethod]
        public void PCSAuthInitTest()
        {
            // to test this unit, placing the variables below, you can extract them from cookies
            string baidu_id = "";
            string bduss = "";
            string stoken = "";
            var oauth = new BaiduCloudSync.oauth.SimpleOAuth(baidu_id, bduss, stoken, DateTime.Now + TimeSpan.FromDays(30));
            var test_obj = new BaiduCloudSync.api.webimpl.PcsAPIWebImpl(oauth);

        }
    }
}
