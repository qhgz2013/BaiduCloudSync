using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using GlobalUtil;

namespace BaiduCloudSync
{
    class TestBench
    {

        public static void TestPCS_API(BaiduPCS api)
        {
            var trace = Tracer.GlobalTracer;

            trace.TraceInfo("Testing PCS API...");

            try { api.DeletePath("/pcsapi_testbench"); } catch { }

            trace.TraceInfo("[1/88] Creating Directory (general test)");
            try
            {
                var temp_dir = api.CreateDirectory("/pcsapi_testbench"); //meta: /pcsapi_testbench
            }
            catch (Exception ex)
            {
                trace.TraceError(ex);
                return;
            }

            trace.TraceInfo("[2/88] Creating Directory (conflict test)");
            try
            {
                var temp_dir = api.CreateDirectory("/pcsapi_testbench/conflict_test"); //meta: /pcsapi_testbench/conflict_test
                temp_dir = api.CreateDirectory("/pcsapi_testbench/conflict_test"); //meta: /pcsapi_testbench_conflict_test(1)
            }
            catch { }

            trace.TraceInfo("[3/88] Creating Directory (special character test)");
            try
            {
                var temp_dir = api.CreateDirectory("/pcsapi_testbench/this<is>a-D!I:R!"); //errno = -7
            }
            catch { }

            var rnd = new Random();
            trace.TraceInfo("[4/88] Upload Test (100 Bytes)");
            var data = new byte[100];
            rnd.NextBytes(data);
            var ms = new MemoryStream(data);
            try
            {
                var temp_file = api.UploadRaw(ms, 100, "/pcsapi_testbench/100b_file.dat", BaiduPCS.ondup.overwrite); //meta: /pcsapi_testbench/100b_file.dat
            }
            catch { }

            trace.TraceInfo("[5/88] Upload Test (5 MB)");
            data = new byte[5242880];
            rnd.NextBytes(data);
            var ms2 = new MemoryStream(data);
            var filedata = new ObjectMetadata();
            try
            {
                var temp_file = api.UploadRaw(ms2, (ulong)data.Length, "/pcsapi_testbench/5mb_file.dat", BaiduPCS.ondup.overwrite); //meta: /pcsapi_testbench/5mb_file.dat
                filedata = temp_file;
            }
            catch { }
            //calculating data
            var md5cp = new System.Security.Cryptography.MD5CryptoServiceProvider();
            md5cp.TransformFinalBlock(data, 0, data.Length);
            var content_md5 = util.Hex(md5cp.Hash);
            md5cp.Initialize();
            md5cp.TransformFinalBlock(data, 0, (int)BaiduPCS.VALIDATE_SIZE);
            var slice_md5 = util.Hex(md5cp.Hash);
            var crccp = new Crc32();
            crccp.TransformBlock(data, 0, data.Length);
            var content_crc32 = crccp.Hash.ToString("X2").ToLower();

            trace.TraceInfo("[6/88] Upload Test (overwrite test)");
            data = new byte[300];
            rnd.NextBytes(data);
            ms = new MemoryStream(data);
            try
            {
                var temp_file = api.UploadRaw(ms, 300, "/pcsapi_testbench/100b_file.dat", BaiduPCS.ondup.overwrite); //meta: /pcsapi_testbench/100b_file.dat
            }
            catch { }

            trace.TraceInfo("[7/88] Upload Test (newcopy test)");
            data = new byte[400];
            rnd.NextBytes(data);
            ms = new MemoryStream(data);
            try
            {
                var temp_file = api.UploadRaw(ms, 400, "/pcsapi_testbench/100b_file.dat", BaiduPCS.ondup.newcopy); //meta: /pcsapi_testbench/100b_file_yyyyMMddHHmmss.dat
            }
            catch { }

            trace.TraceInfo("[8/88] Upload Test (path invalid test)");
            ms.Seek(0, SeekOrigin.Begin);
            try
            {
                var temp_file = api.UploadRaw(ms, 400, "/pcsapi_testbench/100b_file<:nonono:>.dat", BaiduPCS.ondup.newcopy); //HTTP 400 with errno 31062 "file name is invalid"
            }
            catch { }

            trace.TraceInfo("[9/88] Upload Test (zero length file test)");
            ms.Seek(0, SeekOrigin.Begin);
            try
            {
                var temp_file = api.UploadRaw(ms, 0, "/pcsapi_testbench/zero_file.dat"); //meta: /pcsapi_testbench/zero_file.dat (with 0 size)
            }
            catch { }

            trace.TraceInfo("[10/88] Download Test (API, http)");
            try
            {
                var download_data = api.GetDownloadLink_API("/pcsapi_testbench/5mb_file.dat", false); //http://pcs.baidu.com/rest/2.0/pcs/file?method=download&app_id=250528&path=%2Fpcsapi_testbench%2F5mb_file.dat
            }
            catch { }

            trace.TraceInfo("[11/88] Download Test (API, https)");
            try
            {
                var download_data = api.GetDownloadLink_API("/pcsapi_testbench/5mb_file.dat", true); //https://pcs.baidu.com/rest/2.0/pcs/file?method=download&app_id=250528&path=%2Fpcsapi_testbench%2F5mb_file.dat
            }
            catch { }

            trace.TraceInfo("[12/88] Download Test (API, null path)");
            try
            {
                var download_data = api.GetDownloadLink_API(""); //https://pcs.baidu.com/rest/2.0/pcs/file?method=download&app_id=250528&path=
            }
            catch { }

            trace.TraceInfo("[13/88] Download Test (API, invalid path)");
            try
            {
                var download_data = api.GetDownloadLink_API("/pcsapi_testbench/<><><>aaaaa"); //https://pcs.baidu.com/rest/2.0/pcs/file?method=download&app_id=250528&path=%2Fpcsapi_testbench%2F%3C%3E%3C%3E%3C%3Eaaaaa
            }
            catch { }

            trace.TraceInfo("[14/88] Get File List Test (general test)");
            try
            {
                var files = api.GetFileList("/pcsapi_testbench"); //meta: { count=6 }
            }
            catch { }

            trace.TraceInfo("[15/88] Get File List Test (non exist path test)");
            try
            {
                var files = api.GetFileList("/pcsapi_testbench/non_exist_path"); //errno -9
            }
            catch { }

            trace.TraceInfo("[16/88] Get File List Test (invalid path test)");
            try
            {
                var files = api.GetFileList("/pcsapi_testbench/aaa:"); //errno -7
            }
            catch { }

            trace.TraceInfo("[17/88] Get File List Test (path is file test)");
            try
            {
                var files = api.GetFileList("/pcsapi_testbench/100b_file.dat"); //meta: { count = 0 }
            }
            catch { }

            trace.TraceInfo("[18/88] Get File List Test (desc order test)");
            try
            {
                var files = api.GetFileList("/pcsapi_testbench", asc: false); //meta: { count=6 }
            }
            catch { }

            trace.TraceInfo("[19/88] Get File List Test (order by size test)");
            try
            {
                var files = api.GetFileList("/pcsapi_testbench", BaiduPCS.FileOrder.size); //meta: { count=6 }
            }
            catch { }

            trace.TraceInfo("[20/88] Get File List Test (max size test)");
            try
            {
                var files = api.GetFileList("/pcsapi_testbench", count: 2); //meta: { count=2 }
            }
            catch { }

            trace.TraceInfo("[21/88] Get File List Test (page test)");
            try
            {
                var files = api.GetFileList("/pcsapi_testbench", page: 2, count: 2); //meta: { count=2 }
            }
            catch { }

            trace.TraceInfo("[22/88] File Diff Test (null cursor)");
            string next_cursor = null;
            bool noused1, noused2;
            try
            {
                var files = api.GetFileDiff(out next_cursor, out noused1, out noused2); //meta: { count=399 }
            }
            catch { }

            trace.TraceInfo("[23/88] File Diff Test (general cursor)");
            try
            {
                var files = api.GetFileDiff(out next_cursor, out noused1, out noused2, next_cursor); //meta: { count=400 }
            }
            catch { }

            trace.TraceInfo("[24/88] File Diff Test (invalid cursor)");
            try
            {
                var files = api.GetFileDiff(out next_cursor, out noused1, out noused2, "haha"); //errno 2
            }
            catch { }

            trace.TraceInfo("[25/88] Pre Create File Test (general test)");
            string uploadid = null;
            try
            {
                uploadid = api.PreCreateFile("/pcsapi_testbench/slice_upload.dat", 2).UploadId; //P1-XXXXX
            }
            catch { }

            trace.TraceInfo("[26/88] Pre Create File Test (invalid path test)");
            try
            {
                api.PreCreateFile("/pcsapi_testbench/slice:invalid path", 1); //errno -7
            }
            catch { }

            trace.TraceInfo("[27/88] Pre Create File Test (invalid block size)");
            try
            {
                api.PreCreateFile("/pcsapi_testbench/slice_invalid.dat", 0); //errno 2
            }
            catch { }

            trace.TraceInfo("[28/88] Slice Upload Test (general test)");
            data = new byte[BaiduPCS.UPLOAD_SLICE_SIZE + 1];
            var ms3 = new MemoryStream(data);
            rnd.NextBytes(data);
            string slice1 = null, slice2 = null;
            try
            {
                slice1 = api.UploadSliceRaw(ms3, "/pcsapi_testbench/tmp", uploadid, 0, (a, b, c, d) => { }); //file md5
                slice2 = api.UploadSliceRaw(ms3, "/pcsapi_testbench/tmp2", uploadid, 1, (a, b, c, d) => { }); //file md5
            }
            catch { }

            trace.TraceInfo("[29/88] Slice Upload Test (invalid path test)");
            ms.Seek(-1, SeekOrigin.End);
            var id = api.PreCreateFile("/pcsapi_testbench/test_slice_upload.dat", 1);
            try
            {
                api.UploadSliceRaw(ms, "/pcsapi_testbench/:XD", id.UploadId, 1, (a, b, c, d) => { }); //file md5
            }
            catch { }

            trace.TraceInfo("[30/88] Slice Upload Test (invalid uploadid test)");
            ms.Seek(-1, SeekOrigin.End);
            try
            {
                api.UploadSliceRaw(ms, "/pcsapi_testbench/test_slice_upload2.dat", "haha", 1, (a, b, c, d) => { }); //error_code 31299 : Invalid param poms key
            }
            catch { }

            trace.TraceInfo("[31/88] Upload Test (invalid input stream test)");
            try
            {
                var s = new MemoryStream(new byte[] { 1 });
                s.Close();
                s.Dispose();
                api.UploadRaw(s, 1, "/pcsapi_testbench/new_upload.dat"); //empty meta
            }
            catch { }

            trace.TraceInfo("[32/88] Slice Upload Test (invalid input stream test)");
            try
            {
                var s = new MemoryStream(new byte[] { 2 });
                s.Close();
                s.Dispose();
                api.UploadSliceRaw(s, "/pcsapi_testbench/new_slice_upload._dat", id.UploadId, 1, (a, b, c, d) => { }); //ObjectDisposedException
            }
            catch { }

            trace.TraceInfo("[33/88] Slice Upload Test (seq out of range test)");
            ms.Seek(-1, SeekOrigin.End);
            try
            {
                api.UploadSliceRaw(ms, "/pcsapi_testbench/test_slice_upload3.dat", id.UploadId, 2, (a, b, c, d) => { }); //file md5
            }
            catch { }

            trace.TraceInfo("[34/88] Quota Test");
            try
            {
                api.GetQuota(); //Quota
            }
            catch { }

            trace.TraceInfo("[35/88] Locate Download Test (general test)");
            try
            {
                api.GetLocateDownloadLink("/pcsapi_testbench/5mb_file.dat"); //string[5]
            }
            catch { }

            trace.TraceInfo("[36/88] Locate Download Test (non existing path)");
            try
            {
                api.GetLocateDownloadLink("/pcsapi_testbench/nothing"); //error_code 31066 : file does not exist
            }
            catch { }

            trace.TraceInfo("[37/88] Locate Download Test (invalid path)");
            try
            {
                api.GetLocateDownloadLink("/pcsapi_testbench/:data"); //error_code 31066 : file does not exist
            }
            catch { }

            trace.TraceInfo("[38/88] Locate Download Test (empty path)");
            try
            {
                api.GetLocateDownloadLink(""); //ArgumentNullException
            }
            catch { }

            trace.TraceInfo("[39/88] PCS Download Test (general test, https)");
            try
            {
                api.GetDownloadLink(filedata.FS_ID); //https url
            }
            catch { }

            trace.TraceInfo("[40/88] PCS Download Test (general test, http)");
            try
            {
                api.GetDownloadLink(filedata.FS_ID, false); //http url
            }
            catch { }

            trace.TraceInfo("[41/88] PCS Download Test (invalid fs_id test)");
            try
            {
                api.GetDownloadLink(filedata.FS_ID + (ulong)rnd.Next(500)); //empty string
            }
            catch { }

            trace.TraceInfo("[42/88] PCS Download Test (zero fs_id test)");
            try
            {
                api.GetDownloadLink(0); //ArgumentNullException
            }
            catch { }

            trace.TraceInfo("[43/88] Rapid Upload Test (general test)");
            try
            {
                api.RapidUploadRaw("/pcsapi_testbench/rapid_upload_test.dat", (ulong)ms2.Length, content_md5, content_crc32, slice_md5); //meta: /pcsapi_testbench/rapid_upload_test.dat    
            }
            catch { }

            trace.TraceInfo("[44/88] Rapid Upload Test (invalid slice md5)");
            try
            {
                api.RapidUploadRaw("/pcsapi_testbench/rapid_upload_test2.dat", (ulong)ms2.Length, content_md5, content_crc32, "abababababababababababababababab"); //meta: /pcsapi_testbench/rapid_upload_test2.dat
            }
            catch { }

            trace.TraceInfo("[45/88] Rapid Upload Test (empty slice md5)");
            try
            {
                api.RapidUploadRaw("/pcsapi_testbench/rapid_upload_test2.dat", (ulong)ms2.Length, content_md5, content_crc32, ""); //error_code 31023 : param error
            }
            catch { }

            trace.TraceInfo("[46/88] Rapid Upload Test (invalid md5)");
            try
            {
                api.RapidUploadRaw("/pcsapi_testbench/rapid_upload_test3.dat", (ulong)ms2.Length, "abababababababababababababababab", content_crc32, slice_md5); //error_code 31079 : file md5 not found, you should use upload api to upload the whole file
            }
            catch { }

            trace.TraceInfo("[47/88] Rapid Upload Test (empty md5)");
            try
            {
                api.RapidUploadRaw("/pcsapi_testbench/rapid_upload_test3.dat", (ulong)ms2.Length, "", content_crc32, slice_md5); //error_code 31023 : param error
            }
            catch { }

            trace.TraceInfo("[48/88] Rapid Upload Test (invalid crc32)");
            try
            {
                api.RapidUploadRaw("/pcsapi_testbench/rapid_upload_test4.dat", (ulong)ms2.Length, content_md5, "abcdef89", slice_md5); //meta: /pcsapi_testbench/rapid_upload_test4.dat
            }
            catch { }

            trace.TraceInfo("[49/88] Rapid Upload Test (empty crc32)");
            try
            {
                api.RapidUploadRaw("/pcsapi_testbench/rapid_upload_test4.dat", (ulong)ms2.Length, content_md5, "", slice_md5); //meta: /pcsapi_testbench/rapid_upload_test4.dat
            }
            catch { }

            trace.TraceInfo("[50/88] Rapid Upload Test (invalid length)");
            try
            {
                api.RapidUploadRaw("/pcsapi_testbench/rapid_upload_test5.dat", 0, content_md5, content_crc32, slice_md5); //meta: /pcsapi_testbench/rapid_upload_test4.dat (with size 0)
            }
            catch { }

            trace.TraceInfo("[51/88] Rapid Upload Test (overwrite test)");
            try
            {
                api.RapidUploadRaw("/pcsapi_testbench/100b_file.dat", (ulong)ms2.Length, content_md5, content_crc32, slice_md5, BaiduPCS.ondup.overwrite);//meta: /pcsapi_testbench/100b_file.dat
            }
            catch { }

            trace.TraceInfo("[52/88] Rapid Upload Test (newcopy test)");
            try
            {
                api.RapidUploadRaw("/pcsapi_testbench/rapid_upload_test.dat", (ulong)ms2.Length, content_md5, content_crc32, slice_md5, BaiduPCS.ondup.newcopy); //meta: /pcsapi_testbench/rapid_upload_test_yyyyMMddHHmmss.dat
            }
            catch { }

            trace.TraceInfo("[53/88] Rapid Upload Test (invalid path)");
            try
            {
                api.RapidUploadRaw("/pcsapi_testbench/rapid_:nodata", (ulong)ms2.Length, content_md5, content_crc32, slice_md5); //error_code 31062 : file name is invalid
            }
            catch { }

            trace.TraceInfo("[54/88] Copy Test (general test, single file)");
            try
            {
                api.CopyPath("/pcsapi_testbench/100b_file.dat", "/pcsapi_testbench/100b_file.dat2"); //true
            }
            catch { }

            trace.TraceInfo("[55/88] Copy Test (general test, single directory)");
            try
            {
                api.CopyPath("/pcsapi_testbench/conflict_test", "/pcsapi_testbench/new_dir"); //true
            }
            catch { }

            trace.TraceInfo("[56/88] Copy Test (general test, mixed multi data)");
            try
            {
                api.CopyPath(new string[] { "/pcsapi_testbench/100b_file.dat2", "/pcsapi_testbench/new_dir" }, new string[] { "/pcsapi_testbench/new_file.dat", "/pcsapi_testbench/new_dir2" }); //true
            }
            catch { }

            trace.TraceInfo("[57/88] Copy Test (invalid src path test)");
            try
            {
                api.CopyPath("/pcsapi_testbench/haha:D", "/pcsapi_testbench/test_copy_invalid.dat"); //errno 12
            }
            catch { }

            trace.TraceInfo("[58/88] Copy Test (invalid dst path test)");
            try
            {
                api.CopyPath("/pcsapi_testbench/new_file.dat", "/pcsapi_testbench/new_dir/haha:D"); //errno 12
            }
            catch { }

            trace.TraceInfo("[59/88] Copy Test (overwriting same type)");
            try
            {
                api.CopyPath("/pcsapi_testbench/100b_file.dat", "/pcsapi_testbench/the_new_file.dat"); //true
                api.CopyPath("/pcsapi_testbench/new_file.dat", "/pcsapi_testbench/the_new_file.dat"); //errno 12
            }
            catch { }

            trace.TraceInfo("[60/88] Copy Test (overwriting diff type)");
            try
            {
                api.CopyPath("/pcsapi_testbench/new_file.dat", "/pcsapi_testbench/new_dir"); //errno 12
            }
            catch { }

            trace.TraceInfo("[61/88] Copy Test (newcopying same type)");
            try
            {
                api.CopyPath("/pcsapi_testbench/new_file.dat", "/pcsapi_testbench/rapid_upload_test.dat", BaiduPCS.ondup.newcopy); //true : /pcsapi_testbench/rapid_upload_test(1).dat
            }
            catch { }

            trace.TraceInfo("[62/88] Copy Test (newcopying diff type)");
            try
            {
                api.CopyPath("/pcsapi_testbench/new_file.dat", "/pcsapi_testbench/new_dir", BaiduPCS.ondup.newcopy); //true: /pcsapi_testbench/new_dir(1) (file)
            }
            catch { }

            trace.TraceInfo("[63/88] Copy Test (src length differs from dst length)");
            try
            {
                api.CopyPath(new string[] { "/pcsapi_testbench/new_file.dat" }, new string[] { "/pcsapi_testbench/new_file2.dat", "/pcsapi_testbench/new_file3.dat" }); //InvalidOperationException
            }
            catch { }

            trace.TraceInfo("[64/88] Copy Test (src not exist)");
            try
            {
                api.CopyPath("/pcsapi_testbench/new_non_exist.dat", "/pcsapi_testbench/new_non_exist2.dat"); //errno 12
            }
            catch { }

            trace.TraceInfo("[65/88] Move Test (general test, single file)");
            try
            {
                api.MovePath("/pcsapi_testbench/100b_file.dat", "/pcsapi_testbench/move/100b_file.dat2"); //true
            }
            catch { }

            trace.TraceInfo("[66/88] Move Test (general test, single directory)");
            try
            {
                api.MovePath("/pcsapi_testbench/conflict_test", "/pcsapi_testbench/move/new_dir"); //true
            }
            catch { }

            trace.TraceInfo("[67/88] Move Test (general test, mixed multi data)");
            try
            {
                api.MovePath(new string[] { "/pcsapi_testbench/100b_file.dat2", "/pcsapi_testbench/new_dir" }, new string[] { "/pcsapi_testbench/move/new_file.dat", "/pcsapi_testbench/move/new_dir2" }); //true
            }
            catch { }

            trace.TraceInfo("[68/88] Move Test (invalid src path test)");
            try
            {
                api.MovePath("/pcsapi_testbench/haha:D", "/pcsapi_testbench/move/test_copy_invalid.dat"); //errno 12
            }
            catch { }

            trace.TraceInfo("[69/88] Move Test (invalid dst path test)");
            try
            {
                api.MovePath("/pcsapi_testbench/new_file.dat", "/pcsapi_testbench/move/new_dir/haha:D"); //errno 12
            }
            catch { }

            trace.TraceInfo("[70/88] Move Test (overwriting same type)");
            try
            {
                api.MovePath("/pcsapi_testbench/move/new_file.dat", "/pcsapi_testbench/move/rapid_upload_test.dat"); //true
            }
            catch { }

            trace.TraceInfo("[71/88] Move Test (overwriting diff type)");
            try
            {
                api.MovePath("/pcsapi_testbench/move/new_file.dat", "/pcsapi_testbench/move/new_dir"); //errno 12
            }
            catch { }

            trace.TraceInfo("[72/88] Move Test (newcopying same type)");
            try
            {
                api.CopyPath("/pcsapi_testbench/move/rapid_upload_test.dat", "/pcsapi_testbench/move/new_file.dat", BaiduPCS.ondup.newcopy); //true
                api.MovePath("/pcsapi_testbench/move/rapid_upload_test.dat", "/pcsapi_testbench/move/new_file.dat", BaiduPCS.ondup.newcopy); //true: /pcsapi_testbench/move/new_file(1).dat
            }
            catch { }

            trace.TraceInfo("[73/88] Move Test (newcopying diff type)");
            try
            {
                api.MovePath("/pcsapi_testbench/move/new_file.dat", "/pcsapi_testbench/move/new_dir", BaiduPCS.ondup.newcopy); //true: /pcsapi_testbench/move/new_dir(1) (file)
            }
            catch { }

            trace.TraceInfo("[74/88] Move Test (src length differs from dst length)");
            try
            {
                api.MovePath(new string[] { "/pcsapi_testbench/move/new_file.dat" }, new string[] { "/pcsapi_testbench/move/new_file2.dat", "/pcsapi_testbench/move/new_file3.dat" }); //InvalidOperationException
            }
            catch { }

            trace.TraceInfo("[75/88] Move Test (src not exist)");
            try
            {
                api.MovePath("/pcsapi_testbench/move/new_non_exist.dat", "/pcsapi_testbench/move/new_non_exist2.dat"); //errno 12
            }
            catch { }

            trace.TraceInfo("[76/88] Create Super File Test (general test)");
            ms2.Seek(0, SeekOrigin.Begin);
            try
            {
                api.CreateSuperFile("/pcsapi_testbench/super.dat", uploadid, new string[] { slice1, slice2 }, (ulong)ms3.Length); //meta: /pcsapi_testbench/super.dat
            }
            catch { }

            trace.TraceInfo("[77/88] Create Super File Test (invalid upload id)");
            try
            {
                api.CreateSuperFile("/pcsapi_testbench/super2.dat", "qwert", new string[] { slice1, slice2 }, (ulong)ms3.Length); //errno 31353
            }
            catch { }

            trace.TraceInfo("[78/88] Create Super File Test (missing slice)");
            try
            {
                api.CreateSuperFile("/pcsapi_testbench/super3.dat", uploadid, new string[] { slice1 }, (ulong)ms3.Length - 1); //errno 2
            }
            catch { }

            trace.TraceInfo("[79/88] Create Super File Test (invalid slice md5)");
            try
            {
                api.CreateSuperFile("/pcsapi_testbench/super4.dat", uploadid, new string[] { "abababababababababababababababab", slice2 }, (ulong)ms3.Length); //errno 2
            }
            catch { }

            trace.TraceInfo("[80/88] Create Super File Test (invalid file size)");
            try
            {
                api.CreateSuperFile("/pcsapi_testbench/super5.dat", uploadid, new string[] { slice1, slice2 }, (ulong)ms3.Length + 2); //errno 2
            }
            catch { }

            trace.TraceInfo("[81/88] Create Super File Test (invalid path)");
            try
            {
                api.CreateSuperFile("/pcsapi_testbench/:<>haha", uploadid, new string[] { slice1, slice2 }, (ulong)ms3.Length); //errno -7
            }
            catch { }

            trace.TraceInfo("[82/88] Create Super File Test (empty path)");
            try
            {
                api.CreateSuperFile("", uploadid, new string[] { slice1, slice2 }, (ulong)ms3.Length); //ArgumentNullException
            }
            catch { }

            trace.TraceInfo("[83/88] Delete Test (general test, single)");
            try
            {
                api.DeletePath("/pcsapi_testbench/move"); //true
            }
            catch { }

            trace.TraceInfo("[84/88] Delete Test (gemeral test, multi)");
            try
            {
                api.DeletePath(new string[] { "/pcsapi_testbench/super.dat", "/pcsapi_testbench/rapid_upload_test.dat" }); //true
            }
            catch { }

            trace.TraceInfo("[85/88] Delete Test (path invalid)");
            try
            {
                api.DeletePath("/pcsapi_testbench/haha:D"); //errno 12
            }
            catch { }

            trace.TraceInfo("[86/88] Delete Test (non existing path)");
            try
            {
                api.DeletePath("/pcsapi_testbench/helloworld"); //true
            }
            catch { }

            trace.TraceInfo("[87/88] Delete Test (empty path)");
            try
            {
                api.DeletePath(""); //ArgumentNullException
            }
            catch { }

            trace.TraceInfo("[88/88] Delete Test (half non exist)");
            try
            {
                api.DeletePath(new string[] { "/pcsapi_testbench/5mb_file.dat", "/pcsapi_testbench/non_existing" }); //errno 12
            }
            catch { }

            trace.TraceInfo("Test finished, deleting temporary remote directory");
            try
            {
                api.DeletePath("/pcsapi_testbench");
            }
            catch { }
        }
    }
}
