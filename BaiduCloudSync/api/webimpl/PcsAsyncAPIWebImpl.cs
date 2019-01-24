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
        private IOAuth _auth_data;
        private const int _PCS_AUTH_DATA_EXPIRATION_SECS = 60000; // expiration for 10 minutes
        private readonly string _cookie_guid = Guid.NewGuid().ToString(); // fixed GUID for cookie
        private const int APPID = 250528;
        private string _pcs_auth_sign;
        private long _pcs_auth_timestamp;
        private string _pcs_auth_bdstoken;

        public PcsAsyncAPIWebImpl(IOAuth oAuth)
        {
            if (oAuth == null) throw new ArgumentNullException("oAuth");
            if (!oAuth.IsLogin) throw new ArgumentException("OAuth error: not logged in before instancing PCS API");
            _auth_data = oAuth;
            _initialize_pcs_auth_data();
        }

        // 实例化一个HttpSession对象，使用OAuth自带的cookie值
        private HttpSession _instance_session()
        {
            var container = new CookieContainer();
            container.Add(new Cookie("BAIDUID", _auth_data.BaiduID, "/", ".baidu.com"));
            container.Add(new Cookie("BDUSS", _auth_data.BDUSS, "/", ".baidu.com"));
            container.Add(new Cookie("STOKEN", _auth_data.SToken, "/", ".baidu.com"));
            HttpSession.SetCookieContainer(_cookie_guid, container);
            return new HttpSession(cookie_group: _cookie_guid, timeout: 60000);
        }
        private void _initialize_pcs_auth_data()
        {
            if (!string.IsNullOrEmpty(_pcs_auth_sign) &&
                (util.FromUnixTimestamp(_pcs_auth_timestamp) + TimeSpan.FromSeconds(_PCS_AUTH_DATA_EXPIRATION_SECS) > DateTime.Now))
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

        public void ListDir(string path, EventHandler<PcsApiMultiObjectMetaCallbackArgs> callback, PcsFileOrder order = PcsFileOrder.Name, bool desc = false, int page = 1, int count = 1000, object state = null)
        {
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
                    { "dir", path },
                    { "bdstoken", _pcs_auth_bdstoken },
                    { "logid", PcsAsyncAPIWebImplHelper.LogID },
                    { "order", order.ToString().ToLower() },
                    { "desc", desc ? 1 : 0 },
                    { "app_id", APPID },
                    { "channel", "chunlei" },
                    { "web", 1 },
                    { "clienttype", 0 },
                    { "page", page },
                    { "num", count }
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
    }
}
