using BaiduCloudSync.NetUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace BaiduCloudSync
{
    public partial class BaiduPCS
    {
        //是否在函数调用时写入Debug消息
        private const bool _enable_function_trace = true;
        //回调函数的类型声明
        #region delegation callback

        /// <summary>
        /// 网盘使用情况回调函数原型
        /// </summary>
        /// <param name="success">是否成功</param>
        /// <param name="result">网盘的使用情况</param>
        public delegate void QuotaCallback(bool success, Quota result, object state);
        /// <summary>
        /// 文件/文件夹操作回调函数原型
        /// </summary>
        /// <param name="success">是否成功</param>
        /// <param name="result">是否成功（同success）</param>
        public delegate void OperationCallback(bool success, bool result, object state);
        /// <summary>
        /// 文件/文件夹信息回调函数原型
        /// </summary>
        /// <param name="success">是否成功</param>
        /// <param name="result">文件/文件夹信息</param>
        public delegate void ObjectMetaCallback(bool success, ObjectMetadata result, object state);
        /// <summary>
        /// 多个文件/文件夹信息回调函数原型
        /// </summary>
        /// <param name="success">是否成功</param>
        /// <param name="result">多个文件/文件夹信息</param>
        public delegate void MultiObjectMetaCallback(bool success, ObjectMetadata[] result, object state);
        /// <summary>
        /// 文件差异比较的回调函数
        /// </summary>
        /// <param name="success">是否成功</param>
        /// <param name="has_more">是否需要继续请求</param>
        /// <param name="reset">是否重置</param>
        /// <param name="next_cursor">下一个游标位置</param>
        /// <param name="result">多个文件/文件夹信息</param>
        public delegate void FileDiffCallback(bool success, bool has_more, bool reset, string next_cursor, ObjectMetadata[] result, object state);
        /// <summary>
        /// 异步上传文件的回调函数原型
        /// </summary>
        /// <param name="success">是否成功</param>
        /// <param name="task_id">分配的任务id</param>
        /// <param name="connect_stream">输出的数据流</param>
        public delegate void UploadCallback(bool success, Guid task_id, Stream connect_stream, object state);
        /// <summary>
        /// 预创建文件的回调函数原型
        /// </summary>
        /// <param name="success">是否成功</param>
        /// <param name="block_count">分段数量</param>
        /// <param name="upload_id">上传id</param>
        public delegate void PreCreateCallback(bool success, int block_count, string upload_id, object state);
        /// <summary>
        /// 分段上传文件的回调函数原型
        /// </summary>
        /// <param name="success">是否成功</param>
        /// <param name="slice_md5">该分段的MD5</param>
        public delegate void SliceUploadCallback(bool success, string slice_md5, object state);
        /// <summary>
        /// 获取下载链接的回调函数原型
        /// </summary>
        /// <param name="success">是否成功</param>
        /// <param name="urls">多个url</param>
        public delegate void DownloadLinkCallback(bool success, string[] urls, object state);
        /// <summary>
        /// 分享文件的回调函数原型
        /// </summary>
        /// <param name="success"></param>
        /// <param name="result"></param>
        public delegate void ShareMetaCallback(bool success, ShareData result, object state);
        /// <summary>
        /// 获取分享详细信息的回调函数原型
        /// </summary>
        /// <param name="success"></param>
        /// <param name="result"></param>
        public delegate void ShareMultiMetaCallback(bool success, ShareRecord[] result, object state);
        #endregion

        #region file/directory operation
        /// <summary>
        /// 异步获取网盘配额
        /// </summary>
        /// <param name="callback">回调函数</param>
        public void GetQuotaAsync(QuotaCallback callback, object state = null)
        {
            _trace.TraceInfo("BaiduPCS.GetQuotaAsync called: QuotaCallback callback=" + callback.ToString());
            var ret = new Quota();

            var param = new Parameters();
            param.Add("checkexpire", 1);
            param.Add("app_id", APPID);
            param.Add("bdstoken", _auth.bdstoken);
            param.Add("logid", GetLogid());

            var ns = new NetStream();
            ns.CookieKey = _auth.CookieIdentifier;
            try
            {
                ns.HttpGetAsync(API_QUOTA_URL, (sender, e) =>
                {
                    //响应
                    try
                    {
                        var response = sender.ReadResponseString();

                        _trace.TraceInfo(response);
                        var json = JsonConvert.DeserializeObject(response) as JObject;
                        _check_error(json);

                        ret.InUsed = json.Value<ulong>("used");
                        ret.Total = json.Value<ulong>("total");
                        callback?.Invoke(true, ret, state);
                    }
                    catch (Exception ex)
                    {
                        _trace.TraceError(ex);
                        callback?.Invoke(false, ret, state);
                    }
                    finally
                    {
                        sender.Close();
                    }
                }
                , null, _get_xhr_param(), param);

            }
            catch (Exception ex)
            {
                _trace.TraceError(ex);
                ns.Close();
                throw;
            }
        }
        /// <summary>
        /// 异步删除单个文件
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="callback">回调函数</param>
        public void DeletePathAsync(string path, OperationCallback callback, object state = null)
        {
            _trace.TraceInfo("BaiduPCS.DeletePathAsync called: string path=" + path + ", OperationCallback callback=" + callback.ToString());
            DeletePathAsync(new string[] { path }, callback, state);
        }
        /// <summary>
        /// 异步删除多个文件
        /// </summary>
        /// <param name="paths">文件路径</param>
        /// <param name="callback">回调函数</param>
        public void DeletePathAsync(IEnumerable<string> paths, OperationCallback callback, object state = null)
        {
            _trace.TraceInfo("BaiduPCS.DeletePathAsync called: IEnumerable<string> paths=[count=" + paths.Count() + "], OperationCallback callback=" + callback.ToString());
            try
            {
                var querystr = new Parameters();
                querystr.Add("opera", "delete");
                //querystr.Add("async", 2);
                querystr.Add("channel", "chunlei");
                querystr.Add("web", 1);
                querystr.Add("app_id", APPID);
                querystr.Add("bdstoken", _auth.bdstoken);
                querystr.Add("logid", GetLogid());
                querystr.Add("clienttype", 0);

                var postParam = new Parameters();
                var postJson = new JArray();

                bool empty = true;

                foreach (var item in paths)
                {
                    empty = false;
                    if (string.IsNullOrEmpty(item)) throw new ArgumentNullException("path");
                    postJson.Add(item);
                }

                if (empty) return;

                postParam.Add("filelist", JsonConvert.SerializeObject(postJson));

                var ns = new NetStream();
                ns.CookieKey = _auth.CookieIdentifier;
                var data = Encoding.UTF8.GetBytes(postParam.BuildQueryString());
                ns.HttpPostAsync(API_FILEMANAGER_URL, data.Length, (sender, e) =>
                {
                    //GetRequestStream
                    try
                    {
                        sender.RequestStream.Write(data, 0, data.Length);
                        sender.HttpPostResponseAsync((sender2, e2) =>
                        {
                            //GetResponseStream
                            try
                            {
                                var response = sender2.ReadResponseString();
                                _trace.TraceInfo(response);
                                var json = JsonConvert.DeserializeObject(response) as JObject;
                                _check_error(json);
                                callback?.Invoke(true, true, state);
                            }
                            catch (Exception ex)
                            {
                                _trace.TraceError(ex.ToString());
                                callback.Invoke(false, false, state);
                            }
                            finally
                            {
                                sender2.Close();
                            }
                        }, null);
                    }
                    catch (Exception ex)
                    {
                        _trace.TraceError(ex.ToString());
                        sender.Close();
                        callback?.Invoke(false, false, state);
                    }
                }
                , null, "application/x-www-form-urlencoded", _get_xhr_param(), querystr);

            }
            catch (Exception ex)
            {
                _trace.TraceError(ex);
                throw;
            }
        }
        /// <summary>
        /// 异步移动单个文件
        /// </summary>
        /// <param name="source">原文件路径</param>
        /// <param name="destination">目标文件路径</param>
        /// <param name="callback">回调函数</param>
        /// <param name="ondup">同名覆盖方式</param>
        public void MovePathAsync(string source, string destination, OperationCallback callback, ondup ondup = ondup.overwrite, object state = null)
        {
            _trace.TraceInfo("BaiduPCS.MovePathAsync called, string source=" + source + ", string destination=" + destination + ", OperationCallback callback=" + callback.ToString() + ", ondup ondup=" + ondup.ToString());
            MovePathAsync(new string[] { source }, new string[] { destination }, callback, ondup, state);
        }
        /// <summary>
        /// 异步移动多个文件
        /// </summary>
        /// <param name="source">原文件路径</param>
        /// <param name="destination">目标文件路径</param>
        /// <param name="callback">回调函数</param>
        /// <param name="ondup">同名覆盖方式</param>
        public void MovePathAsync(IEnumerable<string> source, IEnumerable<string> destination, OperationCallback callback, ondup ondup = ondup.overwrite, object state = null)
        {
            _trace.TraceInfo("BaiduPCS.MovePathAsync called, IEnumerable<string> source=[count=" + source.Count() + "], IEnumerable<string> destination=[count=" + destination.Count() + "], OperationCallback callback=" + callback.ToString() + ", ondup ondup=" + ondup.ToString());

            if (source.Count() != destination.Count())
            {
                throw new InvalidOperationException("删除文件：源文件数量不等于目标文件数量");
            }


            var postArray = new JArray();
            foreach (var item in source)
            {
                var obj = new JObject();
                if (string.IsNullOrEmpty(item)) throw new ArgumentNullException("源文件名为空");
                obj.Add("path", item);
                postArray.Add(obj);
            }
            int index = 0;
            foreach (var item in destination)
            {
                var val_to_edit = postArray[index++] as JObject;
                var seperator_index = item.LastIndexOf('/');
                if (seperator_index == -1) throw new ArgumentException("找不到\"/\"：非法的路径");
                var parent_path = item.Substring(0, seperator_index);
                var file_name = item.Substring(seperator_index + 1);
                if (string.IsNullOrEmpty(file_name)) throw new ArgumentNullException("目标文件名为空");
                if (string.IsNullOrEmpty(parent_path))
                {
                    val_to_edit.Add("dest", "/");
                }
                else
                {
                    val_to_edit.Add("dest", parent_path);
                }
                val_to_edit.Add("newname", file_name);
                val_to_edit.Add("ondup", ondup.ToString());
            }

            var param = new Parameters();
            param.Add("opera", "move");
            param.Add("app_id", APPID);
            param.Add("bdstoken", _auth.bdstoken);
            param.Add("logid", GetLogid());

            var post_param = new Parameters();
            post_param.Add("filelist", JsonConvert.SerializeObject(postArray));

            var ns = new NetStream();
            ns.CookieKey = _auth.CookieIdentifier;

            var data = Encoding.UTF8.GetBytes(post_param.BuildQueryString());
            try
            {
                ns.HttpPostAsync(API_FILEMANAGER_URL, data.Length, (sender, e) =>
                {
                    //GetRequestStream
                    try
                    {
                        sender.RequestStream.Write(data, 0, data.Length);
                        sender.HttpPostResponseAsync((sender2, e2) =>
                        {
                            //GetResponseStream
                            try
                            {
                                var response = sender2.ReadResponseString();
                                _trace.TraceInfo(response);
                                var json = JsonConvert.DeserializeObject(response) as JObject;
                                _check_error(json);

                                callback?.Invoke(true, true, state);
                            }
                            catch (Exception ex)
                            {
                                _trace.TraceError(ex);
                                callback?.Invoke(false, false, state);
                            }
                            finally
                            {
                                sender2.Close();
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _trace.TraceError(ex);
                        sender.Close();
                        callback?.Invoke(false, false, state);
                    }
                }, null, NetStream.DEFAULT_CONTENT_TYPE_PARAM, _get_xhr_param(), param);
            }
            catch (Exception ex)
            {
                _trace.TraceError(ex);
                ns.Close();
                throw;
            }
        }
        /// <summary>
        /// 异步复制单个文件
        /// </summary>
        /// <param name="source">原文件路径</param>
        /// <param name="destination">目标文件路径</param>
        /// <param name="callback">回调函数</param>
        /// <param name="ondup">同名覆盖方式</param>
        public void CopyPathAsync(string source, string destination, OperationCallback callback, ondup ondup = ondup.overwrite, object state = null)
        {
            _trace.TraceInfo("BaiduPCS.CopyPathAsync called: string source=" + source + ", string destination=" + destination + ", OperationCallback callback=" + callback.ToString() + ", ondup ondup=" + ondup.ToString());
            CopyPathAsync(new string[] { source }, new string[] { destination }, callback, ondup, state);
        }
        /// <summary>
        /// 异步复制多个文件
        /// </summary>
        /// <param name="source">原文件路径</param>
        /// <param name="destination">目标文件路径</param>
        /// <param name="callback">回调函数</param>
        /// <param name="ondup">同名覆盖方式</param>
        public void CopyPathAsync(IEnumerable<string> source, IEnumerable<string> destination, OperationCallback callback, ondup ondup = ondup.overwrite, object state = null)
        {
            _trace.TraceInfo("BaiduPCS.CopyPathAsync called: IEnumerable<string> source=[count=" + source.Count() + "], IEnumerable<string> destination=[count=" + destination.Count() + "], OperationCallback callback=" + callback.ToString() + ", ondup ondup=" + ondup.ToString());

            if (source.Count() != destination.Count())
            {
                throw new InvalidOperationException("删除文件：源文件数量不等于目标文件数量");
            }


            var postArray = new JArray();
            foreach (var item in source)
            {
                var obj = new JObject();
                if (string.IsNullOrEmpty(item)) throw new ArgumentNullException("源文件名为空");
                obj.Add("path", item);
                postArray.Add(obj);
            }
            int index = 0;
            foreach (var item in destination)
            {
                var val_to_edit = postArray[index++] as JObject;
                var seperator_index = item.LastIndexOf('/');
                if (seperator_index == -1) throw new ArgumentException("找不到\"/\"：非法的路径");
                var parent_path = item.Substring(0, seperator_index);
                var file_name = item.Substring(seperator_index + 1);
                if (string.IsNullOrEmpty(file_name)) throw new ArgumentNullException("目标文件名为空");
                if (string.IsNullOrEmpty(parent_path))
                {
                    val_to_edit.Add("dest", "/");
                }
                else
                {
                    val_to_edit.Add("dest", parent_path);
                }
                val_to_edit.Add("newname", file_name);
                val_to_edit.Add("ondup", ondup.ToString());
            }

            var param = new Parameters();
            param.Add("opera", "copy");
            param.Add("app_id", APPID);
            param.Add("bdstoken", _auth.bdstoken);
            param.Add("logid", GetLogid());

            var post_param = new Parameters();
            post_param.Add("filelist", JsonConvert.SerializeObject(postArray));

            var ns = new NetStream();
            ns.CookieKey = _auth.CookieIdentifier;

            var data = Encoding.UTF8.GetBytes(post_param.BuildQueryString());
            try
            {
                ns.HttpPostAsync(API_FILEMANAGER_URL, data.Length, (sender, e) =>
                {
                    //GetRequestStream
                    try
                    {
                        sender.RequestStream.Write(data, 0, data.Length);
                        sender.HttpPostResponseAsync((sender2, e2) =>
                        {
                            //GetResponseStream
                            try
                            {
                                var response = sender2.ReadResponseString();
                                _trace.TraceInfo(response);
                                var json = JsonConvert.DeserializeObject(response) as JObject;
                                _check_error(json);

                                callback?.Invoke(true, true, state);
                            }
                            catch (Exception ex)
                            {
                                _trace.TraceError(ex);
                                callback?.Invoke(false, false, state);
                            }
                            finally
                            {
                                sender2.Close();
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _trace.TraceError(ex);
                        sender.Close();
                        callback?.Invoke(false, false, state);
                    }
                }, null, NetStream.DEFAULT_CONTENT_TYPE_PARAM, _get_xhr_param(), param);
            }
            catch (Exception ex)
            {
                _trace.TraceError(ex);
                ns.Close();
                throw;
            }
        }

        /// <summary>
        /// 异步重命名单个文件
        /// </summary>
        /// <param name="source">原文件路径</param>
        /// <param name="new_name">新文件名</param>
        /// <param name="callback">回调函数</param>
        public void RenameAsync(string source, string new_name, OperationCallback callback, object state = null)
        {
            _trace.TraceInfo("BaiduPCS.RenameAsync called: string source=" + source + ", string new_name=" + new_name + ", OperationCallback callback=" + callback.ToString());
            RenameAsync(new string[] { source }, new string[] { new_name }, callback, state);
        }
        /// <summary>
        /// 异步重命名多个文件
        /// </summary>
        /// <param name="source">原文件路径</param>
        /// <param name="new_name">新文件名</param>
        /// <param name="callback">回调函数</param>
        public void RenameAsync(IEnumerable<string> source, IEnumerable<string> new_name, OperationCallback callback, object state = null)
        {
            _trace.TraceInfo("BaiduPCS.RenameAsync called: string IEnumerable<string> source=[count=" + source.Count() + "], IEnumerable<string> new_name=[count=" + new_name.Count() + "], OperationCallback callback=" + callback.ToString());

            if (source.Count() != new_name.Count())
            {
                throw new InvalidOperationException("删除文件：源文件数量不等于目标文件数量");
            }


            var postArray = new JArray();
            foreach (var item in source)
            {
                var obj = new JObject();
                if (string.IsNullOrEmpty(item)) throw new ArgumentNullException("源文件名为空");
                obj.Add("path", item);
                postArray.Add(obj);
            }
            int index = 0;
            foreach (var item in new_name)
            {
                var val_to_edit = postArray[index++] as JObject;
                if (string.IsNullOrEmpty(item)) throw new ArgumentNullException("目标文件名为空");
                val_to_edit.Add("newname", item);
            }

            var param = new Parameters();
            param.Add("opera", "rename");
            param.Add("app_id", APPID);
            param.Add("bdstoken", _auth.bdstoken);
            param.Add("logid", GetLogid());

            var post_param = new Parameters();
            post_param.Add("filelist", JsonConvert.SerializeObject(postArray));

            var ns = new NetStream();
            ns.CookieKey = _auth.CookieIdentifier;

            var data = Encoding.UTF8.GetBytes(post_param.BuildQueryString());
            try
            {
                ns.HttpPostAsync(API_FILEMANAGER_URL, data.Length, (sender, e) =>
                {
                    //GetRequestStream
                    try
                    {
                        sender.RequestStream.Write(data, 0, data.Length);
                        sender.HttpPostResponseAsync((sender2, e2) =>
                        {
                            //GetResponseStream
                            try
                            {
                                var response = sender2.ReadResponseString();
                                _trace.TraceInfo(response);
                                var json = JsonConvert.DeserializeObject(response) as JObject;
                                _check_error(json);

                                callback?.Invoke(true, true, state);
                            }
                            catch (Exception ex)
                            {
                                _trace.TraceError(ex);
                                callback?.Invoke(false, false, state);
                            }
                            finally
                            {
                                sender2.Close();
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _trace.TraceError(ex);
                        sender.Close();
                        callback?.Invoke(false, false, state);
                    }
                }, null, NetStream.DEFAULT_CONTENT_TYPE_PARAM, _get_xhr_param(), param);
            }
            catch (Exception ex)
            {
                _trace.TraceError(ex);
                ns.Close();
                throw;
            }
        }
        /// <summary>
        /// 异步新建文件夹
        /// </summary>
        /// <param name="path">网盘路径</param>
        /// <param name="callback">回调函数</param>
        public void CreateDirectoryAsync(string path, ObjectMetaCallback callback, object state = null)
        {
            _trace.TraceInfo("BaiduPCS.CreateDirectoryAsync called: string path=" + path + ", ObjectMetaCallback callback=" + callback.ToString());
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");

            var post_param = new Parameters();
            post_param.Add("path", path);
            post_param.Add("isdir", 1);
            post_param.Add("block_list", "[]");

            var query_param = new Parameters();
            query_param.Add("a", "commit");
            query_param.Add("channel", "chunlei");
            query_param.Add("web", 1);
            query_param.Add("app_id", APPID);
            query_param.Add("bdstoken", _auth.bdstoken);
            query_param.Add("logid", GetLogid());
            query_param.Add("clienttype", 0);

            var ns = new NetStream();
            ns.CookieKey = _auth.CookieIdentifier;

            var post_data = Encoding.UTF8.GetBytes(post_param.BuildQueryString());
            try
            {
                ns.HttpPostAsync(API_CREATE_URL, post_data.Length, (sender, e) =>
                {
                    //GetRequestStream
                    try
                    {
                        sender.RequestStream.Write(post_data, 0, post_data.Length);
                        sender.HttpPostResponseAsync((sender2, e2) =>
                        {
                            try
                            {
                                var response = sender2.ReadResponseString();
                                _trace.TraceInfo(response);
                                var json = JsonConvert.DeserializeObject(response) as JObject;
                                _check_error(json);

                                var ret = new ObjectMetadata();
                                ret.FS_ID = json.Value<ulong>("fs_id");
                                ret.Path = json.Value<string>("path");
                                ret.ServerCTime = json.Value<ulong>("ctime");
                                ret.ServerMTime = json.Value<ulong>("mtime");
                                ret.ServerFileName = ret.Path.Substring(ret.Path.LastIndexOf('/') + 1);
                                ret.IsDir = json.Value<uint>("isdir") != 0;
                                ret.MD5 = string.Empty;

                                callback?.Invoke(true, ret, state);
                            }
                            catch (Exception ex)
                            {
                                _trace.TraceError(ex);
                                callback?.Invoke(false, new ObjectMetadata(), state);
                            }
                            finally
                            {
                                sender2.Close();
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _trace.TraceError(ex);
                        sender.Close();
                        callback?.Invoke(false, new ObjectMetadata(), state);
                    }
                }
                , null, NetStream.DEFAULT_CONTENT_TYPE_PARAM, _get_xhr_param(), query_param);
            }
            catch (Exception ex)
            {
                _trace.TraceError(ex);
                ns.Close();
                throw ex;
            }
        }

        /// <summary>
        /// 获取指定文件夹的所有文件/文件夹，失败时返回null
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="callback">回调函数</param>
        /// <param name="order">排序依据</param>
        /// <param name="asc">排序顺序(true为顺序，false为倒序)</param>
        /// <param name="page">页数</param>
        /// <param name="count">显示数量</param>
        public void GetFileListAsync(string path, MultiObjectMetaCallback callback, FileOrder order = FileOrder.name, bool asc = true, int page = 1, int count = 1000, object state = null)
        {
            _trace.TraceInfo("BaiduPCS.GetFileListAsync called: string path=" + path + ", MultiObjectMetaCallback callback=" + callback.ToString() + ", FileOrder order=" + order + ", bool asc=" + asc + ", int page=" + page + ", int count=" + count);
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");

            var param = new Parameters();
            param.Add("dir", path);
            param.Add("bdstoken", _auth.bdstoken);
            param.Add("logid", GetLogid());
            param.Add("order", order);
            param.Add("desc", asc ? 0 : 1);
            param.Add("app_id", APPID);
            param.Add("channel", "chunlei");
            param.Add("web", 1);
            param.Add("clienttype", 0);
            param.Add("page", page);
            param.Add("num", count);
            var ns = new NetStream();
            ns.CookieKey = _auth.CookieIdentifier;
            try
            {
                ns.HttpGetAsync(API_LIST_URL, (sender, e) =>
                {
                    //ResponseStream
                    try
                    {
                        var response_str = sender.ReadResponseString();
                        _trace.TraceInfo(response_str);

                        var json = JsonConvert.DeserializeObject(response_str) as JObject;
                        if (Math.Abs(json.Value<int>("errno")) == 9)
                        {
                            _trace.TraceWarning("该文件夹不存在，请检查文件路径");
                            callback?.Invoke(true, null, state);
                            return;
                        }
                        _check_error(json);

                        var list = json.Value<JArray>("list");
                        var ret = new List<ObjectMetadata>();
                        foreach (JObject item in list)
                        {
                            ret.Add(_read_json_meta(item));
                        }
                        callback?.Invoke(true, ret.ToArray(), state);

                    }
                    catch (Exception ex)
                    {
                        _trace.TraceError(ex);
                        callback?.Invoke(false, null, state);
                    }
                    finally
                    {
                        sender.Close();
                    }
                }, headerParam: _get_xhr_param(), urlParam: param);
            }
            catch (Exception ex)
            {
                _trace.TraceError(ex.ToString());
                ns.Close();
            }
        }
        /// <summary>
        /// 异步获取文件差异
        /// </summary>
        /// <param name="cursor">游标位置</param>
        /// <param name="callback">回调函数</param>
        public void GetFileDiffAsync(string cursor, FileDiffCallback callback, object state = null)
        {
            if (string.IsNullOrEmpty(cursor)) cursor = "null";
            _trace.TraceInfo("BaiduPCS.GetFileDiffAsync called: string cursor=" + cursor + "FileDiffCallback callback=" + callback.ToString());

            var param = new Parameters();
            param.Add("cursor", cursor);
            param.Add("channel", "chunlei");
            param.Add("web", 1);
            param.Add("app_id", APPID);
            param.Add("bdstoken", _auth.bdstoken);
            param.Add("logid", GetLogid());
            param.Add("clienttype", 0);

            var ns = new NetStream();
            ns.CookieKey = _auth.CookieIdentifier;
            try
            {
                ns.HttpGetAsync(API_FILEDIFF_URL, (sender, e) =>
                {
                    //GetResponseStream
                    try
                    {
                        var response = sender.ReadResponseString();
                        _trace.TraceInfo(response);
                        var json = JsonConvert.DeserializeObject(response) as JObject;
                        _check_error(json);

                        var next_cursor = json.Value<string>("cursor");
                        JObject entries = null;
                        if (json["entries"].GetType() == typeof(JObject)) entries = json.Value<JObject>("entries");
                        var has_more = json.Value<bool>("has_more");
                        var reset = json.Value<bool>("reset");
                        var ret_filelist = new List<ObjectMetadata>();
                        if (entries != null)
                            foreach (JProperty item in entries.Children())
                            {
                                ret_filelist.Add(_read_json_meta((JObject)item.Value));
                            }
                        callback?.Invoke(true, has_more, reset, next_cursor, ret_filelist.ToArray(), state);
                    }
                    catch (Exception ex)
                    {
                        _trace.TraceError(ex);
                        callback?.Invoke(false, false, false, null, null, state);
                    }
                    finally
                    {
                        sender.Close();
                    }
                }, null, _get_xhr_param(), param);
            }
            catch (Exception ex)
            {
                _trace.TraceError(ex);
                ns.Close();
                throw ex;
            }
        }
        #endregion

        #region file upload
        /// <summary>
        /// 秒传文件
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="content_length">文件大小</param>
        /// <param name="content_md5">文件总体MD5</param>
        /// <param name="content_crc">文件总体CRC32 (Hex数值)</param>
        /// <param name="slice_md5">前256K验证段的MD5</param>
        /// <param name="callback">回调函数</param>
        /// <param name="ondup">同名覆盖方式</param>
        public void RapidUploadAsync(string path, ulong content_length, string content_md5, string content_crc, string slice_md5, ObjectMetaCallback callback, ondup ondup = ondup.overwrite, object state = null)
        {
            _trace.TraceInfo("BaiduPCS.RapidUploadAsync called: string path=" + path + ", ulong content_length=" + content_length + ", string content_md5=" + content_md5 + ", string content_crc=" + content_crc + ", string slice_md5=" + slice_md5 + ", ObjectMetaCallback callback = " + callback.ToString() + ", ondup ondup=" + ondup);
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
                ns.HttpPostAsync(PCS_FILE_URL, 0, (sender, e) =>
                {
                    //GetRequestStream
                    try
                    {
                        ns.HttpPostResponseAsync((sender2, e2) =>
                        {
                            //GetResponseStream
                            try
                            {
                                var response = sender2.ReadResponseString();
                                _trace.TraceInfo(response);
                                var json = JsonConvert.DeserializeObject(response) as JObject;
                                _check_error(json);

                                ObjectMetadata ret = new ObjectMetadata();
                                ret.Path = json.Value<string>("path");
                                ret.Size = json.Value<ulong>("size");
                                ret.ServerCTime = json.Value<ulong>("ctime");
                                ret.ServerMTime = json.Value<ulong>("mtime");
                                ret.LocalCTime = ret.ServerCTime;
                                ret.LocalMTime = ret.ServerMTime;
                                ret.MD5 = json.Value<string>("md5");
                                ret.FS_ID = json.Value<ulong>("fs_id");
                                ret.IsDir = json.Value<int>("isdir") != 0;
                                ret.ServerFileName = ret.Path.Substring(ret.Path.LastIndexOf('/') + 1);
                                callback?.Invoke(true, ret, state);
                            }
                            catch (WebException ex)
                            {
                                //ignoring 404 status
                                if (ex.Response == null || ((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.NotFound)
                                {
                                    _trace.TraceError(ex);
                                    callback?.Invoke(false, new ObjectMetadata(), state);
                                }
                            }
                            catch (Exception ex)
                            {
                                _trace.TraceError(ex);
                                callback?.Invoke(false, new ObjectMetadata(), state);
                            }
                            finally
                            {
                                sender2.Close();
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _trace.TraceError(ex);
                        sender.Close();
                        callback?.Invoke(false, new ObjectMetadata(), state);
                    }
                }, null, "text/html", _get_xhr_param(), param);
            }
            catch (Exception ex)
            {
                _trace.TraceError(ex);
                throw ex;
            }
        }


        //一个保存临时上传数据的结构
        private struct _upload_data
        {
            public NetStream stream;
            public string boundary;
        }
        //分配的上传guid表
        private static Dictionary<Guid, _upload_data> _upload_queue = new Dictionary<Guid, _upload_data>();
        //多线程的互斥锁
        private static object _upload_external_lock = new object();
        //关闭所有上传的链接并且清空上传列表
        private static void _clear_upload_queue()
        {
            lock (_upload_external_lock)
            {
                foreach (var item in _upload_queue)
                {
                    item.Value.stream.Close();
                }
                _upload_queue.Clear();
            }
        }

        /// <summary>
        /// 异步上传文件（无分段和续传），上传完成后请调用UploadEndAsync获取信息
        /// </summary>
        /// <param name="content_length">文件长度</param>
        /// <param name="path">网盘文件路径</param>
        /// <param name="callback">回调函数，包含任务分配的id和网络数据流</param>
        /// <param name="ondup">同名覆盖方式</param>
        public void UploadBeginAsync(ulong content_length, string path, UploadCallback callback, ondup ondup = ondup.overwrite, object state = null)
        {
            _trace.TraceInfo("BaiduPCS.UploadBeginAsync called: ulong content_length=" + content_length + ", string path=" + path + ", UploadCallback callback=" + callback.ToString() + ", ondup ondup=" + ondup.ToString());
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");

            var param = new Parameters();
            param.Add("method", "upload");
            param.Add("app_id", APPID);
            param.Add("path", path);
            param.Add("ondup", ondup);
            param.Add("logid", GetLogid());
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
                ns.HttpPostAsync(PCS_FILE_URL, total_length, (sender, e) =>
                {
                    //GetRequestStream
                    try
                    {
                        sender.RequestStream.Write(head_bytes, 0, head_bytes.Length);
                        var guid = Guid.NewGuid();
                        var data = new _upload_data { stream = sender, boundary = boundary };
                        _upload_queue.Add(guid, data);

                        callback?.Invoke(true, guid, sender.RequestStream, state);
                    }
                    catch (Exception ex)
                    {
                        _trace.TraceError(ex);
                        sender.Close();
                        callback?.Invoke(false, Guid.Empty, null, state);
                    }
                }, null, "multipart/form-data; boundary=" + boundary, _get_xhr_param(), param);
            }
            catch (Exception ex)
            {
                _trace.TraceError(ex);
                ns.Close();
                throw ex;
            }
        }

        /// <summary>
        /// 异步结束上传文件
        /// </summary>
        /// <param name="task_id">分配到的任务id</param>
        /// <param name="callback">回调函数</param>
        public void UploadEndAsync(Guid task_id, ObjectMetaCallback callback, object state = null)
        {
            _trace.TraceInfo("BaiduPCS.UploadEndAsync called: Guid task_id=" + task_id.ToString() + ", ObjectMetaCallback callback=" + callback.ToString());

            _upload_data data;
            lock (_upload_external_lock)
            {
                if (task_id == Guid.Empty || !_upload_queue.ContainsKey(task_id)) return;
                data = _upload_queue[task_id];
                _upload_queue.Remove(task_id);
            }

            var foot = util.GenerateFormDataEnding(data.boundary);
            var foot_bytes = Encoding.UTF8.GetBytes(foot);

            var ns = data.stream;
            try
            {
                ns.RequestStream.Write(foot_bytes, 0, foot_bytes.Length);
                ns.HttpPostResponseAsync((sender, e) =>
                {
                    //GetResponseStream
                    try
                    {
                        var ret = new ObjectMetadata();
                        var response = sender.ReadResponseString();
                        _trace.TraceInfo(response);
                        var json = JsonConvert.DeserializeObject(response) as JObject;
                        _check_error(json);

                        ret.Path = json.Value<string>("path");
                        ret.Size = json.Value<ulong>("size");
                        ret.LocalCTime = json.Value<ulong>("ctime");
                        ret.LocalMTime = json.Value<ulong>("mtime");
                        ret.ServerCTime = ret.LocalCTime;
                        ret.ServerMTime = ret.LocalMTime;
                        ret.MD5 = json.Value<string>("md5");
                        ret.FS_ID = json.Value<ulong>("fs_id");
                        ret.IsDir = json.Value<int>("isdir") != 0;
                        ret.ServerFileName = ret.Path.Substring(ret.Path.LastIndexOf('/') + 1);

                        callback?.Invoke(true, ret, state);
                    }
                    catch (Exception ex)
                    {
                        _trace.TraceError(ex);
                        callback?.Invoke(false, new ObjectMetadata(), state);
                    }
                    finally
                    {
                        sender.Close();
                    }
                });
            }
            catch (Exception ex)
            {
                _trace.TraceError(ex);
                ns.Close();
                throw ex;
            }
        }

        /// <summary>
        /// 异步预创建文件，用于分段上传
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="block_count">分段数量（以4mb为一个分段）</param>
        /// <param name="callback">回调函数</param>
        public void PreCreateFileAsync(string path, int block_count, PreCreateCallback callback, object state = null)
        {
            _trace.TraceInfo("BaiduPCS.PreCreateFileAsync called: string path=" + path + ", int block_count=" + block_count + ", PreCreateCallback callback=" + callback.ToString());

            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");
            //if (block_count == 0) throw new IndexOutOfRangeException("block_count必须为大于0的正整数");

            var ns = new NetStream();
            ns.CookieKey = _auth.CookieIdentifier;
            var query_param = new Parameters();

            query_param.Add("channel", "chunlei");
            query_param.Add("web", 1);
            query_param.Add("app_id", APPID);
            query_param.Add("bdstoken", _auth.bdstoken);
            query_param.Add("logid", GetLogid());
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

            var post_bytes = Encoding.UTF8.GetBytes(post_data.BuildQueryString());

            try
            {
                ns.HttpPostAsync(API_PRECREATE_URL, post_bytes.Length, (sender, e) =>
                {
                    //GetRequestStream
                    try
                    {
                        sender.RequestStream.Write(post_bytes, 0, post_bytes.Length);
                        sender.HttpPostResponseAsync((sender2, e2) =>
                        {
                            //GetResponseStream
                            try
                            {
                                var response = ns.ReadResponseString();

                                _trace.TraceInfo(response);
                                var json = JsonConvert.DeserializeObject(response) as JObject;
                                _check_error(json);

                                //var block_count2 = json.Value<JArray>("block_list").Count;
                                var block_count2 = block_count;
                                var return_type = json.Value<int>("return_type");
                                var upload_id2 = json.Value<string>("uploadid");

                                callback?.Invoke(true, block_count2, upload_id2, state);
                            }
                            catch (Exception ex)
                            {
                                _trace.TraceError(ex);
                                callback?.Invoke(false, 0, null, state);
                            }
                            finally
                            {
                                sender2.Close();
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _trace.TraceError(ex);
                        sender.Close();
                        callback?.Invoke(false, 0, null, state);
                    }
                }, null, NetStream.DEFAULT_CONTENT_TYPE_PARAM, _get_xhr_param(), query_param);
            }
            catch (Exception ex)
            {
                _trace.TraceError(ex);
                ns.Close();
                throw ex;
            }
        }

        private struct _upload_data2
        {
            public string boundary;
            public string uploadid;
            public NetStream stream;
            public int index;
        }
        private static Dictionary<Guid, _upload_data2> _slice_upload_queue = new Dictionary<Guid, _upload_data2>();
        private static object _slice_upload_external_lock = new object();
        private static void _clear_slice_upload_queue()
        {
            lock (_slice_upload_external_lock)
            {
                foreach (var item in _slice_upload_queue)
                {
                    item.Value.stream.Close();
                }
                _slice_upload_queue.Clear();
            }
        }

        /// <summary>
        /// 异步上传分段数据，上传完成后请调用UploadSliceEndAsync获取信息
        /// </summary>
        /// <param name="content_length">分段数据长度（不大于UPLOAD_SLICE_SIZE）</param>
        /// <param name="path">文件路径</param>
        /// <param name="uploadid">上传id</param>
        /// <param name="sequence">文件序号</param>
        /// <param name="callback">回调函数，包含任务分配的id和网络数据流</param>
        public void UploadSliceBeginAsync(ulong content_length, string path, string uploadid, int sequence, UploadCallback callback, object state = null)
        {
            _trace.TraceInfo("BaiduPCS.UploadSliceBeginAync called: ulong content_length=" + content_length + ", string path=" + path + ", string uploadid=" + uploadid + ", int sequence=" + sequence + ", SliceUploadCallback callback=" + callback.ToString());
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");
            if (string.IsNullOrEmpty(uploadid)) throw new ArgumentNullException("uploadid");

            var query_param = new Parameters();
            query_param.Add("method", "upload");
            query_param.Add("app_id", APPID);
            query_param.Add("channel", "chunlei");
            query_param.Add("clienttype", 1);
            query_param.Add("web", 1);
            query_param.Add("BDUSS", _auth.bduss);
            query_param.Add("logid", GetLogid());
            query_param.Add("path", path);
            query_param.Add("uploadid", uploadid);
            query_param.Add("partseq", sequence);

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
                ns.HttpPostAsync(PCS_SUPERFILE_URL, total_length, (sender, e) =>
                {
                    //GetRequestStream
                    try
                    {
                        sender.RequestStream.Write(head_bytes, 0, head_bytes.Length);
                        var guid = Guid.NewGuid();
                        var data = new _upload_data2 { stream = sender, boundary = boundary, uploadid = uploadid, index = sequence };
                        _slice_upload_queue.Add(guid, data);

                        callback?.Invoke(true, guid, sender.RequestStream, state);
                    }
                    catch (Exception ex)
                    {
                        _trace.TraceError(ex);
                        sender.Close();
                        callback?.Invoke(false, Guid.Empty, null, state);
                    }
                }, null, "multipart/form-data; boundary=" + boundary, _get_xhr_param(), query_param);
            }
            catch (Exception ex)
            {
                _trace.TraceError(ex);
                ns.Close();
                throw ex;
            }
        }

        /// <summary>
        /// 异步结束上传分段数据
        /// </summary>
        /// <param name="task_id">分配到的任务id</param>
        /// <param name="callback">回调函数</param>
        public void UploadSliceEndAsync(Guid task_id, SliceUploadCallback callback, object state = null)
        {
            _trace.TraceInfo("BaiduPCS.UploadSliceEndAsync called: Guid task_id=" + task_id.ToString() + ", SliceUploadCallback callback=" + callback.ToString());

            _upload_data2 data;
            lock (_upload_external_lock)
            {
                if (task_id == Guid.Empty || !_slice_upload_queue.ContainsKey(task_id)) return;
                data = _slice_upload_queue[task_id];
                _slice_upload_queue.Remove(task_id);
            }

            var foot = util.GenerateFormDataEnding(data.boundary);
            var foot_bytes = Encoding.UTF8.GetBytes(foot);

            var ns = data.stream;
            try
            {
                ns.RequestStream.Write(foot_bytes, 0, foot_bytes.Length);
                ns.HttpPostResponseAsync((sender, e) =>
                {
                    //GetResponseStream
                    try
                    {
                        string ret = null;
                        var response = sender.ReadResponseString();
                        _trace.TraceInfo(response);
                        var json = JsonConvert.DeserializeObject(response) as JObject;
                        _check_error(json);

                        ret = json.Value<string>("md5");

                        callback?.Invoke(true, ret, state);
                    }
                    catch (Exception ex)
                    {
                        _trace.TraceError(ex);
                        callback?.Invoke(false, null, state);
                    }
                    finally
                    {
                        sender.Close();
                    }
                });
            }
            catch (Exception ex)
            {
                _trace.TraceError(ex);
                ns.Close();
                throw ex;
            }
        }

        /// <summary>
        /// 异步合并分段数据（注意：分段数大于1时返回的MD5值有很大几率是错误的）
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="uploadid">上传id</param>
        /// <param name="block_list">分段数据的MD5值</param>
        /// <param name="file_size">文件大小</param>
        /// <param name="callback">回调函数</param>
        public void CreateSuperFileAsync(string path, string uploadid, IEnumerable<string> block_list, ulong file_size, ObjectMetaCallback callback, object state = null)
        {
            _trace.TraceInfo("BaiduPCS.CreateSuperFileAsync called: string path=" + path + ", string uploadid=" + uploadid + ", IEnumerable<string> block_list=[count=" + block_list.Count() + "], ulong file_size=" + file_size + ", ObjectMetaCallback callback=" + callback.ToString());
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");
            if (string.IsNullOrEmpty(uploadid)) throw new ArgumentNullException("uploadid");
            if (block_list == null) throw new ArgumentNullException("block_list");
            if (block_list.Count() == 0) throw new ArgumentOutOfRangeException("分段个数为0");
            if (file_size <= 0) throw new ArgumentOutOfRangeException("文件大小必须大于0");

            var ret = new ObjectMetadata();

            var query_param = new Parameters();
            query_param.Add("isdir", 0);
            query_param.Add("channel", "chunlei");
            query_param.Add("web", 1);
            query_param.Add("app_id", APPID);
            query_param.Add("bdstoken", _auth.bdstoken);
            query_param.Add("logid", GetLogid());
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

            var post_data = Encoding.UTF8.GetBytes(post_param.BuildQueryString());
            var ns = new NetStream();
            ns.CookieKey = _auth.CookieIdentifier;
            try
            {
                ns.HttpPostAsync(API_CREATE_URL, post_data.Length, (sender, e) =>
                {
                    //GetRequestStream
                    try
                    {
                        sender.RequestStream.Write(post_data, 0, post_data.Length);
                        sender.HttpPostResponseAsync((sender2, e2) =>
                        {
                            //GetResponseStream
                            try
                            {
                                var response = sender2.ReadResponseString();
                                _trace.TraceInfo(response);
                                var json = JsonConvert.DeserializeObject(response) as JObject;
                                _check_error(json);

                                ret.ServerCTime = json.Value<ulong>("ctime");
                                ret.ServerMTime = json.Value<ulong>("mtime");
                                ret.LocalCTime = ret.ServerCTime;
                                ret.LocalMTime = ret.ServerMTime;
                                ret.FS_ID = json.Value<ulong>("fs_id");
                                ret.IsDir = json.Value<int>("isdir") != 0;
                                ret.MD5 = json.Value<string>("md5");
                                ret.Path = json.Value<string>("path");
                                ret.Size = json.Value<ulong>("size");
                                ret.ServerFileName = ret.Path.Split('/').Last();

                                callback?.Invoke(true, ret, state);
                            }
                            catch (Exception ex)
                            {
                                _trace.TraceError(ex);
                                callback?.Invoke(false, new ObjectMetadata(), state);
                            }
                            finally
                            {
                                sender2.Close();
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _trace.TraceError(ex);
                        sender.Close();
                        callback?.Invoke(false, new ObjectMetadata(), state);
                    }
                }, null, NetStream.DEFAULT_CONTENT_TYPE_PARAM, _get_xhr_param(), query_param);
            }
            catch (Exception ex)
            {
                _trace.TraceError(ex);
                ns.Close();
                throw ex;
            }
        }
        #endregion

        #region file download
        /// <summary>
        /// PCS API for download，返回的只是带参数的url地址（不用发送任何http请求）
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="callback"> 回调函数</param>
        /// <param name="over_https">是否使用https</param>
        public void GetDownloadLinkAPIAsync(string path, DownloadLinkCallback callback, bool over_https = true, object state = null)
        {
            _trace.TraceInfo("BaiduPCS.GetDownloadLinkAPIAsync called: string path=" + path + ", DownloadLinkCallback callback=" + callback.ToString() + ", bool over_https=" + over_https);
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
            ThreadPool.QueueUserWorkItem(delegate
            {
                callback?.Invoke(true, new string[] { ret_url }, state);
            });
        }
        /// <summary>
        /// 异步获取下载url
        /// </summary>
        /// <param name="fs_id">FS_ID</param>
        /// <param name="callback">回调函数</param>
        /// <param name="over_https">是否使用https</param>
        public void GetDownloadLinkAsync(ulong fs_id, DownloadLinkCallback callback, bool over_https = true, object state = null)
        {
            _trace.TraceInfo("BaiduPCS.GetDownloadLinkAsync called: ulong fs_id=" + fs_id + ", DownloadLinkCallback callback=" + callback.ToString() + ", bool over_https=" + over_https);

            if (fs_id == 0) throw new ArgumentNullException("FS_ID不能为0");

            var param = new Parameters();
            var ns = new NetStream();
            ns.CookieKey = _auth.CookieIdentifier;

            param.Add("sign", _auth.sign2);
            param.Add("timestamp", _auth.timestamp);
            param.Add("fidlist", "[" + fs_id + "]");
            param.Add("type", "dlink");
            param.Add("channel", "chunlei");
            param.Add("web", "1");
            param.Add("app_id", APPID);
            param.Add("bdstoken", _auth.bdstoken);
            param.Add("logid", GetLogid());
            param.Add("clienttype", "0");

            try
            {
                ns.HttpGetAsync(API_DOWNLOAD_URL, (sender, e) =>
                {
                    //GetResponseStream
                    try
                    {
                        var response = sender.ReadResponseString();
                        _trace.TraceInfo(response);
                        var json = JsonConvert.DeserializeObject(response) as JObject;
                        _check_error(json);

                        var dlink_array = json.Value<JArray>("dlink");
                        if (dlink_array.Count == 0)
                        {
                            callback?.Invoke(true, null, state);
                            return;
                        }
                        var dlink = (dlink_array[0] as JObject).Value<string>("dlink");

                        if (dlink.StartsWith("https") && !over_https) dlink = "http" + dlink.Substring(5);
                        else if (dlink.StartsWith("http") && !dlink.StartsWith("https") && over_https) dlink = "https" + dlink.Substring(4);

                        callback?.Invoke(true, new string[] { dlink }, state);
                    }
                    catch (Exception ex)
                    {
                        _trace.TraceError(ex);
                        callback?.Invoke(false, null, state);
                    }
                    finally
                    {
                        sender.Close();
                    }
                }, null, _get_xhr_param(), param);
            }
            catch (Exception ex)
            {
                _trace.TraceError(ex);
                ns.Close();
                throw ex;
            }
        }

        /// <summary>
        /// 异步获取多个下载url
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="callback">回调函数</param>
        public void GetLocateDownloadLinkAsync(string path, DownloadLinkCallback callback, object state = null)
        {
            _trace.TraceInfo("BaiduPCS.GetLocateDownloadLinkAsync called: string path=" + path + ", DownloadLinkCallback callback=" + callback.ToString());
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");

            var ns = new NetStream();
            ns.CookieKey = _auth.CookieIdentifier;
            var param = new Parameters();
            param.Add("method", "locatedownload");
            param.Add("app_id", APPID);
            param.Add("ver", "4.0");
            param.Add("path", path);

            try
            {
                ns.HttpPostAsync(PCS_FILE_URL, 0, (sender, e) =>
                {
                    //GetRequestStream
                    try
                    {
                        ns.HttpPostResponseAsync((sender2, e2) =>
                        {
                            //GetResponseStream
                            try
                            {
                                var response = ns.ReadResponseString();
                                _trace.TraceInfo(response);
                                var json = JsonConvert.DeserializeObject(response) as JObject;
                                _check_error(json);

                                var ret_list = new List<string>();
                                var download_urls = json.Value<JArray>("urls");
                                foreach (JObject item in download_urls)
                                {
                                    ret_list.Add(item.Value<string>("url"));
                                }
                                callback?.Invoke(true, ret_list.ToArray(), state);
                            }
                            catch (Exception ex)
                            {
                                _trace.TraceError(ex);
                                callback?.Invoke(false, null, state);
                            }
                            finally
                            {
                                sender2.Close();
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _trace.TraceError(ex);
                        sender.Close();
                        callback?.Invoke(false, null, state);
                    }
                }, null, "text/html", _get_xhr_param(), param);
            }
            catch (Exception ex)
            {
                _trace.TraceError(ex);
                ns.Close();
                throw ex;
            }
        }

        #endregion

        #region sharing

        /// <summary>
        /// 异步创建单文件（夹）公开分享
        /// </summary>
        /// <param name="fs_id">FS ID</param>
        /// <param name="callback">回调函数</param>
        /// <param name="expire_time">有效时间（可选：1-1天，7-7天，0-永久）</param>
        public void CreatePublicShareAsync(ulong fs_id, ShareMetaCallback callback, int expire_time = 0)
        {
            _trace.TraceInfo("BaiduPCS.CreatePublicShareAsync called: ulong fs_id=" + fs_id + ", ShareMetaCallback callback=" + callback.ToString() + ", int expire_time=" + expire_time);
            CreatePublicShareAsync(new ulong[] { fs_id }, callback, expire_time);
        }
        /// <summary>
        /// 异步创建多文件（夹）公开分享
        /// </summary>
        /// <param name="fs_id">FS ID</param>
        /// <param name="callback">回调函数</param>
        /// <param name="expire_time">有效时间（可选：1-1天，7-7天，0-永久）</param>
        public void CreatePublicShareAsync(IEnumerable<ulong> fs_ids, ShareMetaCallback callback, int expire_time = 0, object state = null)
        {
            _trace.TraceInfo("BaiduPCS.CreatePublicShareAsync called: IEnumerable<ulong> fs_ids=[count=" + fs_ids.Count() + "], ShareMetaCallback callback=" + callback.ToString() + ", int expire_time=" + expire_time);

            if (expire_time != 0 && expire_time != 1 && expire_time != 7) throw new ArgumentOutOfRangeException("expire_time数值只能为0，1或7");
            var ns = new NetStream();
            ns.CookieKey = _auth.CookieIdentifier;
            var xhr_param = _get_xhr_param();
            var query_param = new Parameters();
            query_param.Add("channel", "chunlei");
            query_param.Add("clienttype", 0);
            query_param.Add("web", 1);
            query_param.Add("app_id", APPID);
            query_param.Add("bdstoken", _auth.bdstoken);
            query_param.Add("logid", GetLogid());

            var post_param = new Parameters();
            var fid_list = new JArray();
            foreach (var item in fs_ids)
            {
                fid_list.Add(item);
            }
            post_param.Add("fid_list", JsonConvert.SerializeObject(fid_list));
            post_param.Add("schannel", 0);
            post_param.Add("channel_list", "[]");
            post_param.Add("period", expire_time);

            var post_data = Encoding.UTF8.GetBytes(post_param.BuildQueryString());
            try
            {
                ns.HttpPostAsync(API_SHARE_SET_URL, post_data.Length, (sender, e) =>
                {
                    try
                    {
                        sender.RequestStream.Write(post_data, 0, post_data.Length);
                        sender.HttpPostResponseAsync((sender2, e2) =>
                        {
                            try
                            {
                                var response = ns.ReadResponseString();
                                _trace.TraceInfo(response);
                                var json = JsonConvert.DeserializeObject(response) as JObject;
                                _check_error(json);

                                var ret = new ShareData();
                                ret.CreateTime = json.Value<ulong>("ctime");
                                ret.ExpiredType = json.Value<int>("expireType");
                                ret.Link = json.Value<string>("link");
                                ret.Premis = json.Value<bool>("premis");
                                ret.ShareID = json.Value<ulong>("shareid");
                                ret.ShortURL = json.Value<string>("shorturl");
                                callback?.Invoke(true, ret, state);
                            }
                            catch (Exception ex)
                            {
                                _trace.TraceError(ex);
                                callback?.Invoke(false, new ShareData(), state);
                            }
                            finally
                            {
                                sender2.Close();
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _trace.TraceError(ex);
                        sender.Close();
                        callback?.Invoke(false, new ShareData(), state);
                    }
                }, null, NetStream.DEFAULT_CONTENT_TYPE_PARAM, _get_xhr_param(), query_param);
            }
            catch (Exception ex)
            {
                _trace.TraceError(ex);
                ns.Close();
                throw ex;
            }
        }

        /// <summary>
        /// 异步创建单文件（夹）加密分享
        /// </summary>
        /// <param name="fs_id">FS ID</param>
        /// <param name="password">分享密码</param>
        /// <param name="callback">回调函数</param>
        /// <param name="expire_time">有效时间（可选：1-1天，7-7天，0-永久）</param>
        public void CreatePrivateShareAsync(ulong fs_id, string password, ShareMetaCallback callback, int expire_time = 0, object state = null)
        {
            _trace.TraceInfo("BaiduPCS.CreatePrivateShareAsync called: ulong fs_id=" + fs_id + ", string password=" + password + ", ShareMetaCallback callback=" + callback.ToString() + ", int expire_time=" + expire_time);
            CreatePrivateShareAsync(new ulong[] { fs_id }, password, callback, expire_time, state);
        }
        /// <summary>
        /// 异步创建多文件（夹）加密分享
        /// </summary>
        /// <param name="fs_id">FS ID</param>
        /// <param name="password">分享密码</param>
        /// <param name="callback">回调函数</param>
        /// <param name="expire_time">有效时间（可选：1-1天，7-7天，0-永久）</param>
        public void CreatePrivateShareAsync(IEnumerable<ulong> fs_ids, string password, ShareMetaCallback callback, int expire_time = 0, object state = null)
        {
            _trace.TraceInfo("BaiduPCS.CreatePrivateShareAsync called: IEnumerable<ulong> fs_ids=[count=" + fs_ids.Count() + "], string password=" + password + ", ShareMetaCallback callback=" + callback.ToString() + ", int expire_time=" + expire_time);

            if (expire_time != 0 && expire_time != 1 && expire_time != 7) throw new ArgumentOutOfRangeException("expire_time数值只能为0，1或7");
            var ns = new NetStream();
            ns.CookieKey = _auth.CookieIdentifier;
            var xhr_param = _get_xhr_param();
            var query_param = new Parameters();
            query_param.Add("channel", "chunlei");
            query_param.Add("clienttype", 0);
            query_param.Add("web", 1);
            query_param.Add("app_id", APPID);
            query_param.Add("bdstoken", _auth.bdstoken);
            query_param.Add("logid", GetLogid());

            var post_param = new Parameters();
            var fid_list = new JArray();
            foreach (var item in fs_ids)
            {
                fid_list.Add(item);
            }
            post_param.Add("fid_list", JsonConvert.SerializeObject(fid_list));
            post_param.Add("schannel", 4);
            post_param.Add("channel_list", "[]");
            post_param.Add("period", expire_time);
            post_param.Add("pwd", password);

            var post_data = Encoding.UTF8.GetBytes(post_param.BuildQueryString());
            try
            {
                ns.HttpPostAsync(API_SHARE_SET_URL, post_data.Length, (sender, e) =>
                {
                    try
                    {
                        sender.RequestStream.Write(post_data, 0, post_data.Length);
                        sender.HttpPostResponseAsync((sender2, e2) =>
                        {
                            try
                            {
                                var response = ns.ReadResponseString();
                                _trace.TraceInfo(response);
                                var json = JsonConvert.DeserializeObject(response) as JObject;
                                _check_error(json);

                                var ret = new ShareData();
                                ret.CreateTime = json.Value<ulong>("ctime");
                                ret.ExpiredType = json.Value<int>("expireType");
                                ret.Link = json.Value<string>("link");
                                ret.Premis = json.Value<bool>("premis");
                                ret.ShareID = json.Value<ulong>("shareid");
                                ret.ShortURL = json.Value<string>("shorturl");
                                callback?.Invoke(true, ret, state);
                            }
                            catch (Exception ex)
                            {
                                _trace.TraceError(ex);
                                callback?.Invoke(false, new ShareData(), state);
                            }
                            finally
                            {
                                sender2.Close();
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _trace.TraceError(ex);
                        sender.Close();
                        callback?.Invoke(false, new ShareData(), state);
                    }
                }, null, NetStream.DEFAULT_CONTENT_TYPE_PARAM, _get_xhr_param(), query_param);
            }
            catch (Exception ex)
            {
                _trace.TraceError(ex);
                ns.Close();
                throw ex;
            }
        }
        /// <summary>
        /// 异步取消分享
        /// </summary>
        /// <param name="share_id">Share ID</param>
        /// <param name="callback">回调函数</param>
        public void CancelShareAsync(ulong share_id, OperationCallback callback, object state = null)
        {
            _trace.TraceInfo("BaiduPCS.CancelShareAsync called: ulong share_id=" + share_id + ", OperationCallback callback=" + callback.ToString());
            CancelShareAsync(new ulong[] { share_id }, callback, state);
        }
        /// <summary>
        /// 异步取消多个分享
        /// </summary>
        /// <param name="share_ids">Share ID</param>
        /// <param name="callback">回调函数</param>
        public void CancelShareAsync(IEnumerable<ulong> share_ids, OperationCallback callback, object state = null)
        {
            _trace.TraceInfo("BaiduPCS.CancelShareAsync called: IEnumerable<ulong> share_ids=[count=" + share_ids.Count() + "], OperationCallback callback=" + callback.ToString());
            var ns = new NetStream();
            ns.CookieKey = _auth.CookieIdentifier;
            var xhr_param = _get_xhr_param();
            var query_param = new Parameters();
            query_param.Add("channel", "chunlei");
            query_param.Add("clienttype", 0);
            query_param.Add("web", 1);
            query_param.Add("bdstoken", _auth.bdstoken);
            query_param.Add("app_id", APPID);
            query_param.Add("logid", GetLogid());

            var post_body = new Parameters();
            var shareid_list = new JArray();
            foreach (var item in share_ids)
            {
                shareid_list.Add(item);
            }
            post_body.Add("shareid_list", JsonConvert.SerializeObject(shareid_list));

            var post_data = Encoding.UTF8.GetBytes(post_body.BuildQueryString());
            try
            {
                ns.HttpPostAsync(API_SHARE_CANCEL_URL, post_data.Length, (sender, e) =>
                {
                    try
                    {
                        ns.RequestStream.Write(post_data, 0, post_data.Length);
                        ns.HttpPostResponseAsync((sender2, e2) =>
                        {
                            var response = ns.ReadResponseString();
                            _trace.TraceInfo(response);
                            var json = JsonConvert.DeserializeObject(response) as JObject;
                            _check_error(json);

                            callback?.Invoke(true, true, state);
                        });
                    }
                    catch (Exception ex)
                    {
                        _trace.TraceError(ex);
                        sender.Close();
                        callback?.Invoke(false, false, state);
                    }
                }, null, NetStream.DEFAULT_CONTENT_TYPE_PARAM, _get_xhr_param(), query_param);
            }
            catch (Exception ex)
            {
                _trace.TraceError(ex);
                ns.Close();
                throw ex;
            }
        }

        public void GetShareRecordsAsync(ShareMultiMetaCallback callback, object state = null)
        {
            _trace.TraceInfo("BaiduPCS.GetShareRecordsAsync called: ShareMultiMetaCallback callback=" + callback.ToString());
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
            query_param.Add("bdstoken", _auth.bdstoken);
            query_param.Add("app_id", APPID);
            query_param.Add("logid", GetLogid());

            var ns = new NetStream();
            ns.CookieKey = _auth.CookieIdentifier;
            try
            {
                ns.HttpGetAsync(API_SHARE_RECORD_URL, (sender, e) =>
                {
                    try
                    {
                        var response = sender.ReadResponseString();
                        _trace.TraceInfo(response);
                        var json = JsonConvert.DeserializeObject(response) as JObject;
                        _check_error(json);

                        var ret = new List<ShareRecord>();

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
                        callback?.Invoke(true, ret.ToArray(), state);
                    }
                    catch (Exception ex)
                    {
                        _trace.TraceError(ex);
                        callback?.Invoke(false, null, state);
                    }
                    finally
                    {
                        sender.Close();
                    }
                }, null, _get_xhr_param(), query_param);
            }
            catch (Exception ex)
            {
                _trace.TraceError(ex);
                ns.Close();
                throw ex;
            }
        }
        #endregion
    } //end class
} //end namespace
