using Microsoft.VisualStudio.TestTools.UnitTesting;
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

        [TestMethod]
        public void PcsAPIMoveTest()
        {
            // make sure the CreateDirectoryTest has passed before running this test
            // this test no longer validate the CreateDirectory op
            var oauth = GetOAuth();
            var test_obj = new BaiduCloudSync.api.webimpl.PcsAsyncAPIWebImpl(oauth);
            var wait = new ManualResetEventSlim();

            var rnd_id = Guid.NewGuid().ToString();
            var sub_dir_1 = Guid.NewGuid().ToString();
            var sub_dir_2 = Guid.NewGuid().ToString();

            test_obj.CreateDirectory($"/{rnd_id}", (s, e) => { wait.Set(); });
            wait.Wait(); wait.Reset();
            test_obj.CreateDirectory($"/{rnd_id}/{sub_dir_1}", (s, e) => { wait.Set(); });
            wait.Wait(); wait.Reset();
            test_obj.CreateDirectory($"/{rnd_id}/{sub_dir_2}", (s, e) => { wait.Set(); });
            wait.Wait(); wait.Reset();

            // perform move op
            test_obj.Move(new string[] { $"/{rnd_id}/{sub_dir_1}" },
                new string[] { $"/{rnd_id}/{sub_dir_2}/{sub_dir_1}" }, (s, e) => { wait.Set(); });
            wait.Wait(); wait.Reset();

            // check validation
            bool suc = false;
            int count = -1;
            test_obj.ListDir($"/{rnd_id}/{sub_dir_2}/{sub_dir_1}", (s, e) => {
                suc = e.Success;
                count = e.PcsMetadatas.Length;
                wait.Set();
            });

            wait.Wait(); wait.Reset();
            Assert.IsTrue(suc);
            Assert.AreEqual(0, count);

            // cleaning data
            test_obj.Delete(new string[] { $"/{rnd_id}" }, (s, e) => { wait.Set(); });
            wait.Wait();
        }

        [TestMethod]
        public void PcsAPICopyTest()
        {
            // make sure the CreateDirectoryTest has passed before running this test
            // this test no longer validate the CreateDirectory op
            var oauth = GetOAuth();
            var test_obj = new BaiduCloudSync.api.webimpl.PcsAsyncAPIWebImpl(oauth);
            var wait = new ManualResetEventSlim();

            var rnd_id = Guid.NewGuid().ToString();
            var sub_dir_1 = Guid.NewGuid().ToString();
            var sub_dir_2 = Guid.NewGuid().ToString();

            test_obj.CreateDirectory($"/{rnd_id}", (s, e) => { wait.Set(); });
            wait.Wait(); wait.Reset();
            test_obj.CreateDirectory($"/{rnd_id}/{sub_dir_1}", (s, e) => { wait.Set(); });
            wait.Wait(); wait.Reset();
            test_obj.CreateDirectory($"/{rnd_id}/{sub_dir_2}", (s, e) => { wait.Set(); });
            wait.Wait(); wait.Reset();

            // perform copy op
            test_obj.Copy(new string[] { $"/{rnd_id}/{sub_dir_1}" },
                new string[] { $"/{rnd_id}/{sub_dir_2}/{sub_dir_1}" }, (s, e) => { wait.Set(); });
            wait.Wait(); wait.Reset();

            // check validation
            bool suc = false;
            int count = -1;
            test_obj.ListDir($"/{rnd_id}/{sub_dir_2}/{sub_dir_1}", (s, e) => {
                suc = e.Success;
                count = e.PcsMetadatas.Length;
                wait.Set();
            });
            wait.Wait(); wait.Reset();
            Assert.IsTrue(suc);
            Assert.AreEqual(0, count);

            test_obj.ListDir($"/{rnd_id}/{sub_dir_1}", (s, e) => {
                suc = e.Success;
                count = e.PcsMetadatas.Length;
                wait.Set();
            });
            wait.Wait(); wait.Reset();
            Assert.IsTrue(suc);
            Assert.AreEqual(0, count);

            // cleaning data
            test_obj.Delete(new string[] { $"/{rnd_id}" }, (s, e) => { wait.Set(); });
            wait.Wait();
        }

        [TestMethod]
        public void PcsAPIRenameTest()
        {
            // make sure the CreateDirectoryTest has passed before running this test
            // this test no longer validate the CreateDirectory op
            var oauth = GetOAuth();
            var test_obj = new BaiduCloudSync.api.webimpl.PcsAsyncAPIWebImpl(oauth);
            var wait = new ManualResetEventSlim();

            var rnd_id = Guid.NewGuid().ToString();
            var sub_dir_1 = Guid.NewGuid().ToString();
            var sub_dir_2 = Guid.NewGuid().ToString();

            test_obj.CreateDirectory($"/{rnd_id}", (s, e) => { wait.Set(); });
            wait.Wait(); wait.Reset();
            test_obj.CreateDirectory($"/{rnd_id}/{sub_dir_1}", (s, e) => { wait.Set(); });
            wait.Wait(); wait.Reset();

            // perform rename op
            test_obj.Rename(new string[] { $"/{rnd_id}/{sub_dir_1}" },
                new string[] { sub_dir_2 }, (s, e) => { wait.Set(); });
            wait.Wait(); wait.Reset();

            // check validation
            bool suc = false;
            int count = -1;
            test_obj.ListDir($"/{rnd_id}/{sub_dir_2}", (s, e) => {
                suc = e.Success;
                count = e.PcsMetadatas.Length;
                wait.Set();
            });
            wait.Wait(); wait.Reset();
            Assert.IsTrue(suc);
            Assert.AreEqual(0, count);

            test_obj.ListDir($"/{rnd_id}/{sub_dir_1}", (s, e) => {
                suc = e.Success;
                wait.Set();
            });
            wait.Wait(); wait.Reset();
            Assert.IsFalse(suc);

            // cleaning data
            test_obj.Delete(new string[] { $"/{rnd_id}" }, (s, e) => { wait.Set(); });
            wait.Wait();
        }


        [TestMethod]
        public void PcsAPIRapidUploadTest()
        {
            var oauth = GetOAuth();
            var test_obj = new BaiduCloudSync.api.webimpl.PcsAsyncAPIWebImpl(oauth);
            var wait = new ManualResetEventSlim();

            // sample rapid upload data
            long content_length = 34348557;
            string content_md5 = "b48e7f72563509f15f9768d85298fd0c";
            string slice_md5 = "48fb5cde36d102db289412b3ff68760d";

            string rnd_id = Guid.NewGuid().ToString();
            BaiduCloudSync.api.PcsMetadata metadata = null;
            bool suc = false;
            test_obj.RapidUpload($"/{rnd_id}", content_length, content_md5, slice_md5, (s, e) =>
            {
                metadata = e.Metadata;
                suc = e.Success;
                wait.Set();
            });

            wait.Wait(); wait.Reset();

            // cleaning test
            test_obj.Delete(new string[] { $"/{rnd_id}" }, (s, e) => { wait.Set(); });
            wait.Wait();

            Assert.IsTrue(suc);
            Assert.AreEqual(content_md5, metadata.MD5.ToLower());
            Assert.AreEqual(content_length, metadata.Size);
        }

        [TestMethod]
        public void PcsAPIUploadTest()
        {
            var oauth = GetOAuth();
            var test_obj = new BaiduCloudSync.api.webimpl.PcsAsyncAPIWebImpl(oauth);
            var wait = new ManualResetEventSlim();

            var buffer = new byte[4198400]; // 4MB + 4kB
            var rnd_fill = new Random();
            rnd_fill.NextBytes(buffer);

            var rnd_id = Guid.NewGuid().ToString();
            var segment_md5 = new string[]
            {
                GlobalUtil.Util.Hex(GlobalUtil.hash.MD5.ComputeHash(buffer, 0, 4194304)),
                GlobalUtil.Util.Hex(GlobalUtil.hash.MD5.ComputeHash(buffer, 4194304, 4096))
            };

            // test here
            bool suc = false;
            string upload_id = null;
            test_obj.PreCreate($"/{rnd_id}", 2, (s, e) =>
            {
                suc = e.Success;
                upload_id = e.UploadID;
                wait.Set();
            });
            wait.Wait(); wait.Reset();
            Assert.IsTrue(suc);
            Assert.IsFalse(string.IsNullOrEmpty(upload_id));

            var tmp = new byte[4194304];
            Array.Copy(buffer, tmp, tmp.Length);
            test_obj.SuperFile($"/{rnd_id}", upload_id, 0, tmp, (s, e) =>
            {
                suc = e.Success && e.SegmentMD5 == segment_md5[0];
                wait.Set();
            });
            wait.Wait(); wait.Reset();
            Assert.IsTrue(suc);

            tmp = new byte[4096];
            Array.Copy(buffer, 4194304, tmp, 0, 4096);
            test_obj.SuperFile($"/{rnd_id}", upload_id, 1, tmp, (s, e) =>
            {
                suc = e.Success && e.SegmentMD5 == segment_md5[1];
                wait.Set();
            });
            wait.Wait(); wait.Reset();
            Assert.IsTrue(suc);

            long size_got = 0;
            string md5_got = null;
            test_obj.Create($"/{rnd_id}", segment_md5, buffer.Length, upload_id, (s, e) =>
            {
                suc = e.Success;
                size_got = e.Metadata.Size;
                md5_got = e.Metadata.MD5;
                wait.Set();
            });
            wait.Wait(); wait.Reset();
            Assert.IsTrue(suc);
            Assert.AreEqual(buffer.Length, size_got);

            GlobalUtil.Tracer.GlobalTracer.TraceInfo($"MD5 Expected: {GlobalUtil.Util.Hex(GlobalUtil.hash.MD5.ComputeHash(buffer, 0, buffer.Length))}");
            GlobalUtil.Tracer.GlobalTracer.TraceInfo($"MD5 Got: {md5_got}");

            // cleaning data
            test_obj.Delete(new string[] { $"/{rnd_id}" }, (s, e) => { wait.Set(); });
            wait.Wait();
        }
    }
}
