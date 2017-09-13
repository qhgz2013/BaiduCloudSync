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
        public delegate void QuotaCallback(bool success, Quota result);
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
                        _trace.TraceError(ex.ToString());
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
                _trace.TraceError(ex.ToString());
                throw;
            }
        }
        public delegate void DeleteCallback(bool success, bool result);
        public void DeletePathAsync(string path, DeleteCallback callback)
        {
            DeletePathAsync(new string[] { path }, callback);
        }
        public void DeletePathAsync(IEnumerable<string> paths, DeleteCallback callback)
        {
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
                        ns.HttpPostResponseAsync((sender2, e2) =>
                        {
                            //GetResponseStream
                            try
                            {
                                var response = ns.ReadResponseString();
                                ns.Close();
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
                        }, null);
                    }
                    catch (Exception ex)
                    {
                        _trace.TraceError(ex.ToString());
                        callback?.Invoke(false, false);
                    }
                }
                , null, "application/x-www-form-urlencoded", _get_xhr_param(), querystr);

            }
            catch (Exception ex)
            {
                _trace.TraceError(ex.ToString());
                throw;
            }
        }
    }
}
