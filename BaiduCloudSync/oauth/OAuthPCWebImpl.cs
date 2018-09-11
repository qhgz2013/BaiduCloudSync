using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GlobalUtil.NetUtils;
using GlobalUtil;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using System.Drawing;
using System.IO;

namespace BaiduCloudSync.oauth
{
    public class OAuthPCWebImpl : IAuth
    {
        //用于区分不同账号的cookie所需要的key
        private string _identifier;
        //用于重复调用login需要的持久变量
        private string _token;
        private string _gid;
        private string _codestring;
        private string _vcodetype;

        private bool _captcha_generated;
        public OAuthPCWebImpl(string identifier = null)
        {
            if (identifier == null)
            {
                //通过表单的随机生成算法生成当前cookie所属的key
                identifier = util.GenerateFormDataBoundary();
            }
            _identifier = identifier;
            _gid = Guid.NewGuid().ToString().Substring(1);
        }
        public object GetCaptcha()
        {
            _get_token();
            if (string.IsNullOrEmpty(_vcodetype)) return null;
            if (_captcha_generated)
            {
                // re-generate the captcha
                var new_param = _v2__reggetcodestr(_token, _vcodetype);
                _codestring = new_param.verifystr;
                if (string.IsNullOrEmpty(_codestring))
                {
                    Tracer.GlobalTracer.TraceWarning("got an empty codestring after requesting re-get codestring");
                    return null;
                }
                return _cgi_bin_genimage(_codestring);
            }
            else
            {
                _captcha_generated = true;
                // first time generating the captcha
                if (string.IsNullOrEmpty(_codestring)) return null;
                else return _cgi_bin_genimage(_codestring);
            }
        }

        public bool IsLogin()
        {
            throw new NotImplementedException();
        }

        // method implementation of GET /v2/api/?getapi
        private string _v2_api__getapi(string gid)
        {
            var ns = new NetStream();
            ns.CookieKey = _identifier;

            try
            {
                Tracer.GlobalTracer.TraceInfo("Fetching: getapi");

                var query_param = new Parameters();
                query_param.Add("tpl", "netdisk");
                query_param.Add("subpro", "netdisk_web");
                query_param.Add("apiver", "v3");
                query_param.Add("tt", (long)(util.ToUnixTimestamp(DateTime.Now) * 1000));
                query_param.Add("class", "login");
                query_param.Add("gid", gid);
                query_param.Add("loginversion", "v4");
                query_param.Add("logintype", "basicLogin");
                query_param.Add("traceid", "");
                query_param.Add("callback", "bd__cbs__abcdef");

                var referer = new Parameters();
                referer.Add("Referer", "https://pan.baidu.com/");

                ns.HttpGet("https://passport.baidu.com/v2/api/?getapi&" + query_param.BuildQueryString(), headerParam: referer);
                var api_result = ns.ReadResponseString();
                api_result = util.EscapeCallbackFunction(api_result);
                var json_api_result = JsonConvert.DeserializeObject(api_result) as JObject;
                var errno = json_api_result["errInfo"].Value<string>("no");
                if (errno != "0") throw new LoginFailedException("failed to get token: " + json_api_result.ToString());

                return json_api_result["data"].Value<string>("token");
            }
            catch (LoginFailedException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw new LoginFailedException("getapi failed", ex);
            }
            finally
            {
                ns.Close();
            }
        }

        // method implementation of GET /v2/api/?logincheck
        private struct _logincheck_result
        {
            public string codestring;
            public string vcodetype;
        }
        private _logincheck_result _v2_api__logincheck(string token, string username)
        {
            var ns = new NetStream();
            ns.CookieKey = _identifier;

            try
            {
                Tracer.GlobalTracer.TraceInfo("Fetching: logincheck");

                var param = new Parameters();
                param.Add("token", token);
                param.Add("tpl", "netdisk");
                param.Add("subpro", "netdisk_web");
                param.Add("apiver", "v3");
                param.Add("tt", (long)(util.ToUnixTimestamp(DateTime.Now) * 1000));
                param.Add("sub_source", "leadsetpwd");
                param.Add("username", username);
                param.Add("loginversion", "v4");
                param.Add("dv", "i_do_not_know_this_param");
                param.Add("traceid", "");
                param.Add("callback", "bd__cbs__ababab");

                var referer = new Parameters();
                referer.Add("Referer", "https://pan.baidu.com/");

                ns.HttpGet("https://passport.baidu.com/v2/api/?logincheck&" + param.BuildQueryString(), headerParam: referer);

                var api_result = ns.ReadResponseString();
                api_result = util.EscapeCallbackFunction(api_result);

                var json_api_result = JsonConvert.DeserializeObject(api_result) as JObject;
                var errno = json_api_result["errInfo"].Value<string>("no");
                if (errno != "0") throw new LoginFailedException("failed to get logincheck: " + api_result.ToString());

                return new _logincheck_result()
                {
                    codestring = json_api_result["data"].Value<string>("codeString"),
                    vcodetype = json_api_result["data"].Value<string>("vcodetype")
                };
            }
            catch (LoginFailedException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw new LoginFailedException("logincheck failed", ex);
            }
        }

