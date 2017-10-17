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

        //https://pan.baidu.com/
        public const string API_HOST = "https://pan.baidu.com/";
        //https://pan.baidu.com/api/
        public const string API_ROOT_URL = API_HOST + "api/";
        //https://pan.baidu.com/api/list
        public const string API_LIST_URL = API_ROOT_URL + "list";
        //https://pan.baidu.com/api/quota
        public const string API_QUOTA_URL = API_ROOT_URL + "quota";
        //https://pan.baidu.com/api/filemanager
        public const string API_FILEMANAGER_URL = API_ROOT_URL + "filemanager";
        //https://pan.baidu.com/api/download
        public const string API_DOWNLOAD_URL = API_ROOT_URL + "download";
        //https://pan.baidu.com/api/create
        public const string API_CREATE_URL = API_ROOT_URL + "create";
        //https://pan.baidu.com/api/precreate
        public const string API_PRECREATE_URL = API_ROOT_URL + "precreate";
        //https://pan.baidu.com/api/filediff
        public const string API_FILEDIFF_URL = API_ROOT_URL + "filediff";
        //https://pan.baidu.com/share/
        public const string API_SHARE_URL = API_HOST + "share/";
        //https://pan.baidu.com/share/set
        public const string API_SHARE_SET_URL = API_SHARE_URL + "set";
        //https://pan.baidu.com/share/cancel
        public const string API_SHARE_CANCEL_URL = API_SHARE_URL + "cancel";
        //https://pan.baidu.com/share/record
        public const string API_SHARE_RECORD_URL = API_SHARE_URL + "record";

        //https://pan.baidu.com/disk/home
        public const string BAIDU_NETDISK_URL = "https://pan.baidu.com/disk/home";

        //默认数据流缓存区大小
        public const int BUFFER_SIZE = 2048;
        //文件验证段：前256KB字节
        public const long VALIDATE_SIZE = 262144;
        //默认上传分段的大小
        public const int UPLOAD_SLICE_SIZE = 4194304;

        public delegate void UploadStatusCallback(string path, string local_path, long current, long length);
        #endregion

        #region auth
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
            /// <summary>
            /// 文件是否被删除（仅限于FileDiff）
            /// </summary>
            public bool IsDelete;
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
            if (errno == 0) errno = obj.Value<int>("error_code");
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
            ret.IsDelete = obj.Value<int>("isdelete") != 0;
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
        /// <summary>
        /// 获取文件差异
        /// </summary>
        /// <param name="has_more">是否还需要获取</param>
        /// <param name="next_cursor">下一次获取的游标位置</param>
        /// <param name="reset">是否需要重置</param>
        /// <param name="cursor">游标位置</param>
        public ObjectMetadata[] GetFileDiff(out string next_cursor, out bool has_more, out bool reset, string cursor = null)
        {
            ObjectMetadata[] ret = null;
            var sync_thread = Thread.CurrentThread;
            string arg1 = null;
            bool arg2 = false, arg3 = false;
            GetFileDiffAsync(cursor, (suc, hasmore, rst, nextcursor, data) =>
            {
                ret = data;
                arg1 = nextcursor;
                arg2 = rst;
                arg3 = hasmore;
                sync_thread.Interrupt();
            });
            try
            {
                Thread.Sleep(Timeout.Infinite);
            }
            catch (ThreadInterruptedException) { }
            catch (Exception ex) { throw ex; }
            next_cursor = arg1;
            has_more = arg2;
            reset = arg3;
            return ret;
        }
        #endregion


        #region Upload
        
        /// <summary>
        /// 秒传文件，失败时返回fs_id为0
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

            var sync_thread = Thread.CurrentThread;
            ObjectMetadata ret = new ObjectMetadata();
            RapidUploadAsync(path, content_length, content_md5, content_crc, slice_md5, (suc, data) =>
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

            ObjectMetadata ret = new ObjectMetadata();
            var sync_thread = Thread.CurrentThread;
            Guid task_id;
            UploadBeginAsync(content_length, path, (suc, id, data) =>
            {
                try
                {
                    task_id = id;
                    var buffer = new byte[BUFFER_SIZE];
                    long total = 0;
                    int cur = 0;
                    do
                    {
                        cur = stream_in.Read(buffer, 0, (int)Math.Min(BUFFER_SIZE, (long)content_length - total));
                        data.Write(buffer, 0, cur);
                        total += cur;
                        callback?.Invoke(path, null, total, (long)content_length);
                    } while (cur > 0);

                    UploadEndAsync(id, (suc2, data2) =>
                    {
                        ret = data2;
                        sync_thread.Interrupt();
                    });
                }
                catch (Exception ex)
                {
                    _trace.TraceError(ex);
                    sync_thread.Interrupt();
                }
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
        /// 预创建文件，用于分段上传
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="block_count">分段数量（以4mb为一个分段）</param>
        /// <returns></returns>
        public PreCreateResult PreCreateFile(string path, int block_count)
        {
            _trace.TraceInfo("BaiduPCS.PreCreateFile called: string path=" + path + ", int block_count=" + block_count);
            var ret = new PreCreateResult();

            var sync_thread = Thread.CurrentThread;
            PreCreateFileAsync(path, block_count, (suc, block_count2, uploadid) =>
            {
                ret.BlockCount = block_count2;
                ret.ReturnType = 0;
                ret.UploadId = uploadid;
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

            string ret = null;
            var sync_thread = Thread.CurrentThread;
            Guid task_id;
            var upload_data = util.ReadBytes(stream_in, UPLOAD_SLICE_SIZE);
            var memory_buffer = new MemoryStream(upload_data);
            upload_data = null;

            UploadSliceBeginAsync((ulong)memory_buffer.Length, path, uploadid, sequence, (suc, id, data) =>
            {
                task_id = id;
                var buffer = new byte[BUFFER_SIZE];
                long total = 0;
                int cur = 0;
                do
                {
                    cur = memory_buffer.Read(buffer, 0, BUFFER_SIZE);
                    data.Write(buffer, 0, cur);
                    total += cur;
                    callback?.Invoke(path, null, total, (long)memory_buffer.Length);
                } while (cur > 0);

                UploadSliceEndAsync(id, (suc2, data2) =>
                {
                    ret = data2;
                    sync_thread.Interrupt();
                });
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
        /// 合并分段数据（注意：分段数大于1时返回的MD5值有很大几率是错误的）
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="uploadid">上传id</param>
        /// <param name="block_list">分段数据的MD5值</param>
        /// <param name="file_size">文件大小</param>
        /// <returns></returns>
        public ObjectMetadata CreateSuperFile(string path, string uploadid, IEnumerable<string> block_list, ulong file_size)
        {
            _trace.TraceInfo("BaiduPCS.CreateSuperFile called: string path=" + path + ", string uploadid=" + uploadid + ", Ienumerable<string> block_list=[count=" + block_list.Count() + "]");
            ObjectMetadata ret = new ObjectMetadata();
            var sync_thread = Thread.CurrentThread;
            CreateSuperFileAsync(path, uploadid, block_list, file_size, (suc, data) =>
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
        #endregion


        #region Download
        /// <summary>
        /// PCS API for download，返回的只是带参数的url地址（不用发送任何http请求）
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

            var sync_thread = Thread.CurrentThread;
            string[] ret = null;
            GetDownloadLinkAsync(fs_id, (suc, data) =>
            {
                ret = data;
                sync_thread.Interrupt();
            }, over_https);

            try
            {
                Thread.Sleep(Timeout.Infinite);
            }
            catch (ThreadInterruptedException) { }
            catch (Exception ex) { throw ex; }

            if (ret == null || ret.Length == 0) return string.Empty;
            return ret[0];
        }
        /// <summary>
        /// 获取多源的下载url，失败时返回的Array元素为0
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns></returns>
        public string[] GetLocateDownloadLink(string path)
        {
            _trace.TraceInfo("BaiduPCS.GetLocateDownloadLink called: string path=" + path);

            var sync_thread = Thread.CurrentThread;
            string[] ret = null;
            GetLocateDownloadLinkAsync(path, (suc, data) =>
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

            if (ret == null) return new string[0];
            return ret;
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

            var sync_thread = Thread.CurrentThread;
            CreatePublicShareAsync(fs_ids, (suc, data) =>
            {
                ret = data;
                sync_thread.Interrupt();
            }, expireTime);

            try
            {
                Thread.Sleep(Timeout.Infinite);
            }
            catch (ThreadInterruptedException) { }
            catch (Exception ex) { throw ex; }

            return ret;
        }

        /// <summary>
        /// 创建单文件（夹）加密分享
        /// </summary>
        /// <param name="fs_id">FS ID</param>
        /// <param name="password">分享密码</param>
        /// <param name="expireTime">有效时间（可选：1-1天，7-7天，0-永久）</param>
        public ShareData CreatePrivateShare(ulong fs_id, string password, int expireTime = 0)
        {
            _trace.TraceInfo("BaiduPCS.CreatePrivateShare called: ulong fs_id=" + fs_id + ", string password=" + password + ", int expireTime=" + expireTime);
            return CreatePrivateShare(new ulong[] { fs_id }, password, expireTime);
        }
        /// <summary>
        /// 创建多文件（夹）加密分享
        /// </summary>
        /// <param name="fs_ids">FS ID</param>
        /// <param name="password">分享密码</param>
        /// <param name="expireTime">有效时间（可选：1-1天，7-7天，0-永久）</param>
        /// <returns></returns>
        public ShareData CreatePrivateShare(IEnumerable<ulong> fs_ids, string password, int expireTime = 0)
        {
            _trace.TraceInfo("BaiduPCS.CreatePrivateShare called: Ienumerable<ulong> fs_ids=[count=" + fs_ids.Count() + "], string password=" + password + ", int expireTime=" + expireTime);
            var ret = new ShareData();

            var sync_thread = Thread.CurrentThread;
            CreatePublicShareAsync(fs_ids, (suc, data) =>
            {
                ret = data;
                sync_thread.Interrupt();
            }, expireTime);

            try
            {
                Thread.Sleep(Timeout.Infinite);
            }
            catch (ThreadInterruptedException) { }
            catch (Exception ex) { throw ex; }

            return ret;
        }

        /// <summary>
        /// 取消分享
        /// </summary>
        /// <param name="share_id">Share ID</param>
        public void CancelShare(ulong share_id)
        {
            _trace.TraceInfo("BaiduPCS.CancelShare called: ulong share_id=" + share_id);
            CancelShare(new ulong[] { share_id });
        }
        /// <summary>
        /// 取消多个分享
        /// </summary>
        /// <param name="share_ids">Share ID</param>
        public void CancelShare(IEnumerable<ulong> share_ids)
        {
            _trace.TraceInfo("BaiduPCS.CancelShare called: IEnumerable<ulong> share_ids=[count=" + share_ids.Count() + "]");
            var sync_thread = Thread.CurrentThread;
            CancelShareAsync(share_ids, (suc, data) =>
            {
                sync_thread.Interrupt();
            });
            try
            {
                Thread.Sleep(Timeout.Infinite);
            }
            catch (ThreadInterruptedException) { }
            catch (Exception ex) { throw ex; }
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
            ShareRecord[] ret = null;
            var sync_thread = Thread.CurrentThread;
            GetShareRecordsAsync((suc, data) =>
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

        #endregion

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
