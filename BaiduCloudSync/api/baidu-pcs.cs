using BaiduCloudSync.NetUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace BaiduCloudSync
{
    //百度自带的一些api，可能需要调用到oauth的参数
    //todo: 优化授权代码 + 网盘定期刷新模块
    public partial class BaiduPCS
    {


        #region static & const vars
        public static uint APPID = 250528;
        private static Tracer _trace = Tracer.GlobalTracer;
        //https://pcs.baidu.com/
        public const string PCS_HOST = "https://pcs.baidu.com/";
        //https://pcs.baidu.com/rest/2.0/pcs/
        public const string PCS_ROOT_URL = PCS_HOST + "rest/2.0/pcs/";
        //https://pcs.baidu.com/rest/2.0/pcs/file
        public const string PCS_FILE_URL = PCS_ROOT_URL + "file";
        //https://pcs.baidu.com/rest/2.0/pcs/superfile2
        public const string PCS_SUPERFILE_URL = PCS_ROOT_URL + "superfile2";

        //http://pan.baidu.com/
        public const string API_HOST = "http://pan.baidu.com/";
        //http://pan.baidu.com/api/
        public const string API_ROOT_URL = API_HOST + "api/";
        //http://pan.baidu.com/api/list
        public const string API_LIST_URL = API_ROOT_URL + "list";
        //http://pan.baidu.com/api/quota
        public const string API_QUOTA_URL = API_ROOT_URL + "quota";
        //http://pan.baidu.com/api/filemanager
        public const string API_FILEMANAGER_URL = API_ROOT_URL + "filemanager";
        //http://pan.baidu.com/api/download
        public const string API_DOWNLOAD_URL = API_ROOT_URL + "download";
        //http://pan.baidu.com/api/create
        public const string API_CREATE_URL = API_ROOT_URL + "create";
        //http://pan.baidu.com/api/precreate
        public const string API_PRECREATE_URL = API_ROOT_URL + "precreate";
        //http://pan.baidu.com/share/
        public const string API_SHARE_URL = API_HOST + "share/";
        //http://pan.baidu.com/share/set
        public const string API_SHARE_SET_URL = API_SHARE_URL + "set";
        //http://pan.baidu.com/share/cancel
        public const string API_SHARE_CANCEL_URL = API_SHARE_URL + "cancel";
        //http://pan.baidu.com/share/record
        public const string API_SHARE_RECORD_URL = API_SHARE_URL + "record";

        //http://pan.baidu.com/disk/home
        public const string BAIDU_NETDISK_URL = "http://pan.baidu.com/disk/home";

        //默认数据流缓存区大小
        private const int BUFFER_SIZE = 2048;
        //文件验证段：前256KB字节
        private const long VALIDATE_SIZE = 262144;
        //默认上传分段的大小
        private const int UPLOAD_SLICE_SIZE = 4194304;
        #endregion


        #region Login Data
        private Thread __next_update_thread;
        //登陆的一些参数，由抓包得来
        private void _init_login_data()
        {
            _trace.TraceInfo("BaiduPCS._init_login_data called: void");
            const string pan_root_url = "http://pan.baidu.com/disk/home";
            try
            {
                var ns = new NetStream();
                ns.CookieKey = _auth.CookieIdentifier;
                ns.HttpGet(pan_root_url);

                var str = ns.ReadResponseString();
                ns.Close();

                //_trace.TraceInfo(str);

                var match = Regex.Match(str, "\"bdstoken\":\"(\\w+)\"");
                if (match.Success) __bdstoken = match.Result("$1");
                match = Regex.Match(str, "\"sign1\":\"(\\w+)\"");
                if (match.Success) __sign1 = match.Result("$1");
                match = Regex.Match(str, "\"sign3\":\"(\\w+)\"");
                if (match.Success) __sign3 = match.Result("$1");
                match = Regex.Match(str, "\"timestamp\":(\\d+)");
                if (match.Success) __timestamp = match.Result("$1");

                //calculate for sign2
                var j = Encoding.UTF8.GetBytes(__sign3);
                var r = Encoding.UTF8.GetBytes(__sign1);
                byte[] a = new byte[256], p = new byte[256];
                var o = new byte[r.Length];
                int v = j.Length;
                for (int q = 0; q < 256; q++)
                {
                    a[q] = j[q % v];
                    p[q] = (byte)q;
                }
                int u = 0;
                for (int q = 0; q < 256; q++)
                {
                    u = (u + p[q] + a[q]) % 256;
                    byte t = p[q];
                    p[q] = p[u];
                    p[u] = t;
                }
                int i = 0;
                u = 0;
                for (int q = 0; q < r.Length; q++)
                {
                    i = (i + 1) % 256;
                    u = (u + p[i]) % 256;
                    byte t = p[i];
                    p[i] = p[u];
                    p[u] = t;
                    byte k = p[(p[i] + p[u]) % 256];
                    o[q] = (byte)(r[q] ^ k);
                }
                __sign2 = Convert.ToBase64String(o);

                _trace.TraceInfo("Initialization complete.\r\nbdstoken=" + __bdstoken + "\r\nsign1=" + __sign1 + "\r\nsign2=" + __sign2 + "\r\nsign3=" + __sign3 + "\r\ntimestamp=" + __timestamp);
                //test
                TestFunc();

                //next update thread
                if (__next_update_thread != null)
                {
                    try { var thd = __next_update_thread; __next_update_thread = null; ThreadPool.QueueUserWorkItem(delegate { thd.Abort(); }); } catch { }
                }
                __next_update_thread = new Thread(() =>
                {
                    var ts = TimeSpan.FromHours(1);
                    Thread.Sleep(ts);
                    _init_login_data();
                    __next_update_thread = null;
                });
                __next_update_thread.IsBackground = true;
                __next_update_thread.Name = "网盘登陆数据刷新线程";
                __next_update_thread.Start();
            }
            catch (ThreadAbortException) { }
            catch (Exception ex)
            {
                _trace.TraceError(ex.ToString());
                //next update thread (exception raised mode)
                if (__next_update_thread != null)
                {
                    try { var thd = __next_update_thread; __next_update_thread = null; ThreadPool.QueueUserWorkItem(delegate { thd.Abort(); }); } catch { }
                }
                __next_update_thread = new Thread(() =>
                {
                    var ts = TimeSpan.FromSeconds(15);
                    Thread.Sleep(ts);
                    _init_login_data();
                    __next_update_thread = null;
                });
                __next_update_thread.IsBackground = true;
                __next_update_thread.Name = "网盘登陆数据刷新线程";
                __next_update_thread.Start();
                __next_update_thread.Join();
            }
        }
        private string __bdstoken, __sign1, __sign2, __sign3, __timestamp;
        private string _bdstoken
        {
            get
            {
                if (string.IsNullOrEmpty(__bdstoken))
                    _init_login_data();
                return __bdstoken;
            }
        }
        private string _sign1
        {
            get
            {
                if (string.IsNullOrEmpty(__sign1))
                    _init_login_data();
                return __sign1;
            }
        }
        private string _sign2
        {
            get
            {
                if (string.IsNullOrEmpty(__sign2))
                    _init_login_data();
                return __sign2;
            }
        }
        private string _sign3
        {
            get
            {
                if (string.IsNullOrEmpty(__sign3))
                    _init_login_data();
                return __sign3;
            }
        }
        private string _timestamp
        {
            get
            {
                if (string.IsNullOrEmpty(__timestamp))
                    _init_login_data();
                return __timestamp;
            }
        }

        private BaiduOAuth _auth;
        public BaiduPCS(BaiduOAuth auth)
        {
            _auth = auth;
            if (auth == null) throw new ArgumentNullException("auth");
        }
        #endregion


        #region public structures
        /// <summary>
        /// 文件顺序
        /// </summary>
        public enum FileOrder
        {
            time, name, size
        }
        /// <summary>
        /// 网盘数据
        /// </summary>
        public struct ObjectMetadata
        {
            //D F | D=Directory only, F=File only
            //+ + 文件分类，默认为 1视频 2音乐 3图片 4文档 5应用 6其他 7种子，1和3有缩略图(thumbs)
            /// <summary>
            /// 文件分类，默认为 1视频 2音乐 3图片 4文档 5应用 6其他 7种子，1和3有缩略图(thumbs)
            /// </summary>
            public uint Category;
            //+ + 文件唯一标识符
            /// <summary>
            /// 文件唯一标识符
            /// </summary>
            public ulong FS_ID;
            //+ + 是否为文件夹
            /// <summary>
            /// 是否为文件夹
            /// </summary>
            public bool IsDir;
            //+ + 是否为文件夹
            /// <summary>
            /// 是否为文件夹
            /// </summary>
            public ulong LocalCTime;
            //+ + 本地修改时间
            /// <summary>
            /// 本地修改时间
            /// </summary>
            public ulong LocalMTime;
            //+ + unknown property
            /// <summary>
            /// unknown property
            /// </summary>
            public uint OperID;
            //+ + 文件的完整路径
            /// <summary>
            /// 文件的完整路径
            /// </summary>
            public string Path;
            //+ + 服务器创建时间
            /// <summary>
            /// 服务器创建时间
            /// </summary>
            public ulong ServerCTime;
            //+ + 文件名称(不含路径)
            /// <summary>
            /// 文件名称(不含路径)
            /// </summary>
            public string ServerFileName;
            //+ + 服务器修改时间
            /// <summary>
            /// 服务器修改时间
            /// </summary>
            public ulong ServerMTime;
            //+ + 文件大小(文件夹大小为0)
            /// <summary>
            /// 文件大小(文件夹大小为0)
            /// </summary>
            public ulong Size;
            //+ + unknown property
            /// <summary>
            /// unknown property
            /// </summary>
            public uint Unlist;
            //- + 文件的MD5
            /// <summary>
            /// 文件的MD5
            /// </summary>
            public string MD5;
            public override string ToString()
            {
                return Path;
            }
        }
        /// <summary>
        /// 同名覆盖方式
        /// </summary>
        public enum ondup
        {
            newcopy,
            overwrite
        }
        /// <summary>
        /// 网盘配额
        /// </summary>
        public struct Quota
        {
            public ulong InUsed;
            public ulong Total;
            public override string ToString()
            {
                return "used: " + InUsed + "B(" + util.FormatBytes(InUsed) + "), total: " + Total + "B(" + util.FormatBytes(Total) + ") (" + (InUsed * 100.0 / Total).ToString("0.000") + "% used)";
            }
        }

        public struct PreCreateResult
        {
            public int BlockCount;
            public int ReturnType;
            public string UploadId;
        }
        #endregion


        #region Util functions
        /// <summary>
        /// 获取当前时间的Base64加密字符串 (logid)
        /// </summary>
        /// <returns>当前时间的Base64加密字符串</returns>
        private string _get_logid()
        {
            var str = (util.ToUnixTimestamp(DateTime.Now) * 10000).ToString();
            if (!str.Contains(".")) str += ".0";
            var rnd = new Random();
            str = str.Substring(0, str.IndexOf('.'));
            str += "." + rnd.Next().ToString();
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(str));
        }
        /// <summary>
        /// 检查json的错误码并自动抛出异常
        /// </summary>
        /// <param name="obj">json对象</param>
        /// <remarks>throwable</remarks>
        private void _check_error(JObject obj)
        {
            int errno = obj.Value<int>("errno");
            if (errno == 0) return;
            JToken msg;
            if (obj.TryGetValue("msg", out msg))
            {
                var strmsg = msg.Value<string>();
                _trace.TraceError("error " + errno + ", msg=" + strmsg);
            }
            else
            {
                _trace.TraceError("error " + errno);
            }
            //throw new Exception("errno is not zero (" + errno + ")");
            throw new ErrnoException(errno, "errno is not zero (" + errno + ")");
        }
        /// <summary>
        /// 从json数据中读取到结构中
        /// </summary>
        /// <param name="obj">json对象</param>
        /// <returns>等效的数据</returns>
        private ObjectMetadata _read_json_meta(JObject obj)
        {
            var ret = new ObjectMetadata();
            ret.Category = obj.Value<uint>("category");
            ret.FS_ID = obj.Value<ulong>("fs_id");
            ret.IsDir = obj.Value<int>("isdir") != 0;
            ret.LocalCTime = obj.Value<uint>("local_ctime");
            ret.LocalMTime = obj.Value<uint>("local_mtime");
            ret.OperID = obj.Value<uint>("oper_id");
            ret.Path = obj.Value<string>("path");
            ret.ServerCTime = obj.Value<uint>("server_ctime");
            ret.ServerFileName = obj.Value<string>("server_filename");
            ret.ServerMTime = obj.Value<ulong>("server_mtime");
            ret.Size = obj.Value<ulong>("size");
            ret.Unlist = obj.Value<uint>("unlist");

            if (!ret.IsDir)
            {
                ret.MD5 = obj.Value<string>("md5");
            }
            else
                ret.MD5 = string.Empty;

            return ret;
        }
        /// <summary>
        /// 返回带有xml http request的参数
        /// </summary>
        /// <returns></returns>
        private Parameters _get_xhr_param()
        {
            var ret = new Parameters();

            ret.Add("X-Requested-With", "XMLHttpRequest");
            ret.Add("Origin", "http://pan.baidu.com");
            ret.Add("Referer", BAIDU_NETDISK_URL);

            return ret;
        }
        #endregion

        /// <summary>
        /// 获取网盘大小，失败时返回0B used/0B total
        /// </summary>
        /// <returns>网盘大小</returns>
        /// <remarks></remarks>
        public Quota GetQuota()
        {
            _trace.TraceInfo("BaiduPCS.GetQuota called: void");
            var sync_thread = Thread.CurrentThread;
            Quota ret = new Quota();
            GetQuotaAsync((suc, data) =>
            {
                ret = data;
                sync_thread.Interrupt();
            });
            try
            {
                Thread.Sleep(Timeout.Infinite);
            }
            catch (ThreadInterruptedException) { }
            catch (Exception ex) { throw ex; }
            return ret;
        }


        #region Directory Operation
        /// <summary>
        /// 删除单个文件夹/文件
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns></returns>
        public bool DeletePath(string path)
        {
            _trace.TraceInfo("BaiduPCS.DeletePath called: string path=" + path);
            return DeletePath(new string[] { path });
        }
        /// <summary>
        /// 删除多个文件夹/文件
        /// </summary>
        /// <param name="paths">文件路径</param>
        /// <returns></returns>
        public bool DeletePath(IEnumerable<string> paths)
        {
            _trace.TraceInfo("BaiduPCS.DeletePath called: IEnumerable<string> paths=[count=" + paths.Count() + "]");
            var sync_thread = Thread.CurrentThread;
            bool ret = false;
            DeletePathAsync(paths, (suc, data) =>
            {
                ret = data;
                sync_thread.Interrupt();
            });
            try
            {
                Thread.Sleep(Timeout.Infinite);
            }
            catch (ThreadInterruptedException) { }
            catch (Exception ex) { throw ex; }
            return ret;
        }
        /// <summary>
        /// 移动单个文件
        /// </summary>
        /// <param name="source">原文件路径</param>
        /// <param name="destination">目标文件路径</param>
        /// <param name="ondup">同名覆盖方式</param>
        /// <returns>返回是否成功</returns>
        public bool MovePath(string source, string destination, ondup ondup = ondup.overwrite)
        {
            _trace.TraceInfo("BaiduPCS.MovePath called: string source=" + source + ", string destination=" + destination + ", ondup ondup=" + ondup);
            return MovePath(new string[] { source }, new string[] { destination }, ondup);
        }
        /// <summary>
        /// 移动多个文件
        /// </summary>
        /// <param name="source">原文件路径</param>
        /// <param name="destination">目标文件路径</param>
        /// <param name="ondup">同名覆盖方式</param>
        /// <returns>返回是否成功</returns>
        public bool MovePath(IEnumerable<string> source, IEnumerable<string> destination, ondup ondup = ondup.overwrite)
        {
            _trace.TraceInfo("BaiduPCS.MovePath called: IEnumerable<string> source=[count=" + source.Count() + "], IEnumerable<string> destination=[count=" + destination.Count() + "], ondup ondup=" + ondup);
            var sync_thread = Thread.CurrentThread;
            bool ret = false;
            MovePathAsync(source, destination, (suc, data) =>
            {
                ret = data;
                sync_thread.Interrupt();
            }, ondup);
            try
            {
                Thread.Sleep(Timeout.Infinite);
            }
            catch (ThreadInterruptedException) { }
            catch (Exception ex) { throw ex; }
            return ret;
        }

        /// <summary>
        /// 复制单个文件
        /// </summary>
        /// <param name="source">原文件路径</param>
        /// <param name="destination">目标文件路径</param>
        /// <param name="ondup">同名覆盖方式</param>
        /// <returns>返回是否成功</returns>
        public bool CopyPath(string source, string destination, ondup ondup = ondup.overwrite)
        {
            _trace.TraceInfo("BaiduPCS.CopyPath called: string source=" + source + ", string destination=" + destination + ", ondup ondup=" + ondup);
            return CopyPath(new string[] { source }, new string[] { destination }, ondup);
        }
        /// <summary>
        /// 复制多个文件
        /// </summary>
        /// <param name="source">原文件路径</param>
        /// <param name="destination">目标文件路径</param>
        /// <param name="ondup">同名覆盖方式</param>
        /// <returns>返回是否成功</returns>
        public bool CopyPath(IEnumerable<string> source, IEnumerable<string> destination, ondup ondup = ondup.overwrite)
        {
            _trace.TraceInfo("BaiduPCS.CopyPath called: IEnumerable<string> source=[count=" + source.Count() + "], IEnumerable<string> destination=[count=" + destination.Count() + "], ondup ondup=" + ondup);
            var sync_thread = Thread.CurrentThread;
            bool ret = false;
            CopyPathAsync(source, destination, (suc, data) =>
            {
                ret = data;
                sync_thread.Interrupt();
            }, ondup);
            try
            {
                Thread.Sleep(Timeout.Infinite);
            }
            catch (ThreadInterruptedException) { }
            catch (Exception ex) { throw ex; }
            return ret;
        }
        /// <summary>
        /// 重命名单个文件
        /// </summary>
        /// <param name="source">原文件路径</param>
        /// <param name="new_name">新文件名</param>
        /// <returns>返回是否成功</returns>
        public bool Rename(string source, string new_name)
        {
            _trace.TraceInfo("BaiduPCS.Rename called: string source=" + source + ", string new_name=" + new_name);
            return Rename(new string[] { source }, new string[] { new_name });
        }
        /// <summary>
        /// 重命名多个文件
        /// </summary>
        /// <param name="source">原文件路径</param>
        /// <param name="new_name">新文件名</param>
        /// <returns>返回是否成功</returns>
        public bool Rename(IEnumerable<string> source, IEnumerable<string> new_name)
        {
            _trace.TraceInfo("BaiduPCS.Rename called: IEnumerable<string> source=[count=" + source.Count() + "], IEnumerable<string> new_name=[count=" + new_name.Count() + "]");
            var sync_thread = Thread.CurrentThread;
            bool ret = false;
            RenameAsync(source, new_name, (suc, data) =>
            {
                ret = data;
                sync_thread.Interrupt();
            });
            try
            {
                Thread.Sleep(Timeout.Infinite);
            }
            catch (ThreadInterruptedException) { }
            catch (Exception ex) { throw ex; }
            return ret;
        }
        /// <summary>
        /// 新建文件夹
        /// </summary>
        /// <param name="path">网盘路径</param>
        /// <returns>文件夹信息，失败时返回的fs_id=0</returns>
        public ObjectMetadata CreateDirectory(string path)
        {
            _trace.TraceInfo("BaiduPCS.CreateDirectory called: string path=" + path);
            var sync_thread = Thread.CurrentThread;
            ObjectMetadata ret = new ObjectMetadata();
            CreateDirectoryAsync(path, (suc, data) =>
            {
                ret = data;
                sync_thread.Interrupt();
            });
            try
            {
                Thread.Sleep(Timeout.Infinite);
            }
            catch (ThreadInterruptedException) { }
            catch (Exception ex) { throw ex; }
            return ret;
        }

        /// <summary>
        /// 获取指定文件夹的所有文件/文件夹，失败时返回null
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="order">排序依据</param>
        /// <param name="asc">排序顺序</param>
        /// <param name="page">页数</param>
        /// <param name="count">显示数量</param>
        /// <returns></returns>
        public ObjectMetadata[] GetFileList(string path, FileOrder order = FileOrder.name, bool asc = true, int page = 1, int count = 1000)
        {
            _trace.TraceInfo("BaiduPCS.GetFileList called: string path=" + path + ", FileOrder order=" + order + ", bool asc=" + asc + ", int page=" + page + ", int count=" + count);
            ObjectMetadata[] ret = null;
            var sync_thread = Thread.CurrentThread;
            GetFileListAsync(path, (suc, data) =>
            {
                ret = data;
                sync_thread.Interrupt();
            }, order, asc, page, count);
            try
            {
                Thread.Sleep(Timeout.Infinite);
            }
            catch (ThreadInterruptedException) { }
            catch (Exception ex) { throw ex; }
            return ret;
        }
        #endregion


        #region Upload
        public delegate void UploadStatusCallback(string path, string local_path, long current, long length);
        public struct RapidUploadInterface
        {
            public ulong content_length;
            public string content_md5;
            public string content_crc32;
            public string slice_md5;
        }
        /// <summary>
        /// 从本地文件中获取秒传文件的参数
        /// </summary>
        /// <param name="local_path">文件路径</param>
        /// <param name="callback">读取进度回调函数</param>
        /// <returns></returns>
        public RapidUploadInterface GetRapidUploadArguments(string local_path, UploadStatusCallback callback = null)
        {
            _trace.TraceInfo("BaiduPCS.GetRapidUploadArguments called: string local_path=" + local_path + ", UploadStatusCallback callback=" + callback?.ToString());
            var ret = new RapidUploadInterface();

            var file_info = new FileInfo(local_path);
            if (!file_info.Exists)
            {
                //throw new FileNotFoundException("无法找到文件 " + local_path);
                return ret;
            }

            var content_length = file_info.Length;
            var stream_in = file_info.OpenRead();

            var crc_calc = new Crc32();
            var md5_calc = new System.Security.Cryptography.MD5CryptoServiceProvider();
            var slice_md5_calc = new System.Security.Cryptography.MD5CryptoServiceProvider();

            long readed_bytes = 0;
            int cur_read = 0;
            var buffer = new byte[BUFFER_SIZE];
            var temp_buffer = new byte[BUFFER_SIZE];

            do
            {
                cur_read = stream_in.Read(buffer, 0, BUFFER_SIZE);

                crc_calc.Append(buffer, 0, cur_read);
                if (readed_bytes + cur_read <= VALIDATE_SIZE)
                {
                    slice_md5_calc.TransformBlock(buffer, 0, cur_read, temp_buffer, 0);
                }
                else if (readed_bytes <= VALIDATE_SIZE)
                {
                    slice_md5_calc.TransformBlock(buffer, 0, (int)(VALIDATE_SIZE - readed_bytes), temp_buffer, 0);
                }
                md5_calc.TransformBlock(buffer, 0, cur_read, temp_buffer, 0);

                readed_bytes += cur_read;
                callback?.Invoke(string.Empty, local_path, readed_bytes, content_length);
            } while (cur_read > 0);

            stream_in.Close();
            stream_in.Dispose();
            md5_calc.TransformFinalBlock(buffer, 0, 0);
            slice_md5_calc.TransformFinalBlock(buffer, 0, 0);

            var content_md5 = util.Hex(md5_calc.Hash);
            var content_crc = crc_calc.GetCrc32().ToString("X2").ToLower();
            var slice_md5 = util.Hex(slice_md5_calc.Hash);

            ret.content_crc32 = content_crc;
            ret.content_length = (ulong)content_length;
            ret.content_md5 = content_md5;
            ret.slice_md5 = slice_md5;

            if (content_length < VALIDATE_SIZE)
            {
                _trace.TraceWarning("File size too small, could not use RapidUpload method");
                ret.slice_md5 = string.Empty;
            }
            return ret;
        }
        /// <summary>
        /// 秒传文件，使用本地路径计算，失败时返回fs_id为0，MD5为null，没有找到文件时MD5="404"
        /// </summary>
        /// <param name="path">网盘的文件路径</param>
        /// <param name="local_path">本地文件的路径</param>
        /// <param name="ondup">同名覆盖方式</param>
        /// <param name="callback">读取进度回调函数</param>
        /// <returns>成功时返回文件信息，失败时返回fs_id和MD5都为0</returns>
        public ObjectMetadata RapidUpload(string path, string local_path, ondup ondup = ondup.overwrite, UploadStatusCallback callback = null)
        {
            _trace.TraceInfo("BaiduPCS.RapidUpload called: string path=" + path + ", string local_path=" + local_path + ", ondup ondup=" + ondup + ", UploadStatusCallback callback=" + callback?.ToString());
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(local_path))
            {
                //throw new ArgumentNullException("path");
                return new ObjectMetadata();
            }
            var arg = GetRapidUploadArguments(local_path, callback);
            return RapidUploadRaw(path, arg.content_length, arg.content_md5, arg.content_crc32, arg.slice_md5, ondup);
        }
        /// <summary>
        /// 秒传文件，失败时返回fs_id为0，MD5为null，没有找到文件时MD5="404"
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="content_length">文件大小</param>
        /// <param name="content_md5">文件总体MD5</param>
        /// <param name="content_crc">文件总体CRC32 (Hex数值)</param>
        /// <param name="slice_md5">前256K验证段的MD5</param>
        /// <param name="ondup">同名覆盖方式</param>
        /// <returns>返回文件信息</returns>
        public ObjectMetadata RapidUploadRaw(string path, ulong content_length, string content_md5, string content_crc, string slice_md5, ondup ondup = ondup.overwrite)
        {
            _trace.TraceInfo("BaiduPCS.RapidUploadRaw called: string path=" + path + ", ulong content_length=" + content_length + ", string content_md5=" + content_md5 + ", string content_crc=" + content_crc + ", string slice_md5=" + slice_md5 + ", ondup ondup=" + ondup);

            ObjectMetadata ret = new ObjectMetadata();
            var param = new Parameters();

            param.Add("method", "rapidupload");
            param.Add("app_id", APPID);
            param.Add("path", path);
            param.Add("content-length", content_length);
            param.Add("content-md5", content_md5);
            param.Add("slice-md5", slice_md5);
            param.Add("content-crc32", content_crc);
            param.Add("ondup", ondup);

            var ns = new NetStream();
            ns.CookieKey = _auth.CookieIdentifier;
            try
            {
                ns.HttpPost(PCS_FILE_URL, new byte[] { }, "text/html", headerParam: _get_xhr_param(), urlParam: param);

                var response = ns.ReadResponseString();
                ns.Close();

                _trace.TraceInfo(response);

                var json = JsonConvert.DeserializeObject(response) as JObject;
                _check_error(json);

                //todo: parsing json data
                ret.Path = json.Value<string>("path");
                ret.Size = json.Value<ulong>("size");
                ret.ServerCTime = json.Value<ulong>("ctime");
                ret.ServerMTime = json.Value<ulong>("mtime");
                ret.MD5 = json.Value<string>("md5");
                ret.FS_ID = json.Value<ulong>("fs_id");
                ret.IsDir = json.Value<int>("isdir") != 0;
            }
            catch (ErrnoException ex)
            {
                _trace.TraceError(ex.ToString());
                //if (TRANSFER_ERRNO_EXCEPTION)
                //    throw;
            }
            catch (WebException ex)
            {
                if (ex.Response != null)
                {
                    var http_resp = (HttpWebResponse)ex.Response;
                    if (http_resp.StatusCode == HttpStatusCode.NotFound)
                    {
                        ret.MD5 = "404";
                    }
                    else
                        _trace.TraceError(ex.ToString());
                }
            }
            catch (Exception ex)
            {
                _trace.TraceError(ex.ToString());
            }

            return ret;
        }
        /// <summary>
        /// 从输入流中读取数据并上传
        /// </summary>
        /// <param name="stream_in">能读取的输入流</param>
        /// <param name="content_length">文件长度</param>
        /// <param name="path">网盘文件路径</param>
        /// <param name="ondup">同名覆盖方式</param>
        /// <param name="callback">读取进度回调函数</param>
        /// <returns></returns>
        public ObjectMetadata UploadRaw(Stream stream_in, ulong content_length, string path, ondup ondup = ondup.overwrite, UploadStatusCallback callback = null)
        {
            _trace.TraceInfo("BaiduPCS.UploadRaw called: Stream stream_in=" + stream_in.ToString() + ", ulong content_length=" + content_length + ", string path=" + path + ", ondup ondup=" + ondup + ", UploadStatusCallback callback=" + callback?.ToString());

            var ret = new ObjectMetadata();
            if (!stream_in.CanRead || string.IsNullOrEmpty(path)) return ret;
            var param = new Parameters();
            param.Add("method", "upload");
            param.Add("app_id", APPID);
            param.Add("path", path);
            param.Add("ondup", ondup);
            param.Add("logid", _get_logid());
            param.Add("BDUSS", _auth.bduss);

            var ns = new NetStream();
            ns.CookieKey = _auth.CookieIdentifier;
            var boundary = util.GenerateFormDataBoundary();

            var formdata_param = new Parameters();
            formdata_param.Add("Content-Disposition", "form-data; name=\"uploadedfile\"; filename=\"" + path + "\"");
            formdata_param.Add("Content-Type", "application/octet-stream");
            formdata_param.Add("Content-Transfer-Encoding", "binary");

            var head = util.GenerateFormDataObject(boundary, formdata_param);
            var foot = util.GenerateFormDataEnding(boundary);

            var head_bytes = Encoding.UTF8.GetBytes(head);
            var foot_bytes = Encoding.UTF8.GetBytes(foot);

            long total_length = head_bytes.Length + foot_bytes.Length + (long)content_length;

            try
            {
                var stream_out = ns.HttpPost(PCS_FILE_URL, total_length, "multipart/form-data; boundary=" + boundary, headerParam: _get_xhr_param(), urlParam: param);
                stream_out.Write(head_bytes, 0, head_bytes.Length);

                long total_read = 0, current_read = 0;
                var buffer = new byte[BUFFER_SIZE];
                do
                {
                    current_read = stream_in.Read(buffer, 0, BUFFER_SIZE);
                    stream_out.Write(buffer, 0, (int)current_read);
                    total_read += current_read;
                    callback?.Invoke(path, string.Empty, total_read, (long)content_length);
                } while (current_read != 0);

                stream_out.Write(foot_bytes, 0, foot_bytes.Length);

                ns.HttpPostClose();
                var response = ns.ReadResponseString();
                ns.Close();

                var json = JsonConvert.DeserializeObject(response) as JObject;
                _check_error(json);

                ret = _read_json_meta(json);
            }
            catch (ErrnoException ex)
            {
                _trace.TraceError(ex.ToString());
                //if (TRANSFER_ERRNO_EXCEPTION)
                //    throw;
            }
            catch (Exception ex)
            {
                _trace.TraceError(ex.ToString());
            }
            stream_in.Close();
            stream_in.Dispose();
            return ret;
        }

        /// <summary>
        /// 预创建文件，用于分段上传
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="block_count">分段数量（以4mb为一个分段）</param>
        /// <returns></returns>
        public PreCreateResult PreCreateFile(string path, int block_count)
        {
            _trace.TraceInfo("BaiduPCS.PreCreateFile called: string path=" + path + ", int block_count=" + block_count);
            var ret = new PreCreateResult();
            if (string.IsNullOrEmpty(path) || block_count == 0) return ret;

            var ns = new NetStream();
            ns.CookieKey = _auth.CookieIdentifier;
            var query_param = new Parameters();

            query_param.Add("channel", "chunlei");
            query_param.Add("web", 1);
            query_param.Add("app_id", APPID);
            query_param.Add("bdstoken", _bdstoken);
            query_param.Add("logid", _get_logid());
            query_param.Add("clienttype", 0);

            var post_data = new Parameters();
            post_data.Add("path", path);
            post_data.Add("autoinit", 1);
            var block_array = new JArray();
            var rnd = new Random();
            var rnd_md5 = new byte[16]; //这里直接随机就ok了，反正也没什么卵用
            for (int i = 0; i < block_count; i++)
            {
                rnd.NextBytes(rnd_md5);
                var str_md5 = util.Hex(rnd_md5);
                block_array.Add(str_md5);
            }
            post_data.Add("block_list", JsonConvert.SerializeObject(block_array));

            try
            {
                ns.HttpPost(API_PRECREATE_URL, post_data, headerParam: _get_xhr_param(), urlParam: query_param);
                var response = ns.ReadResponseString();
                ns.Close();

                _trace.TraceInfo(response);
                var json = JsonConvert.DeserializeObject(response) as JObject;
                _check_error(json);

                ret.BlockCount = json.Value<JArray>("block_list").Count;
                ret.ReturnType = json.Value<int>("return_type");
                ret.UploadId = json.Value<string>("uploadid");
            }
            catch (ErrnoException ex)
            {
                _trace.TraceError(ex.ToString());
                //if (TRANSFER_ERRNO_EXCEPTION)
                //    throw;
            }
            catch (Exception ex)
            {
                _trace.TraceError(ex.ToString());
            }
            return ret;
        }
        /// <summary>
        /// 上传分段数据
        /// </summary>
        /// <param name="stream_in">输入数据流</param>
        /// <param name="path">文件路径</param>
        /// <param name="uploadid">上传id</param>
        /// <param name="sequence">分段索引</param>
        /// <param name="callback">进度回调函数</param>
        /// <returns></returns>
        public string UploadSliceRaw(Stream stream_in, string path, string uploadid, int sequence, UploadStatusCallback callback = null)
        {
            _trace.TraceInfo("BaiduPCS.UploadSliceRaw called: Stream stream_in=" + stream_in.ToString() + ", string uploadid=" + uploadid + ", int sequence=" + sequence + ", UploadStatusCallback callback=" + callback.ToString());
            if (string.IsNullOrEmpty(uploadid) || string.IsNullOrEmpty(path) || !stream_in.CanRead) return string.Empty;
            if (string.IsNullOrEmpty(_auth.bduss)) return string.Empty;

            var query_param = new Parameters();
            query_param.Add("method", "upload");
            query_param.Add("app_id", APPID);
            query_param.Add("channel", "chunlei");
            query_param.Add("clienttype", 1);
            query_param.Add("web", 1);
            query_param.Add("BDUSS", _auth.bduss);
            query_param.Add("logid", _get_logid());
            query_param.Add("path", path);
            query_param.Add("uploadid", uploadid);
            query_param.Add("partseq", sequence);

            var ns = new NetStream();
            ns.CookieKey = _auth.CookieIdentifier;
            var boundary = util.GenerateFormDataBoundary();

            var formdata_param = new Parameters();
            formdata_param.Add("Content-Disposition", "form-data; name=\"file\"; filename=\"blob\"");
            formdata_param.Add("Content-Type", "application/octet-stream");

            var head = util.GenerateFormDataObject(boundary, formdata_param);
            var foot = util.GenerateFormDataEnding(boundary);

            var head_bytes = Encoding.UTF8.GetBytes(head);
            var foot_bytes = Encoding.UTF8.GetBytes(foot);

            var temp_ms = new MemoryStream();
            var buffer = new byte[BUFFER_SIZE];
            //load to memory stream
            try
            {
                int nread = 0, totalread = 0;
                do
                {
                    int length = Math.Min(BUFFER_SIZE, UPLOAD_SLICE_SIZE - totalread);
                    nread = stream_in.Read(buffer, 0, length);
                    temp_ms.Write(buffer, 0, nread);
                    totalread += length;
                } while (nread != 0);
            }
            catch (Exception ex)
            {
                _trace.TraceError(ex.ToString());
                return string.Empty;
            }
            temp_ms.Seek(0, SeekOrigin.Begin);

            long total_length = head_bytes.Length + foot_bytes.Length + temp_ms.Length;

            try
            {
                var stream_out = ns.HttpPost(PCS_SUPERFILE_URL, total_length, "multipart/form-data; boundary=" + boundary, headerParam: _get_xhr_param(), urlParam: query_param);
                stream_out.Write(head_bytes, 0, head_bytes.Length);

                long total_read = 0, current_read = 0;
                do
                {
                    current_read = temp_ms.Read(buffer, 0, BUFFER_SIZE);
                    stream_out.Write(buffer, 0, (int)current_read);
                    total_read += current_read;
                    callback?.Invoke(path, string.Empty, total_read, temp_ms.Length);
                } while (current_read != 0);

                stream_out.Write(foot_bytes, 0, foot_bytes.Length);

                ns.HttpPostClose();
                var response = ns.ReadResponseString();
                ns.Close();

                _trace.TraceInfo(response);
                var json = JsonConvert.DeserializeObject(response) as JObject;
                _check_error(json);

                return json.Value<string>("md5");
            }
            catch (ErrnoException ex)
            {
                _trace.TraceError(ex.ToString());
                //if (TRANSFER_ERRNO_EXCEPTION)
                    //throw;
            }
            catch (Exception ex)
            {
                _trace.TraceError(ex.ToString());
            }
            finally
            {
                temp_ms.Close();
            }
            return string.Empty;

        }
        /// <summary>
        /// 合并分段数据（注意：返回的MD5值有很大几率是错误的）
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="uploadid">上传id</param>
        /// <param name="block_list">分段数据的MD5值</param>
        /// <param name="file_size">文件大小</param>
        /// <returns></returns>
        public ObjectMetadata CreateSuperFile(string path, string uploadid, IEnumerable<string> block_list, ulong file_size)
        {
            _trace.TraceInfo("BaiduPCS.CreateSuperFile called: string path=" + path + ", string uploadid=" + uploadid + ", Ienumerable<string> block_list=[count=" + block_list.Count() + "]");
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(uploadid) || block_list == null) return new ObjectMetadata();
            var ret = new ObjectMetadata();

            var query_param = new Parameters();
            query_param.Add("isdir", 0);
            query_param.Add("channel", "chunlei");
            query_param.Add("web", 1);
            query_param.Add("app_id", APPID);
            query_param.Add("bdstoken", _bdstoken);
            query_param.Add("logid", _get_logid());
            query_param.Add("clienttype", 0);

            var post_param = new Parameters();
            post_param.Add("path", path);
            post_param.Add("size", file_size);
            post_param.Add("uploadid", uploadid);
            var blist = new JArray();
            foreach (var item in block_list)
            {
                blist.Add(item);
            }
            post_param.Add("block_list", JsonConvert.SerializeObject(blist));

            var ns = new NetStream();
            ns.CookieKey = _auth.CookieIdentifier;
            try
            {
                ns.HttpPost(API_CREATE_URL, post_param, headerParam: _get_xhr_param(), urlParam: query_param);
                var response = ns.ReadResponseString();
                ns.Close();

                _trace.TraceInfo(response);
                var json = JsonConvert.DeserializeObject(response) as JObject;
                _check_error(json);

                ret.ServerCTime = json.Value<ulong>("ctime");
                ret.ServerMTime = json.Value<ulong>("mtime");
                ret.FS_ID = json.Value<ulong>("fs_id");
                ret.IsDir = json.Value<int>("isdir") != 0;
                ret.MD5 = json.Value<string>("md5");
                ret.Path = json.Value<string>("path");
                ret.Size = json.Value<ulong>("size");

                ret.ServerFileName = ret.Path.Split('/').Last();
            }
            catch (ErrnoException ex)
            {
                _trace.TraceError(ex.ToString());
                //if (TRANSFER_ERRNO_EXCEPTION)
                //    throw;
            }
            catch (Exception ex)
            {
                _trace.TraceError(ex.ToString());
            }
            return ret;
        }
        #endregion


        #region Download
        /// <summary>
        /// PCS API for download，返回的只是带参数的url地址
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="over_https">是否使用https</param>
        /// <returns></returns>
        public string GetDownloadLink_API(string path, bool over_https = true)
        {
            _trace.TraceInfo("BaiduPCS.GetDownloadLink_API called: string path=" + path + ", bool over_https=" + over_https);
            var param = new Parameters();
            param.Add("method", "download");
            param.Add("app_id", APPID);
            param.Add("path", path);

            var src_url = PCS_FILE_URL;
            if (src_url.StartsWith("http://")) src_url = src_url.Substring(7);
            else if (src_url.StartsWith("https://")) src_url = src_url.Substring(8);
            var ret_url = "http";
            if (over_https) ret_url += "s";
            ret_url += "://" + src_url;
            ret_url += "?" + param.BuildQueryString();
            return ret_url;
        }
        /// <summary>
        /// 获取下载url，失败时返回string.Empty
        /// </summary>
        /// <param name="fs_id">fs_id</param>
        /// <param name="over_https">是否使用https</param>
        /// <returns></returns>
        public string GetDownloadLink(ulong fs_id, bool over_https = true)
        {
            _trace.TraceInfo("BaiduPCS.GetDownloadLink called: ulong fs_id=" + fs_id + ", bool over_https=" + over_https);
            if (fs_id == 0) return string.Empty;

            var param = new Parameters();
            var ns = new NetStream();
            ns.CookieKey = _auth.CookieIdentifier;

            param.Add("sign", _sign2);
            param.Add("timestamp", _timestamp);
            param.Add("fidlist", "[" + fs_id + "]");
            param.Add("type", "dlink");
            param.Add("channel", "chunlei");
            param.Add("web", "1");
            param.Add("app_id", APPID);
            param.Add("bdstoken", _bdstoken);
            param.Add("logid", _get_logid());
            param.Add("clienttype", "0");

            try
            {
                var url = API_DOWNLOAD_URL;
                ns.HttpGet(url, _get_xhr_param(), param);

                var response = ns.ReadResponseString();
                ns.Close();

                _trace.TraceInfo(response);
                var json = JsonConvert.DeserializeObject(response) as JObject;
                _check_error(json);

                var dlink_array = json.Value<JArray>("dlink");
                var dlink = (dlink_array[0] as JObject).Value<string>("dlink");

                if (dlink.StartsWith("http") && over_https) dlink = "https" + dlink.Substring(4);

                return dlink;
            }
            catch (ErrnoException ex)
            {
                _trace.TraceError(ex.ToString());
                //if (TRANSFER_ERRNO_EXCEPTION)
                //    throw;
            }
            catch (Exception ex)
            {
                _trace.TraceError(ex.ToString());
            }
            return string.Empty;
        }
        /// <summary>
        /// 获取多源的下载url，失败时返回的Array元素为0
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns></returns>
        public string[] GetLocateDownloadLink(string path)
        {
            _trace.TraceInfo("BaiduPCS.GetLocateDownloadLink called: string path=" + path);
            var ret_list = new List<string>();
            if (string.IsNullOrEmpty(path)) return ret_list.ToArray();

            var ns = new NetStream();
            ns.CookieKey = _auth.CookieIdentifier;
            var param = new Parameters();
            param.Add("method", "locatedownload");
            param.Add("app_id", APPID);
            param.Add("ver", "4.0");
            param.Add("path", path);

            try
            {
                var url = PCS_FILE_URL;
                ns.HttpPost(url, new byte[] { }, "text/html", _get_xhr_param(), param);

                var response = ns.ReadResponseString();
                ns.Close();

                _trace.TraceInfo(response);
                var json = JsonConvert.DeserializeObject(response) as JObject;
                _check_error(json);

                var download_urls = json.Value<JArray>("urls");
                foreach (JObject item in download_urls)
                {
                    ret_list.Add(item.Value<string>("url"));
                }
            }
            catch (ErrnoException ex)
            {
                _trace.TraceError(ex.ToString());
                //if (TRANSFER_ERRNO_EXCEPTION)
                //    throw;
            }
            catch (Exception ex)
            {
                _trace.TraceError(ex.ToString());
            }
            return ret_list.ToArray();
        }
        #endregion


        #region Sharing
        /// <summary>
        /// 分享数据
        /// </summary>
        public struct ShareData
        {
            /// <summary>
            /// 创建时间（Unix timestamp）
            /// </summary>
            public ulong CreateTime;
            /// <summary>
            /// 有效时间（天数，0：永久）
            /// </summary>
            public int ExpiredType;
            /// <summary>
            /// 长分享链接
            /// </summary>
            public string Link;
            /// <summary>
            /// 未知属性
            /// </summary>
            public bool Premis;
            /// <summary>
            /// Share ID
            /// </summary>
            public ulong ShareID;
            /// <summary>
            /// 短分享链接
            /// </summary>
            public string ShortURL;
        }
        /// <summary>
        /// 创建单文件（夹）公开分享
        /// </summary>
        /// <param name="fs_id">FS ID</param>
        /// <param name="expireTime">有效时间（可选：1-1天，7-7天，0-永久）</param>
        /// <returns></returns>
        public ShareData CreatePublicShare(ulong fs_id, int expireTime = 0)
        {
            _trace.TraceInfo("BaiduPCS.CreatePublicShare called: ulong fs_id=" + fs_id + ", int expireTime=" + expireTime);
            return CreatePublicShare(new ulong[] { fs_id }, expireTime);
        }
        /// <summary>
        /// 创建多文件（夹）公开分享
        /// </summary>
        /// <param name="fs_ids">FS ID</param>
        /// <param name="expireTime">有效时间（可选：1-1天，7-7天，0-永久）</param>
        /// <returns></returns>
        public ShareData CreatePublicShare(IEnumerable<ulong> fs_ids, int expireTime = 0)
        {
            _trace.TraceInfo("BaiduPCS.CreatePublicShare called: IEnumerable<ulong> fs_ids=[count=" + fs_ids.Count() + "]" + ", int expireTime=" + expireTime);
            var ret = new ShareData();

            if (expireTime != 0 && expireTime != 1 && expireTime != 7) return ret;
            var ns = new NetStream();
            ns.CookieKey = _auth.CookieIdentifier;
            var xhr_param = _get_xhr_param();
            var query_param = new Parameters();
            query_param.Add("channel", "chunlei");
            query_param.Add("clienttype", 0);
            query_param.Add("web", 1);
            query_param.Add("app_id", APPID);
            query_param.Add("bdstoken", _bdstoken);
            query_param.Add("logid", _get_logid());

            var post_param = new Parameters();
            var fid_list = new JArray();
            foreach (var item in fs_ids)
            {
                fid_list.Add(item);
            }
            post_param.Add("fid_list", JsonConvert.SerializeObject(fid_list));
            post_param.Add("schannel", 0);
            post_param.Add("channel_list", "[]");
            post_param.Add("period", expireTime);

            try
            {
                ns.HttpPost(API_SHARE_SET_URL, post_param, headerParam: xhr_param, urlParam: query_param);
                var response = ns.ReadResponseString();
                ns.Close();
                _trace.TraceInfo(response);

                var json = JsonConvert.DeserializeObject(response) as JObject;
                _check_error(json);

                ret.CreateTime = json.Value<ulong>("ctime");
                ret.ExpiredType = json.Value<int>("expireType");
                ret.Link = json.Value<string>("link");
                ret.Premis = json.Value<bool>("premis");
                ret.ShareID = json.Value<ulong>("shareid");
                ret.ShortURL = json.Value<string>("shorturl");
            }
            catch (ErrnoException ex)
            {
                _trace.TraceError(new Exception("创建分享失败", ex).ToString());
                //if (TRANSFER_ERRNO_EXCEPTION)
                //    throw;
            }
            catch (Exception ex)
            {
                _trace.TraceError(new Exception("创建分享失败", ex).ToString());
            }
            return ret;
        }

        public ShareData CreatePrivateShare(ulong fs_id, string password, int expireTime = 0)
        {
            _trace.TraceInfo("BaiduPCS.CreatePrivateShare called: ulong fs_id=" + fs_id + ", string password=" + password + ", int expireTime=" + expireTime);
            return CreatePrivateShare(new ulong[] { fs_id }, password, expireTime);
        }
        public ShareData CreatePrivateShare(IEnumerable<ulong> fs_ids, string password, int expireTime = 0)
        {
            _trace.TraceInfo("BaiduPCS.CreatePrivateShare called: Ienumerable<ulong> fs_ids=[count=" + fs_ids.Count() + "], string password=" + password + ", int expireTime=" + expireTime);
            ShareData ret = new ShareData();
            if (expireTime != 0 && expireTime != 1 && expireTime != 7) return ret;
            if (password == null || password.Length != 4) return ret;
            var ns = new NetStream();
            ns.CookieKey = _auth.CookieIdentifier;
            var xhr_param = _get_xhr_param();
            var query_param = new Parameters();
            query_param.Add("channel", "chunlei");
            query_param.Add("clienttype", 0);
            query_param.Add("web", 1);
            query_param.Add("app_id", APPID);
            query_param.Add("bdstoken", _bdstoken);
            query_param.Add("logid", _get_logid());

            var post_body = new Parameters();
            var fid_list = new JArray();
            foreach (var item in fs_ids)
            {
                fid_list.Add(item);
            }
            post_body.Add("fid_list", JsonConvert.SerializeObject(fid_list));
            post_body.Add("schannel", 4);
            post_body.Add("channel_list", "[]");
            post_body.Add("period", expireTime);
            post_body.Add("pwd", password);

            try
            {
                ns.HttpPost(API_SHARE_SET_URL, post_body, headerParam: xhr_param, urlParam: query_param);
                var response = ns.ReadResponseString();
                ns.Close();
                _trace.TraceInfo(response);

                var json = JsonConvert.DeserializeObject(response) as JObject;
                _check_error(json);
                ret.CreateTime = json.Value<ulong>("ctime");
                ret.ExpiredType = json.Value<int>("expiredType");
                ret.Link = json.Value<string>("link");
                ret.Premis = json.Value<bool>("premis");
                ret.ShareID = json.Value<ulong>("shareid");
                ret.ShortURL = json.Value<string>("shorturl");
            }
            catch (ErrnoException ex)
            {
                _trace.TraceError(new Exception("创建私密分享错误", ex).ToString());
                //if (TRANSFER_ERRNO_EXCEPTION)
                //    throw;
            }
            catch (Exception ex)
            {
                _trace.TraceError(new Exception("创建私密分享错误", ex).ToString());
            }
            return ret;
        }

        /// <summary>
        /// 取消分享
        /// </summary>
        /// <param name="share_id">Share ID</param>
        public void CancelShare(ulong share_id)
        {
            CancelShare(new ulong[] { share_id });
        }
        /// <summary>
        /// 取消多个分享
        /// </summary>
        /// <param name="share_ids">Share ID</param>
        public void CancelShare(IEnumerable<ulong> share_ids)
        {
            var ns = new NetStream();
            ns.CookieKey = _auth.CookieIdentifier;
            var xhr_param = _get_xhr_param();
            var query_param = new Parameters();
            query_param.Add("channel", "chunlei");
            query_param.Add("clienttype", 0);
            query_param.Add("web", 1);
            query_param.Add("bdstoken", _bdstoken);
            query_param.Add("app_id", APPID);
            query_param.Add("logid", _get_logid());

            var post_body = new Parameters();
            var shareid_list = new JArray();
            foreach (var item in share_ids)
            {
                shareid_list.Add(item);
            }
            post_body.Add("shareid_list", JsonConvert.SerializeObject(shareid_list));

            try
            {
                ns.HttpPost(API_SHARE_CANCEL_URL, post_body, headerParam: xhr_param, urlParam: query_param);
                var response = ns.ReadResponseString();
                ns.Close();
                _trace.TraceInfo(response);

                var json = JsonConvert.DeserializeObject(response) as JObject;
                _check_error(json);

            }
            catch (ErrnoException ex)
            {
                _trace.TraceError(new Exception("取消分享失败", ex).ToString());
                //if (TRANSFER_ERRNO_EXCEPTION)
                //    throw;
            }
            catch (Exception ex)
            {
                _trace.TraceError(new Exception("取消分享失败", ex).ToString());
            }
        }
        public struct ShareRecord
        {
            /// <summary>
            /// 创建时间
            /// </summary>
            public ulong CreateTime;
            /// <summary>
            /// 私密分享的密码
            /// </summary>
            public string Password;
            /// <summary>
            /// 是否为公开分享
            /// </summary>
            public bool IsPublic;
            /// <summary>
            /// Share　ID
            /// </summary>
            public ulong ShareID;
            /// <summary>
            /// 分享的短链接
            /// </summary>
            public string ShortURL;
            /// <summary>
            /// 分享状态（0：正常，9：被删除，其他待研究）
            /// </summary>
            public int Status;
            /// <summary>
            /// 文件路径
            /// </summary>
            public string TypicalPath;
            /// <summary>
            /// 该分享所包含的FS ID列表
            /// </summary>
            public List<ulong> FS_IDs;
            /// <summary>
            /// 有效期（1：1天，7：7天，0：永久）
            /// </summary>
            public int ExpiredType;
            /// <summary>
            /// 浏览数
            /// </summary>
            public uint ViewCount;
            /// <summary>
            /// 下载数
            /// </summary>
            public uint DownloadCount;
            /// <summary>
            /// 转存数
            /// </summary>
            public uint TransferCount;
            /// <summary>
            /// 转为ShareData类型的数据
            /// </summary>
            /// <returns></returns>
            public ShareData ToShareDate()
            {
                return new ShareData
                {
                    CreateTime = CreateTime,
                    Link = string.Empty,
                    ExpiredType = ExpiredType,
                    Premis = false, //unknown
                    ShareID = ShareID,
                    ShortURL = ShortURL
                };
            }
        }
        /// <summary>
        /// 获取分享记录（目前仅限首页）
        /// </summary>
        /// <returns></returns>
        public ShareRecord[] GetShareRecords()
        {
            _trace.TraceInfo("BaiduPCS.GetShareRecords called: void");
            var ret = new List<ShareRecord>();
            //todo:支持多页&多排序
            var page = 1;

            var xhr_param = _get_xhr_param();
            var query_param = new Parameters();
            query_param.Add("channel", "chunlei");
            query_param.Add("clienttype", 0);
            query_param.Add("web", 1);
            query_param.Add("page", page);
            query_param.Add("order", "ctime");
            query_param.Add("desc", 1);
            query_param.Add("_", (ulong)util.ToUnixTimestamp(DateTime.Now) * 1000);
            query_param.Add("bdstoken", _bdstoken);
            query_param.Add("app_id", APPID);
            query_param.Add("logid", _get_logid());

            var ns = new NetStream();
            ns.CookieKey = _auth.CookieIdentifier;
            try
            {
                ns.HttpGet(API_SHARE_RECORD_URL, xhr_param, query_param);
                var response = ns.ReadResponseString();
                ns.Close();
                _trace.TraceInfo(response);

                var json = JsonConvert.DeserializeObject(response) as JObject;
                _check_error(json);

                var nextpage = json.Value<int>("nextpage");
                if (nextpage != 0)
                {
                    _trace.TraceWarning("检测到分享列表存在多页，但目前仍不支持");
                }

                var list = json.Value<JArray>("list");
                foreach (JObject item in list)
                {
                    var record = new ShareRecord();
                    record.CreateTime = item.Value<ulong>("ctime");
                    record.DownloadCount = item.Value<uint>("dCnt");
                    record.ExpiredType = item.Value<int>("expiredType");
                    record.FS_IDs = new List<ulong>();
                    foreach (var item2 in item.Value<JArray>("fsIds"))
                    {
                        record.FS_IDs.Add((ulong)item2);
                    }
                    record.IsPublic = item.Value<int>("public") != 0;
                    record.Password = item.Value<string>("passwd");
                    record.ShareID = item.Value<ulong>("shareId");
                    record.ShortURL = item.Value<string>("shortlink");
                    record.Status = item.Value<int>("status");
                    record.TransferCount = item.Value<uint>("tCnt");
                    record.TypicalPath = item.Value<string>("typicalPath");
                    record.ViewCount = item.Value<uint>("vCnt");

                    ret.Add(record);
                }
            }
            catch (ErrnoException ex)
            {
                _trace.TraceError(new Exception("获取分享列表时发生错误", ex).ToString());
                //if (TRANSFER_ERRNO_EXCEPTION)
                //    throw;
            }
            catch (Exception ex)
            {
                _trace.TraceError(new Exception("获取分享列表时发生错误", ex).ToString());
            }
            return ret.ToArray();
        }

        #endregion

        private void TestFunc()
        {
            var url = "http://pan.baidu.com/api/report/user";
            var query_param = new Parameters();
            query_param.Add("channel", "chunlei");
            query_param.Add("web", 1);
            query_param.Add("app_id", APPID);
            query_param.Add("bdstoken", _bdstoken);
            query_param.Add("logid", _get_logid());
            query_param.Add("clienttype", 0);

            var post_param = new Parameters();
            post_param.Add("timestamp", (long)util.ToUnixTimestamp(DateTime.Now));
            post_param.Add("action", "fm_self");

            var ns = new NetStream();
            ns.CookieKey = _auth.CookieIdentifier;
            ns.HttpPost(url, post_param, headerParam: _get_xhr_param(), urlParam: query_param);
            var rep = ns.ReadResponseString();
            ns.Close();
        }
    }
    public class ErrnoException : Exception
    {
        public int Errno { get; }
        public ErrnoException(int errno) : base()
        {
            Errno = errno;
        }
        public ErrnoException(int errno, string message) : base(message)
        {
            Errno = errno;
        }
        public ErrnoException(int errno, string message, Exception innerException) : base(message, innerException)
        {
            Errno = errno;
        }
    }
}