        // method implementation of GET /v2/getpublickey
        private struct _getpublickey_result
        {
            public string key;
            public string pubkey;
        }
        private _getpublickey_result _v2_getpublickey(string token, string gid)
        {
            var ns = new NetStream();
            ns.CookieKey = _identifier;
            try
            {
                Tracer.GlobalTracer.TraceInfo("Fetching: getpublickey");

                var param = new Parameters();
                param.Add("token", token);
                param.Add("tpl", "netdisk");
                param.Add("subpro", "netdisk_web");
                param.Add("apiver", "v3");
                param.Add("tt", (long)(util.ToUnixTimestamp(DateTime.Now) * 1000));
                param.Add("gid", gid);
                param.Add("loginversion", "v4");
                param.Add("traceid", "");
                param.Add("callback", "bd__cbs__emmmmm");

                var referer = new Parameters();
                referer.Add("Referer", "https://pan.baidu.com/");

                ns.HttpGet("https://passport.baidu.com/v2/getpublickey", urlParam: param, headerParam: referer);

                var api_result = ns.ReadResponseString();
                api_result = util.EscapeCallbackFunction(api_result);

                var json_api_result = JsonConvert.DeserializeObject(api_result) as JObject;

                var errno = json_api_result.Value<string>("errno");
                if (errno != "0") throw new LoginFailedException("failed to get getpublickey: " + json_api_result.ToString());

                return new _getpublickey_result()
                {
                    key = json_api_result.Value<string>("key"),
                    pubkey = json_api_result.Value<string>("pubkey")
                };
            }
            catch (LoginFailedException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw new LoginFailedException("get getpublickey failed", ex);
            }
        }

        // method implementation of /v2/api/?login
        private struct _login_result
        {
            public string errno;
            public string codestring;
            public string vcodetype;
        }
        private _login_result _v2_api__login(string token, string codestring, string gid, string username, string password, string rsakey, string rsa_publickey, string verify_code)
        {
            var ns = new NetStream();
            ns.CookieKey = _identifier;

            try
            {
                Tracer.GlobalTracer.TraceInfo("Posting: login");

                var rsa_param = Crypto.RSA_ImportPEMPublicKey(rsa_publickey);
                var encrypted_password = Crypto.RSA_StringEncrypt(password, rsa_param);
                password = Convert.ToBase64String(encrypted_password);

                var body = new Parameters();
                body.Add("staticpage", "https://pan.baidu.com/res/static/thirdparty/pass_v3_jump.html");
                body.Add("charset", "UTF-8");
                body.Add("token", token);
                body.Add("tpl", "netdisk");
                body.Add("subpro", "netdisk_web");
                body.Add("apiver", "v3");
                body.Add("tt", (long)(util.ToUnixTimestamp(DateTime.Now) * 1000));
                body.Add("codestring", codestring == null ? "" : codestring);
                body.Add("safeflg", "0");
                body.Add("u", "https://pan.baidu.com/disk/home");
                body.Add("isPhone", "");
                body.Add("detect", "1");
                body.Add("gid", gid);
                body.Add("quick_user", "0");
                body.Add("logintype", "basicLogin");
                body.Add("logLoginType", "pc_loginBasic");
                body.Add("idc", "");
                body.Add("loginmerge", "true");
                body.Add("foreignusername", "");
                body.Add("username", username);
                body.Add("password", password);
                if (verify_code != null)
                    body.Add("verifycode", verify_code);
                body.Add("mem_pass", "on");
                body.Add("rsakey", rsakey);
                body.Add("crypttype", "12");
                body.Add("ppui_logintime", new Random().Next(5000, 15000)); // random range [5000, 15000)
                body.Add("countrycode", "");
                body.Add("fp_uid", "");
                body.Add("fp_info", "");
                body.Add("login_version", "v4");
                body.Add("dv", "i_do_not_know_this_parameter");
                body.Add("traceid", "");
                body.Add("callback", "parent.bd__pcbs__fuckbd");

                var header = new Parameters();
                header.Add("Origin", "https://pan.baidu.com");
                header.Add("Referer", "https://pan.baidu.com/");

                ns.HttpPost("https://passport.baidu.com/v2/api/?login", body, headerParam: header);

                var response = ns.ReadResponseString();

                Tracer.GlobalTracer.TraceInfo(response);

                var ret = new _login_result();

                // parsing login result
                var match = Regex.Match(response, "href\\s\\+=\\s\"([^\"]+)\"");
                if (!match.Success) throw new LoginFailedException("could not get login status code from HTML response");

                var response_param = match.Result("$1");
                match = Regex.Match(response_param, @"&?([^=]+)=([^&]*)");
                var response_key_values = new Dictionary<string, string>();
                while (match.Success)
                {
                    response_key_values.Add(match.Result("$1"), match.Result("$2"));
                    match = match.NextMatch();
                }

                if (!response_key_values.TryGetValue("err_no", out ret.errno)) throw new LoginFailedException("could not get errno from HTML response");
                if (!response_key_values.TryGetValue("codeString", out ret.codestring)) throw new LoginFailedException("could not get codestring from HTML response");
                if (!response_key_values.TryGetValue("vcodetype", out ret.vcodetype)) throw new LoginFailedException("could not get vcodetype from HTML response");

                _captcha_generated = false;
                return ret;
            }
            catch (LoginFailedException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw new LoginFailedException("post login failed", ex);
            }
            finally
            {
                ns.Close();
            }
        }

