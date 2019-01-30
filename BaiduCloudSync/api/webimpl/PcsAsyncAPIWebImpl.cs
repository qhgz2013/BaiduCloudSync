using BaiduCloudSync.api.callbackargs;
using BaiduCloudSync.oauth;
using GlobalUtil;
using GlobalUtil.http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace BaiduCloudSync.api.webimpl
{
    public class PcsAsyncAPIWebImpl : IPcsAsyncAPI
    {
        private const int _PCS_AUTH_DATA_EXPIRATION_SECS = 60000; // expiration for 10 minutes
        private readonly string _cookie_guid = Guid.NewGuid().ToString(); // fixed GUID for cookie
        private const int APPID = 250528;
        private string _pcs_auth_sign;
        private long _pcs_auth_timestamp;
        private string _pcs_auth_bdstoken;
        private Random _random = new Random();
        private readonly object _lock = new object();
        public IOAuth Auth { get; set; }
        public PcsAsyncAPIWebImpl(IOAuth oAuth)
        {
            if (oAuth == null) throw new ArgumentNullException("oAuth");
            if (!oAuth.IsLogin) throw new ArgumentException("OAuth error: not logged in before instancing PCS API");
            Auth = oAuth;
            _initialize_pcs_auth_data();
        }

        // 实例化一个HttpSession对象，使用OAuth自带的cookie值
        private HttpSession _instance_session()
        {
            var container = new CookieContainer();
            container.Add(new Cookie("BAIDUID", Auth.BaiduID, "/", ".baidu.com"));
            container.Add(new Cookie("BDUSS", Auth.BDUSS, "/", ".baidu.com"));
            container.Add(new Cookie("STOKEN", Auth.SToken, "/", ".baidu.com"));
            HttpSession.SetCookieContainer(_cookie_guid, container);
            return new HttpSession(cookie_group: _cookie_guid, timeout: 60000);
        }
        private void _initialize_pcs_auth_data()
        {
            lock (_lock)
            {
                if (!string.IsNullOrEmpty(_pcs_auth_sign) &&
                    (Util.FromUnixTimestamp(_pcs_auth_timestamp) + TimeSpan.FromSeconds(_PCS_AUTH_DATA_EXPIRATION_SECS) > DateTime.Now))
                    return;
                var sess = _instance_session();
                try
                {
                    Tracer.GlobalTracer.TraceInfo("Begin PCS auth data initialization");

                    sess.HttpGet("https://pan.baidu.com/");
                    var html = sess.ReadResponseString();

                    string bdstoken = null, sign1 = null, sign3 = null;
                    long timestamp = 0;

                    var _lambda_dump_failure = new ParameterizedThreadStart((msg) =>
                    {
                        Tracer.GlobalTracer.TraceError("Could not initialize PCS Auth data: REGEX not match, the original HTML code is dumped below");
                        Tracer.GlobalTracer.TraceError(msg.ToString());
                        throw new PCSApiUnexpectedResponseException();
                    });

                    var match = Regex.Match(html, "\"bdstoken\":\"(\\w+)\"");
                    if (match.Success)
                        bdstoken = match.Result("$1");
                    else
                        _lambda_dump_failure(html);

                    match = Regex.Match(html, "\"sign1\":\"(\\w+)\"");
                    if (match.Success)
                        sign1 = match.Result("$1");
                    else
                        _lambda_dump_failure(html);

                    match = Regex.Match(html, "\"sign3\":\"(\\w+)\"");
                    if (match.Success)
                        sign3 = match.Result("$1");
                    else
                        _lambda_dump_failure(html);

                    match = Regex.Match(html, "\"timestamp\":(\\d+)");
                    if (match.Success)
                        timestamp = long.Parse(match.Result("$1"));
                    else
                        _lambda_dump_failure(html);

                    // compute sign
                    var j = Encoding.UTF8.GetBytes(sign3);
                    var r = Encoding.UTF8.GetBytes(sign1);
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
                    int i = 0; u = 0;
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

                    _pcs_auth_sign = Convert.ToBase64String(o);
                    _pcs_auth_bdstoken = bdstoken;
                    _pcs_auth_timestamp = timestamp;
                }
                catch (PCSApiUnexpectedResponseException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Tracer.GlobalTracer.TraceError(ex);
                    throw new PCSApiUnexpectedResponseException("Unexpected error while initializing pcs auth data", ex);
                }
                finally
                {
                    Tracer.GlobalTracer.TraceInfo("Exit PCS auth data initialization");
                    sess.Close();
                }
            }
        }

        public void ListDir(string path, EventHandler<PcsApiMultiObjectMetaCallbackArgs> callback, PcsFileOrder order = PcsFileOrder.Name, bool desc = false, int page = 1, int count = 1000, object state = null)
        {
            // test passed, https api version 20190127
            try
            {
                path = new PcsPath(path).FullPath;
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException("Invalid Parameter: path", ex);
            }
            if (page < 1) throw new ArgumentOutOfRangeException("page", "page should start with index 1");
            if (count < 1) throw new ArgumentOutOfRangeException("count", "count should be positive");

            try
            {
                _initialize_pcs_auth_data();
                var sess = _instance_session();

                var param = new Parameters
                {
                    { "order", order.ToString().ToLower() },
                    { "desc", desc ? 1 : 0 },
                    { "showempty", 0 },
                    { "web", 1 },
                    { "page", page },
                    { "num", count },
                    { "dir", path },
                    { "t", _random.NextDouble() },
                    { "channel", "chunlei" },
                    { "web", 1 },
                    { "app_id", APPID },
                    { "bdstoken", _pcs_auth_bdstoken },
                    { "logid", PcsAsyncAPIWebImplHelper.LogID },
                    { "clienttype", 0 },
                    { "startLogTime", (long)(DateTime.Now.ToUnixTimestamp() * 1000) }
                };

                sess.HttpGetAsync("https://pan.baidu.com/api/list", query: param, callback: (sender, e) =>
                {
                    string failed_reason = null;
                    PcsMetadata[] data = null;
                    try
                    {
                        var response_string = e.Session.ReadResponseString();
                        if (e.Session.HTTP_Response.StatusCode != HttpStatusCode.OK)
                            throw new PCSApiUnexpectedResponseException($"API call from HTTP failed, HTTP status code: {(int)e.Session.HTTP_Response.StatusCode}, response string: {response_string}");
                        var json = JsonConvert.DeserializeObject(response_string) as JObject;

                        failed_reason = PcsAsyncAPIWebImplHelper.CheckJson(json);
                        if (!string.IsNullOrEmpty(failed_reason))
                            throw new PCSApiUnexpectedResponseException($"API call from HTTP failed: {failed_reason}");

                        var files = json.Value<JArray>("list");
                        var temp_data = new List<PcsMetadata>();
                        foreach (var file in files)
                        {
                            temp_data.Add(PcsAsyncAPIWebImplHelper.ReadMetadataFromJson(file as JObject));
                        }
                        data = temp_data.ToArray();
                    }
                    catch (Exception ex)
                    {
                        Tracer.GlobalTracer.TraceError("Unexpected API Exception");
                        Tracer.GlobalTracer.TraceError(ex);
                        failed_reason = ex.Message;
                    }
                    finally
                    {
                        e.Session.Close();
                        if (data == null)
                            callback?.Invoke(this, new PcsApiMultiObjectMetaCallbackArgs(failed_reason, state));
                        else
                            callback?.Invoke(this, new PcsApiMultiObjectMetaCallbackArgs(data, state));
                    }
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("operation failed, see the below exception for more details", ex);
            }
        }

        public void GetQuota(EventHandler<PcsApiQuotaCallbackArgs> callback, object state = null)
        {
            // test passed, https api version 20190127
            try
            {
                _initialize_pcs_auth_data();
                var sess = _instance_session();

                var param = new Parameters
                {
                    { "checkexpire", 1 },
                    { "checkfree", 1 },
                    { "channel", "chunlei" },
                    { "web", 1 },
                    { "app_id", APPID },
                    { "bdstoken", _pcs_auth_bdstoken },
                    { "logid", PcsAsyncAPIWebImplHelper.LogID },
                    { "clienttype", 0 }
                };

                sess.HttpGetAsync("https://pan.baidu.com/api/quota", query: param, callback: (sender, e) =>
                {
                    string failed_reason = null;
                    long? used = null, total = null;
                    try
                    {
                        var response_string = e.Session.ReadResponseString();
                        if (e.Session.HTTP_Response.StatusCode != HttpStatusCode.OK)
                            throw new PCSApiUnexpectedResponseException($"API call from HTTP failed, HTTP status code: {(int)e.Session.HTTP_Response.StatusCode}, response string: {response_string}");
                        var json = JsonConvert.DeserializeObject(response_string) as JObject;

                        failed_reason = PcsAsyncAPIWebImplHelper.CheckJson(json);
                        if (!string.IsNullOrEmpty(failed_reason))
                            throw new PCSApiUnexpectedResponseException($"API call from HTTP failed: {failed_reason}");

                        used = json.Value<long>("used");
                        total = json.Value<long>("total");
                    }
                    catch (Exception ex)
                    {
                        Tracer.GlobalTracer.TraceError("Unexpected API Exception");
                        Tracer.GlobalTracer.TraceError(ex);
                        failed_reason = ex.Message;
                    }
                    finally
                    {
                        e.Session.Close();
                        if (total == null)
                            callback?.Invoke(this, new PcsApiQuotaCallbackArgs(failed_reason, state));
                        else
                            callback?.Invoke(this, new PcsApiQuotaCallbackArgs(used.Value, total.Value, state));
                    }
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("operation failed, see the below exception for more details", ex);
            }
        }

        public void CreateDirectory(string path, EventHandler<PcsApiOperationCallbackArgs> callback, object state = null)
        {
            // test passed, api version 20190127
            try
            {
                path = new PcsPath(path).FullPath;
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException("Invalid Parameter: path", ex);
            }

            try
            {
                _initialize_pcs_auth_data();
                var sess = _instance_session();
                sess.ContentType = "application/x-www-form-urlencoded";

                var param = new Parameters
                {
                    { "a", "commit" },
                    { "channel", "chunlei" },
                    { "web", 1 },
                    { "app_id", APPID },
                    { "bdstoken", _pcs_auth_bdstoken },
                    { "logid", PcsAsyncAPIWebImplHelper.LogID },
                    { "clienttype", 0 }
                };
                var body = new Parameters
                {
                    { "path", path },
                    { "isdir", 1 },
                    { "blocklist", "[]" }
                };
                sess.HttpPostAsync("https://pan.baidu.com/api/create", query: param, body: body, callback: (sender, e) =>
                {
                    string failed_reason = null;
                    bool suc = false;
                    try
                    {
                        var response_string = e.Session.ReadResponseString();
                        if (e.Session.HTTP_Response.StatusCode != HttpStatusCode.OK)
                            throw new PCSApiUnexpectedResponseException($"API call from HTTP failed, HTTP status code: {(int)e.Session.HTTP_Response.StatusCode}, response string: {response_string}");
                        var json = JsonConvert.DeserializeObject(response_string) as JObject;

                        failed_reason = PcsAsyncAPIWebImplHelper.CheckJson(json);
                        if (!string.IsNullOrEmpty(failed_reason))
                            throw new PCSApiUnexpectedResponseException($"API call from HTTP failed: {failed_reason}");

                        suc = true;
                    }
                    catch (Exception ex)
                    {
                        Tracer.GlobalTracer.TraceError("Unexpected API Exception");
                        Tracer.GlobalTracer.TraceError(ex);
                        failed_reason = ex.Message;
                    }
                    finally
                    {
                        e.Session.Close();
                        if (!suc)
                            callback?.Invoke(this, new PcsApiOperationCallbackArgs(failed_reason, state));
                        else
                            callback?.Invoke(this, new PcsApiOperationCallbackArgs(state));
                    }
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("operation failed, see the below exception for more details", ex);
            }
        }

        // wait for async task to be done (by sending heartbeat request)
        private bool _wait_async_complete(string task_id, Parameters filelist, out string failure_reason)
        {
            failure_reason = null;
            if (string.IsNullOrEmpty(task_id) || filelist == null)
            {
                failure_reason = "Invalid arguments for calling wait_async_complete";
                return false;
            }

            var query_param = new Parameters
            {
                { "taskid", task_id },
                { "channel", "chunlei" },
                { "web", 1 },
                { "app_id", APPID },
                { "bdstoken", _pcs_auth_bdstoken },
                { "logid", PcsAsyncAPIWebImplHelper.LogID },
                { "clienttype", 0 }
            };

            DateTime begin = DateTime.Now;
            TimeSpan exceed_time = TimeSpan.FromMinutes(1);
            bool warn_on_exceed = false;

            var sess = _instance_session();
            sess.ContentType = "application/x-www-form-urlencoded";
            while (true)
            {
                sess.HttpPost("https://pan.baidu.com/share/taskquery", body: filelist, query: query_param);
                var str = sess.ReadResponseString();
                var task_json = JsonConvert.DeserializeObject(str) as JObject;
                PcsAsyncAPIWebImplHelper.CheckJson(task_json);
                var status = task_json.Value<string>("status");

                switch (status)
                {
                    case "success":
                        return true;

                    case "failed":
                        if (task_json.TryGetValue("task_errno", out var task_errno_token))
                            failure_reason = $"API call: Async wait failed with errno no: {task_errno_token.Value<string>()}";
                        else
                            failure_reason = "API call: Async wait failed";
                        return false;

                    case "pending":
                    case "running":
                        if (!warn_on_exceed && DateTime.Now - begin > exceed_time)
                        {
                            warn_on_exceed = true;
                            Tracer.GlobalTracer.TraceWarning($"API call: Async wait has blocked for more than ${exceed_time.TotalMinutes} minutes");
                        }
                        Thread.Sleep(1000);
                        break;

                    default:
                        failure_reason = $"Unexpected task status: {status}, set to failure";
                        Tracer.GlobalTracer.TraceWarning(failure_reason);
                        return false;
                }
            }
        }

        private void _file_manager_ops(string op_name, Parameters body, EventHandler<PcsApiOperationCallbackArgs> callback, object state)
        {
            try
            {
                _initialize_pcs_auth_data();
                var sess = _instance_session();
                sess.ContentType = "application/x-www-form-urlencoded";

                var param = new Parameters
                {
                    { "opera", op_name },
                    { "async", 2 },
                    { "onnest", "fail" },
                    { "channel", "chunlei" },
                    { "web", 1 },
                    { "app_id", APPID },
                    { "bdstoken", _pcs_auth_bdstoken },
                    { "logid", PcsAsyncAPIWebImplHelper.LogID },
                    { "clienttype", 0 }
                };

                sess.HttpPostAsync("https://pan.baidu.com/api/filemanager", query: param, body: body, callback: (sender, e) =>
                {
                    string failed_reason = null;
                    string taskid = null;
                    bool suc = false;
                    try
                    {
                        var response_string = e.Session.ReadResponseString();
                        if (e.Session.HTTP_Response.StatusCode != HttpStatusCode.OK)
                            throw new PCSApiUnexpectedResponseException($"API call from HTTP failed, HTTP status code: {(int)e.Session.HTTP_Response.StatusCode}, response string: {response_string}");
                        var json = JsonConvert.DeserializeObject(response_string) as JObject;

                        failed_reason = PcsAsyncAPIWebImplHelper.CheckJson(json);
                        if (!string.IsNullOrEmpty(failed_reason))
                            throw new PCSApiUnexpectedResponseException($"API call from HTTP failed: {failed_reason}");

                        // extracting task id from json
                        bool extract_taskid_suc = false;
                        extract_taskid_suc = json.TryGetValue("taskid", out var taskid_token);
                        if (extract_taskid_suc)
                        {
                            taskid = taskid_token.Value<string>();
                            if (string.IsNullOrEmpty(taskid)) extract_taskid_suc = false;
                        }

                        if (!extract_taskid_suc)
                        {
                            // could not get async data, set it to success
                            Tracer.GlobalTracer.TraceWarning("API call for Delete: Could not get async task id, ignore async wait and assume the operation is succeeded");
                            suc = true;
                            return;
                        }

                        // wait success
                        if (!(suc = _wait_async_complete(taskid, body, out var tmp_failed_reason)))
                        {
                            failed_reason = tmp_failed_reason;
                        }
                    }
                    catch (Exception ex)
                    {
                        Tracer.GlobalTracer.TraceError("Unexpected API Exception");
                        Tracer.GlobalTracer.TraceError(ex);
                        failed_reason = ex.Message;
                    }
                    finally
                    {
                        e.Session.Close();
                        if (!suc)
                            callback?.Invoke(this, new PcsApiOperationCallbackArgs(failed_reason, state));
                        else
                            callback?.Invoke(this, new PcsApiOperationCallbackArgs(state));
                    }
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("operation failed, see the below exception for more details", ex);
            }
        }

        // variable access for file manager api:
        public void Delete(IEnumerable<string> paths, EventHandler<PcsApiOperationCallbackArgs> callback, object state = null)
        {
            // test passed, api version 20190127
            var path_list = paths.ToList();
            if (path_list.Count == 0)
                callback?.Invoke(this, new PcsApiOperationCallbackArgs(state));
            try
            {
                for (int i = 0; i < path_list.Count; i++)
                {
                    path_list[i] = new PcsPath(path_list[i]).FullPath;
                }
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException("Invalid Parameter: paths", ex);
            }
            var path_json = JsonConvert.SerializeObject(path_list);
            var body = new Parameters
            {
                { "filelist", path_json }
            };

            _file_manager_ops("delete", body, callback, state);
        }

        public void Move(IEnumerable<string> source, IEnumerable<string> destination, EventHandler<PcsApiOperationCallbackArgs> callback, object state = null)
        {
            // test passed, api version 20190130
            var src = source.ToList();
            var dst = destination.ToList();
            var new_name = new List<string>(dst.Count);
            if (src.Count != dst.Count)
                throw new ArgumentException($"Length of source ({src.Count}) is mot match the length of destination ({dst.Count})");

            try
            {
                for (int i = 0; i < src.Count; i++)
                    src[i] = new PcsPath(src[i]).FullPath;
                for (int i = 0; i < dst.Count; i++)
                {
                    // split destination to parent_directory + file_name (required in POST field)
                    var path = new PcsPath(dst[i]);
                    dst[i] = path.Parent.FullPath;
                    new_name.Add(path.Name);
                }
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException("Invalid Parameter: path", ex);
            }

            if (src.Count == 0)
                callback?.Invoke(this, new PcsApiOperationCallbackArgs(state));

            var path_json = new JArray();
            for (int i = 0; i < src.Count; i++)
            {
                path_json.Add(new JObject {
                    { "path", src[i] },
                    { "dest", dst[i] },
                    { "newname", new_name[i] }
                });
            }
            var body = new Parameters
            {
                { "filelist", JsonConvert.SerializeObject(path_json) }
            };

            _file_manager_ops("move", body, callback, state);
        }

        public void Copy(IEnumerable<string> source, IEnumerable<string> destination, EventHandler<PcsApiOperationCallbackArgs> callback, object state = null)
        {
            // test passed, api version 20190130
            var src = source.ToList();
            var dst = destination.ToList();
            var new_name = new List<string>(dst.Count);
            if (src.Count != dst.Count)
                throw new ArgumentException($"Length of source ({src.Count}) is mot match the length of destination ({dst.Count})");

            try
            {
                for (int i = 0; i < src.Count; i++)
                    src[i] = new PcsPath(src[i]).FullPath;
                for (int i = 0; i < dst.Count; i++)
                {
                    // split destination to parent_directory + file_name (required in POST field)
                    var path = new PcsPath(dst[i]);
                    dst[i] = path.Parent.FullPath;
                    new_name.Add(path.Name);
                }
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException("Invalid Parameter: path", ex);
            }

            if (src.Count == 0)
                callback?.Invoke(this, new PcsApiOperationCallbackArgs(state));

            var path_json = new JArray();
            for (int i = 0; i < src.Count; i++)
            {
                path_json.Add(new JObject {
                    { "path", src[i] },
                    { "dest", dst[i] },
                    { "newname", new_name[i] }
                });
            }
            var body = new Parameters
            {
                { "filelist", JsonConvert.SerializeObject(path_json) }
            };

            _file_manager_ops("copy", body, callback, state);
        }

        public void Rename(IEnumerable<string> source, IEnumerable<string> new_name, EventHandler<PcsApiOperationCallbackArgs> callback, object state = null)
        {
            // test passed, api version 20190130
            var src = source.ToList();
            var name = new_name.ToList();
            if (src.Count != name.Count)
                throw new ArgumentException($"Length of source ({src.Count}) is mot match the length of new_name ({name.Count})");

            try
            {
                for (int i = 0; i < src.Count; i++)
                    src[i] = new PcsPath(src[i]).FullPath;
                for (int i = 0; i < name.Count; i++)
                    new PcsPath($"/{name}"); // just testing the name is valid or not
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException("Invalid Parameter: path", ex);
            }

            if (src.Count == 0)
                callback?.Invoke(this, new PcsApiOperationCallbackArgs(state));

            var path_json = new JArray();
            for (int i = 0; i < src.Count; i++)
            {
                path_json.Add(new JObject {
                    { "path", src[i] },
                    { "newname", name[i] }
                });
            }
            var body = new Parameters
            {
                { "filelist", JsonConvert.SerializeObject(path_json) }
            };

            _file_manager_ops("rename", body, callback, state);
        }
    }
}
