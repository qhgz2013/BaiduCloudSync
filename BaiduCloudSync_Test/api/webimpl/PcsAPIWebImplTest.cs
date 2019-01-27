﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace BaiduCloudSync_Test.api.webimpl
{
    [TestClass]
    public class PcsAPIWebImplTest
    {
        // to test this unit, placing the variables below, you can extract them from cookies
        private static BaiduCloudSync.oauth.IOAuth GetOAuth()
        {
            string baidu_id = "";
            string bduss = "";
            string stoken = "";
            string path = "D:/baidu_oauth.txt";
            BaiduCloudSync.oauth.IOAuth oauth;
            if (string.IsNullOrEmpty(path))
                oauth = new BaiduCloudSync.oauth.SimpleOAuth(baidu_id, bduss, stoken, DateTime.Now + TimeSpan.FromDays(30));
            else
                oauth = new BaiduCloudSync.oauth.SimpleFileOAuth(path);
            return oauth;
        }

        [TestMethod]
        public void PCSAuthInitTest()
        {
            var oauth = GetOAuth();
            var test_obj = new BaiduCloudSync.api.webimpl.PcsAsyncAPIWebImpl(oauth);

        }

        [TestMethod]
        public void PcsAPIListDirTest()
        {
            var oauth = GetOAuth();
            var test_obj = new BaiduCloudSync.api.webimpl.PcsAsyncAPIWebImpl(oauth);
            var wait = new ManualResetEventSlim();
            BaiduCloudSync.api.callbackargs.PcsApiMultiObjectMetaCallbackArgs result = null;
            test_obj.ListDir("/", (sender, e) =>
            {
                result = e;
                wait.Set();
            });
            wait.Wait();
            Assert.IsTrue(result.Success);
            Assert.AreEqual(BaiduCloudSync.api.callbackargs.PcsApiCallbackType.MultiObjectMetadata, result.EventType);
            Assert.IsNotNull(result.PcsMetadatas);
            for (int i = 0; i < result.PcsMetadatas.Length; i++)
            {
                Assert.IsFalse(string.IsNullOrEmpty(result.PcsMetadatas[i].PathInfo.FullPath));
                Assert.IsTrue(result.PcsMetadatas[i].FSID > 0);
                if (!result.PcsMetadatas[0].IsDirectory)
                    Assert.IsFalse(string.IsNullOrEmpty(result.PcsMetadatas[i].MD5));
            }
        }

        [TestMethod]
        public void PcsAPIQuotaTest()
        {
            var oauth = GetOAuth();
            var test_obj = new BaiduCloudSync.api.webimpl.PcsAsyncAPIWebImpl(oauth);
            var wait = new ManualResetEventSlim();
            BaiduCloudSync.api.callbackargs.PcsApiQuotaCallbackArgs result = null;
            test_obj.GetQuota((sender, e) =>
            {
                result = e;
                wait.Set();
            });
            wait.Wait();
            Assert.IsTrue(result.Success);
            Assert.AreEqual(BaiduCloudSync.api.callbackargs.PcsApiCallbackType.Quota, result.EventType);
            Assert.IsTrue(result.Used >= 0);
            Assert.IsTrue(result.Total > 0);
        }

        [TestMethod]
        public void PcsAPICreateDirectoryTest()
        {
            var oauth = GetOAuth();
            var test_obj = new BaiduCloudSync.api.webimpl.PcsAsyncAPIWebImpl(oauth);
            var wait = new ManualResetEventSlim();

            BaiduCloudSync.api.callbackargs.PcsApiOperationCallbackArgs result1 = null;
            var rnd_id = Guid.NewGuid().ToString();
            test_obj.CreateDirectory($"/{rnd_id}", (sender, e) =>
            {
                result1 = e;
                wait.Set();
            });
            wait.Wait();
            wait.Reset();
            Assert.IsTrue(result1.Success);
            Assert.AreEqual(BaiduCloudSync.api.callbackargs.PcsApiCallbackType.OperationResult, result1.EventType);
            
            BaiduCloudSync.api.callbackargs.PcsApiMultiObjectMetaCallbackArgs result = null;
            test_obj.ListDir("/", (sender, e) =>
            {
                result = e;
                wait.Set();
            });
            wait.Wait();
            Assert.IsTrue(result.Success);
            Assert.AreEqual(BaiduCloudSync.api.callbackargs.PcsApiCallbackType.MultiObjectMetadata, result.EventType);
            Assert.IsNotNull(result.PcsMetadatas);
            bool dir_exist = false;
            for (int i = 0; !dir_exist && i < result.PcsMetadatas.Length; i++)
            {
                if (result.PcsMetadatas[i].PathInfo.Name == rnd_id)
                    dir_exist = true;
            }
            Assert.IsTrue(dir_exist);
        }

        [TestMethod]
        public void PcsAPIDeleteTest()
        {
            //from CreateDirectory
            var oauth = GetOAuth();
            var test_obj = new BaiduCloudSync.api.webimpl.PcsAsyncAPIWebImpl(oauth);
            var wait = new ManualResetEventSlim();

            BaiduCloudSync.api.callbackargs.PcsApiOperationCallbackArgs result1 = null;
            var rnd_id = Guid.NewGuid().ToString();
            test_obj.CreateDirectory($"/{rnd_id}", (sender, e) =>
            {
                result1 = e;
                wait.Set();
            });
            wait.Wait();
            wait.Reset();
            Assert.IsTrue(result1.Success);
            Assert.AreEqual(BaiduCloudSync.api.callbackargs.PcsApiCallbackType.OperationResult, result1.EventType);

            BaiduCloudSync.api.callbackargs.PcsApiMultiObjectMetaCallbackArgs result = null;
            test_obj.ListDir("/", (sender, e) =>
            {
                result = e;
                wait.Set();
            });
            wait.Wait();
            wait.Reset();
            Assert.IsTrue(result.Success);
            Assert.AreEqual(BaiduCloudSync.api.callbackargs.PcsApiCallbackType.MultiObjectMetadata, result.EventType);
            Assert.IsNotNull(result.PcsMetadatas);
            bool dir_exist = false;
            for (int i = 0; !dir_exist && i < result.PcsMetadatas.Length; i++)
            {
                if (result.PcsMetadatas[i].PathInfo.Name == rnd_id)
                    dir_exist = true;
            }
            Assert.IsTrue(dir_exist);

            test_obj.Delete(new string[] { $"/{rnd_id}" }, (sender, e) =>
            {
                result1 = e;
                wait.Set();
            });
            wait.Wait();
            Assert.IsTrue(result1.Success);
            Assert.AreEqual(BaiduCloudSync.api.callbackargs.PcsApiCallbackType.OperationResult, result1.EventType);

        }
    }
}
