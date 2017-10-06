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
    public partial class BaiduPCS
    {
        //回调函数的类型声明

        public delegate void QuotaCallback(bool success, Quota result);
        public delegate void OperationCallback(bool success, bool result);
        public delegate void ObjectMetaCallback(bool success, ObjectMetadata result);
        public delegate void MultiObjectMetaCallback(bool success, ObjectMetadata[] result);

        /// <summary>
        /// 异步获取网盘配额
        /// </summary>
        /// <param name="callback">回调函数</param>
        public void GetQuotaAsync(QuotaCallback callback)
        {
            _trace.TraceInfo("BaiduPCS.GetQuotaAsync called: QuotaCallback callback=" + callback.ToString());
            var ret = new Quota();

            var param = new Parameters();
            param.Add("checkexpire", 1);
            param.Add("app_id", APPID);
            param.Add("bdstoken", _bdstoken);
            param.Add("logid", _get_logid());

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
                        callback?.Invoke(true, ret);
                    }
                    catch (Exception ex)
                    {
                        _trace.TraceError(ex);
                        callback?.Invoke(false, ret);
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
        public void DeletePathAsync(string path, OperationCallback callback)
        {
            _trace.TraceInfo("BaiduPCS.DeletePathAsync called: string path=" + path + ", OperationCallback callback=" + callback.ToString());
            DeletePathAsync(new string[] { path }, callback);
        }
        /// <summary>
        /// 异步删除多个文件
        /// </summary>
        /// <param name="paths">文件路径</param>
        /// <param name="callback">回调函数</param>
        public void DeletePathAsync(IEnumerable<string> paths, OperationCallback callback)
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
                querystr.Add("bdstoken", _bdstoken);
                querystr.Add("logid", _get_logid());
                querystr.Add("clienttype", 0);

                var postParam = new Parameters();
                var postJson = new JArray();

                bool empty = true;

                foreach (var item in paths)
                {
                    empty = false;
                    if (string.IsNullOrEmpty(item)) return;
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
                                callback?.Invoke(true, true);
                            }
                            catch (Exception ex)
                            {
                                _trace.TraceError(ex.ToString());
                                callback.Invoke(false, false);
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
                        callback?.Invoke(false, false);
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
        public void MovePathAsync(string source, string destination, OperationCallback callback, ondup ondup = ondup.overwrite)
        {
            _trace.TraceInfo("BaiduPCS.MovePathAsync called, string source=" + source + ", string destination=" + destination + ", OperationCallback callback=" + callback.ToString() + ", ondup ondup=" + ondup.ToString());
            MovePathAsync(new string[] { source }, new string[] { destination }, callback, ondup);
        }
        /// <summary>
        /// 异步移动多个文件
        /// </summary>
        /// <param name="source">原文件路径</param>
        /// <param name="destination">目标文件路径</param>
        /// <param name="callback">回调函数</param>
        /// <param name="ondup">同名覆盖方式</param>
        public void MovePathAsync(IEnumerable<string> source, IEnumerable<string> destination, OperationCallback callback, ondup ondup = ondup.overwrite)
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
            param.Add("bdstoken", _bdstoken);
            param.Add("logid", _get_logid());

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

                                callback?.Invoke(true, true);
                            }
                            catch (Exception ex)
                            {
                                _trace.TraceError(ex);
                                callback?.Invoke(false, false);
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
                        callback?.Invoke(false, false);
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
        public void CopyPathAsync(string source, string destination, OperationCallback callback, ondup ondup = ondup.overwrite)
        {
            _trace.TraceInfo("BaiduPCS.CopyPathAsync called: string source=" + source + ", string destination=" + destination + ", OperationCallback callback=" + callback.ToString() + ", ondup ondup=" + ondup.ToString());
            CopyPathAsync(new string[] { source }, new string[] { destination }, callback, ondup);
        }
        /// <summary>
        /// 异步复制多个文件
        /// </summary>
        /// <param name="source">原文件路径</param>
        /// <param name="destination">目标文件路径</param>
        /// <param name="callback">回调函数</param>
        /// <param name="ondup">同名覆盖方式</param>
        public void CopyPathAsync(IEnumerable<string> source, IEnumerable<string> destination, OperationCallback callback, ondup ondup = ondup.overwrite)
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
            param.Add("bdstoken", _bdstoken);
            param.Add("logid", _get_logid());

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

                                callback?.Invoke(true, true);
                            }
                            catch (Exception ex)
                            {
                                _trace.TraceError(ex);
                                callback?.Invoke(false, false);
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
                        callback?.Invoke(false, false);
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
        public void RenameAsync(string source, string new_name, OperationCallback callback)
        {
            _trace.TraceInfo("BaiduPCS.RenameAsync called: string source=" + source + ", string new_name=" + new_name + ", OperationCallback callback=" + callback.ToString());
            RenameAsync(new string[] { source }, new string[] { new_name }, callback);
        }
        /// <summary>
        /// 异步重命名多个文件
        /// </summary>
        /// <param name="source">原文件路径</param>
        /// <param name="new_name">新文件名</param>
        /// <param name="callback">回调函数</param>
        public void RenameAsync(IEnumerable<string> source, IEnumerable<string> new_name, OperationCallback callback)
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
            param.Add("bdstoken", _bdstoken);
            param.Add("logid", _get_logid());

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

                                callback?.Invoke(true, true);
                            }
                            catch (Exception ex)
                            {
                                _trace.TraceError(ex);
                                callback?.Invoke(false, false);
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
                        callback?.Invoke(false, false);
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
        public void CreateDirectoryAsync(string path, ObjectMetaCallback callback)
        {
            _trace.TraceInfo("BaiduPCS.CreateDirectory called: string path=" + path + ", ObjectMetaCallback callback=" + callback.ToString());
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
            query_param.Add("bdstoken", _bdstoken);
            query_param.Add("logid", _get_logid());
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

                                callback?.Invoke(true, ret);
                            }
                            catch (Exception ex)
                            {
                                _trace.TraceError(ex);
                                callback?.Invoke(false, new ObjectMetadata());
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
                        callback?.Invoke(false, new ObjectMetadata());
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

        public void GetFileListAsync(string path, MultiObjectMetaCallback callback, FileOrder order = FileOrder.name, bool asc = true, int page = 1, int count = 1000)
        {
            _trace.TraceInfo("BaiduPCS.GetFileListAsync called: string path=" + path + ", MultiObjectMetaCallback callback=" + callback.ToString() + ", FileOrder order=" + order + ", bool asc=" + asc + ", int page=" + page + ", int count=" + count);
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");

            var param = new Parameters();
            param.Add("dir", path);
            param.Add("bdstoken", _bdstoken);
            param.Add("logid", _get_logid());
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
                ns.HttpGetAsync(API_LIST_URL, (sender,e )=> 
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
                            callback?.Invoke(true, null);
                            return;
                        }
                        _check_error(json);

                        var list = json.Value<JArray>("list");
                        var ret = new List<ObjectMetadata>();
                        foreach (JObject item in list)
                        {
                            ret.Add(_read_json_meta(item));
                        }
                        callback?.Invoke(true, ret.ToArray());

                    }
                    catch (Exception ex)
                    {
                        _trace.TraceError(ex);
                        callback?.Invoke(false, null);
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
    }
}
