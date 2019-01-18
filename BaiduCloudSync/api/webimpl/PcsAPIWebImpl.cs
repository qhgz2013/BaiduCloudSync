using BaiduCloudSync.oauth;
using GlobalUtil.http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace BaiduCloudSync.api.webimpl
{
    public class PcsAPIWebImpl : IPcsAPI
    {
        private IOAuth _auth_data;
        private const int _PCS_AUTH_DATA_EXPIRATION_SECS = 60000; // expiration for 10 minutes
        private readonly string _cookie_guid = Guid.NewGuid().ToString(); // fixed GUID for cookie
        
        private string _pcs_auth_sign;
        private long _pcs_auth_timestamp;
        private string _pcs_auth_bdstoken;

        public PcsAPIWebImpl(IOAuth oAuth)
        {
            if (oAuth == null) throw new ArgumentNullException("oAuth");
            if (!oAuth.IsLogin) throw new ArgumentException("OAuth error: not logged in before instancing PCS API");
            _auth_data = oAuth;
            _initialize_pcs_auth_data();
        }

        private void _prepare_http_cookie()
        {
            var container = new CookieContainer();
            container.Add(new Cookie("BAIDUID", _auth_data.BaiduID, "/", ".baidu.com"));
            container.Add(new Cookie("BDUSS", _auth_data.BDUSS, "/", ".baidu.com"));
            container.Add(new Cookie("STOKEN", _auth_data.SToken, "/", ".baidu.com"));
            if (!HttpSession.DefaultCookieContainer.ContainsKey(_cookie_guid))
                HttpSession.DefaultCookieContainer.Add(_cookie_guid, container);
            else
                HttpSession.DefaultCookieContainer[_cookie_guid] = container;
        }
        private HttpSession _instance_session()
        {
            _prepare_http_cookie();
            return new HttpSession(cookie_group: _cookie_guid);
        }
        private void _initialize_pcs_auth_data()
        {
            if (!string.IsNullOrEmpty(_pcs_auth_sign) && 
                (GlobalUtil.util.FromUnixTimestamp(_pcs_auth_timestamp) + TimeSpan.FromSeconds(_PCS_AUTH_DATA_EXPIRATION_SECS) > DateTime.Now))
                return;
            try
            {
                GlobalUtil.Tracer.GlobalTracer.TraceInfo("Begin PCS auth data initialization");

                var sess = _instance_session();
                sess.HttpGet("https://pan.baidu.com/");
                var html = sess.ReadResponseString();

                string bdstoken = null, sign1 = null, sign3 = null;
                long timestamp = 0;

                // this is the inner function called while match failed
                var lambda_dump_failure = new ParameterizedThreadStart( (msg) => { 
                    GlobalUtil.Tracer.GlobalTracer.TraceError("Could not initialize PCS Auth data: REGEX not match, the original HTML code is dumped below");
                    GlobalUtil.Tracer.GlobalTracer.TraceError(msg.ToString());
                    throw new PCSApiUnexpectedResponseException();
                });

                var match = Regex.Match(html, "\"bdstoken\":\"(\\w+)\"");
                if (match.Success)
                    bdstoken = match.Result("$1");
                else
                    lambda_dump_failure(html);

                match = Regex.Match(html, "\"sign1\":\"(\\w+)\"");
                if (match.Success)
                    sign1 = match.Result("$1");
                else
                    lambda_dump_failure(html);

                match = Regex.Match(html, "\"sign3\":\"(\\w+)\"");
                if (match.Success)
                    sign3 = match.Result("$1");
                else
                    lambda_dump_failure(html);

                match = Regex.Match(html, "\"timestamp\":(\\d+)");
                if (match.Success)
                    timestamp = long.Parse(match.Result("$1"));
                else
                    lambda_dump_failure(html);

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
                GlobalUtil.Tracer.GlobalTracer.TraceError(ex);
                throw new PCSApiUnexpectedResponseException("Unexpected error while initializing pcs auth data", ex);
            }
            finally
            {
                GlobalUtil.Tracer.GlobalTracer.TraceInfo("Exit PCS auth data initialization");
            }
        }
        
        public void Copy(string src_path, string dst_path, PcsOverwriteType overwrite_type)
        {
            throw new NotImplementedException();
        }

        public bool CreateDirectory(string path)
        {
            throw new NotImplementedException();
        }

        public bool Delete(string path)
        {
            throw new NotImplementedException();
        }

        public PcsMetadata ListDir(string path)
        {
            throw new NotImplementedException();
        }

        public void Move(string src_path, string dst_path, PcsOverwriteType overwrite_type)
        {
            throw new NotImplementedException();
        }

        public void Rename(string path, string new_name)
        {
            throw new NotImplementedException();
        }

        PcsMetadata[] IPcsAPI.ListDir(string path)
        {
            throw new NotImplementedException();
        }
    }
}