        // method implementation of /cgi-bin/genimage
        private Image _cgi_bin_genimage(string codestring)
        {
            var ns = new NetStream();
            ns.CookieKey = _identifier;
            try
            {
                Tracer.GlobalTracer.TraceInfo("Fetching: genimage");

                ns.HttpGet("https://passport.baidu.com/cgi-bin/genimage?" + codestring);
                var binary_data = ns.ReadResponseBinary();
                var ms = new MemoryStream(binary_data);
                return Image.FromStream(ms);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                ns.Close();
            }
        }

        // method implementation of /v2/?regetcodestr
        private struct _regetcodestr_result
        {
            public string verifysign;
            public string verifystr;
        }
        private _regetcodestr_result _v2__reggetcodestr(string token, string vcodetype)
        {
            var ns = new NetStream();
            ns.CookieKey = _identifier;

            try
            {
                Tracer.GlobalTracer.TraceInfo("Fetching: reggetcodestr");

                var param = new Parameters();
                param.Add("token", token);
                param.Add("tpl", "netdisk");
                param.Add("subpro", "netdisk_web");
                param.Add("apiver", "v3");
                param.Add("tt", (long)(util.ToUnixTimestamp(DateTime.Now) * 1000));
                param.Add("fr", "login");
                param.Add("loginversion", "v4");
                param.Add("vcodetype", vcodetype);
                param.Add("traceid", "");
                param.Add("callback", "bd__cbs__nyanya");

                var referer = new Parameters();
                referer.Add("Referer", "https://pan.baidu.com/");

                ns.HttpGet("https://passport.baidu.com/v2/?reggetcodestr&" + param.BuildQueryString(), headerParam: referer);

                var api_result = ns.ReadResponseString();
                api_result = util.EscapeCallbackFunction(api_result);

                var json_api_result = JsonConvert.DeserializeObject(api_result) as JObject;

                var errno = json_api_result["errInfo"].Value<string>("no");
                if (errno != "0") throw new Exception("get captcha failed");

                return new _regetcodestr_result()
                {
                    verifysign = json_api_result["data"].Value<string>("verifySign"),
                    verifystr = json_api_result["data"].Value<string>("verifyStr")
                };
            }
            finally
            {
                ns.Close();
            }
        }


        // handling token
        private string _get_token()
        {
            var ns = new NetStream();
            ns.CookieKey = _identifier;
            try
            {
                if (_token == null)
                {
                    Tracer.GlobalTracer.TraceInfo("Fetching netdisk main page");

                    ns.HttpGet("https://pan.baidu.com/");
                    ns.Close();

                    _token = _v2_api__getapi(_gid);
                }
            }
            finally
            {
                ns.Close();
            }
            return _token;
        }
        public bool Login(string username, string password, object captcha = null)
        {
            var ns = new NetStream();
            ns.CookieKey = _identifier;
            try
            {
                _get_token();

                var captcha_data = _v2_api__logincheck(_token, username);
                if (!string.IsNullOrEmpty(captcha_data.codestring))
                {
                    // todo: handle this
                    _codestring = captcha_data.codestring;
                    _vcodetype = captcha_data.vcodetype;
                    throw new NotImplementedException("not implemented scene: logincheck with codestring != empty");
                }

                var rsa_key = _v2_getpublickey(_token, _gid);

                var login_result = _v2_api__login(_token, _codestring, _gid, username, password, rsa_key.key, rsa_key.pubkey, (string)captcha);
                if (!string.IsNullOrEmpty(login_result.vcodetype)) _vcodetype = login_result.vcodetype;
                if (!string.IsNullOrEmpty(login_result.codestring)) _codestring = login_result.codestring;
                switch (login_result.errno)
                {
                    case "0":
                        return true;
                    case "4":
                        throw new WrongPasswordException();
                    case "257":
                        throw new CaptchaRequiredException();
                    default:
                        throw new LoginFailedException("Login failed with response code " + login_result.errno);
                }

            }
            catch (LoginFailedException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw new LoginFailedException("Login failed", ex);
            }
        }

        public bool Logout()
        {
            throw new NotImplementedException();
        }
    }
}
