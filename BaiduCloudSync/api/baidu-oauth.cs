using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using GlobalUtil;
using GlobalUtil.NetUtils;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Net;
using System.Threading;

namespace BaiduCloudSync
{

    /// <summary>
    /// 百度登陆验证
    /// </summary>
    public class BaiduOAuth
    {

        #region constants
        private const string _OAUTH_GETAPI_URL = "https://passport.baidu.com/v2/api/?getapi";
        private const string _OAUTH_LOGINCHECK_URL = "https://passport.baidu.com/v2/api/?logincheck";
        private const string _OAUTH_LOGIN_URL = "https://passport.baidu.com/v2/api/?login";
        private const string _OAUTH_CAPTCHA_URL = "https://passport.baidu.com/cgi-bin/genimage";
        private const string _OAUTH_CHECKVCODE_URL = "https://passport.baidu.com/v2/?checkvcode";
        private const string _OAUTH_REGET_CODESTR_URL = "https://passport.baidu.com/v2/?reggetcodestr";
        private const string _BAIDU_ROOT_URL = "https://www.baidu.com/";
        private const string _PAN_ROOT_URL = "https://pan.baidu.com/";
        #endregion

        #region login request
        #region private functions
        /// <summary>
        /// 生成guid
        /// </summary>
        /// <returns></returns>
        private string _get_guid()
        {
            return Guid.NewGuid().ToString().ToUpper();
        }
        /// <summary>
        /// 获取当前的时间戳
        /// </summary>
        /// <returns></returns>
        private long _get_unixtime()
        {
            return Convert.ToInt64(util.ToUnixTimestamp(DateTime.Now) * 1000);
        }
        /// <summary>
        /// 获取回调函数中的json值
        /// </summary>
        /// <param name="strIn">输入的字符串</param>
        /// <returns></returns>
        private string _escape_callback_function(string strIn)
        {
            return Regex.Match(strIn, @"^bd__cbs__([0-9a-zA-Z]+)\((?<json>.*)\)\s*$").Result("${json}");
        }

        // method implementation of GET /v2/api/?getapi
        private string _v2_api__getapi(string gid)
        {
            var ns = new NetStream();
            ns.CookieKey = _cookie_identifier;

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
                api_result = _escape_callback_function(api_result);
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
            ns.CookieKey = _cookie_identifier;

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
                api_result = _escape_callback_function(api_result);

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
            ns.CookieKey = _cookie_identifier;
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
                api_result = _escape_callback_function(api_result);

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
            ns.CookieKey = _cookie_identifier;

            try
            {
                Tracer.GlobalTracer.TraceInfo("Posting: login");

                // 对明文密码进行RSA，并转换为base64的字符串
                var rsa_param = Crypto.RSA_ImportPEMPublicKey(rsa_publickey); //导入public key
                var encrypted_password = Crypto.RSA_StringEncrypt(password, rsa_param); //加密
                password = Convert.ToBase64String(encrypted_password); //to base64

                // 登陆要POST的数据
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
            ns.CookieKey = _cookie_identifier;
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

        // 这里的单词好像是re-g-get-code-str，不知道是不是我英语不行
        // method implementation of /v2/?reggetcodestr
        private struct _regetcodestr_result
        {
            public string verifysign;
            public string verifystr;
        }
        private _regetcodestr_result _v2__reggetcodestr(string token, string vcodetype)
        {
            var ns = new NetStream();
            ns.CookieKey = _cookie_identifier;

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
                api_result = _escape_callback_function(api_result);

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


        //初始化token并返回token
        private string _get_token()
        {
            var ns = new NetStream();
            ns.CookieKey = _cookie_identifier;
            try
            {
                if (_token == null)
                {
                    Tracer.GlobalTracer.TraceInfo("Fetching netdisk main page");

                    ns.HttpGet("https://pan.baidu.com/");
                    ns.Close();

                    _token = _v2_api__getapi(_get_guid());
                }
            }
            finally
            {
                ns.Close();
            }
            return _token;
        }
        
        #endregion

        #region login variables
        //用户名和明文密码
        private string _username, _password;
        //token 验证码code跟验证码
        private string _token, _codestring, _verifycode;
        //vcodetype...不知道怎么形容好
        private string _vcodetype;

        //分辨多用户的标识key
        private string _cookie_identifier;
        private bool _captcha_generated;
        public string CookieIdentifier { get { return _cookie_identifier; } }
        public BaiduOAuth(string cookieKey = null)
        {
            //由表单边界格式生成cookie
            _cookie_identifier = string.IsNullOrEmpty(cookieKey) ? util.GenerateFormDataBoundary() : cookieKey;
            _username = "";
            _password = "";
            _token = "";
            _verifycode = "";
            _codestring = "";
            _vcodetype = "";
            _captcha_generated = false;
        }

        #endregion

        #region public login functions
        /// <summary>
        /// 登陆到百度
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码</param>
        /// <returns>返回登陆是否成功</returns>
        /// <remarks>no throw, return false if failed</remarks>
        public bool Login(string username, string password)
        {

            Tracer.GlobalTracer.TraceInfo("BaiduOAuth.Login called: username=" + username + ", password=*");
            _username = username;
            _password = password;
            try
            {
                // #1 HTTP request: token request
                _get_token();

                // #2 HTTP request: login check
                var captcha_data = _v2_api__logincheck(_token, username);
                //需要验证码并且验证码为空时，引发CaptchaRequiredException
                if (!string.IsNullOrEmpty(captcha_data.codestring) && _verifycode == null)
                {
                    _codestring = captcha_data.codestring;
                    _vcodetype = captcha_data.vcodetype;
                    LoginCaptchaRequired?.Invoke();
                    throw new CaptchaRequiredException();
                }

                // #3 HTTP request: get public key (RSA password encryption)
                var rsa_key = _v2_getpublickey(_token, _get_guid());

                // #4 HTTP request: post login
                var login_result = _v2_api__login(_token, _codestring, _get_guid(), username, password, rsa_key.key, rsa_key.pubkey, _verifycode);
                //对登陆结果返回的验证码字段进行赋值
                if (!string.IsNullOrEmpty(login_result.vcodetype)) _vcodetype = login_result.vcodetype;
                if (!string.IsNullOrEmpty(login_result.codestring)) _codestring = login_result.codestring;
                switch (login_result.errno)
                {
                    case "0":
                        //登陆成功
                        LoginSucceeded?.Invoke();
                        return true;
                    case "257":
                        LoginCaptchaRequired?.Invoke();
                        throw new CaptchaRequiredException();
                    default:
                        _failed_code = int.Parse(login_result.errno);
                        _failed_reason = "";
                        throw new LoginFailedException("Login failed with response code " + login_result.errno);
                }

            }
            catch (Exception ex)
            {
                Tracer.GlobalTracer.TraceError(ex);
                LoginFailed?.Invoke();
                return false;
            }
        }
        /// <summary>
        /// 获取验证码
        /// </summary>
        /// <returns>验证码图片</returns>
        /// <remarks>no throw, return 1x1 bitmap if failed</remarks>
        public Image GetCaptcha()
        {
            Tracer.GlobalTracer.TraceInfo("BaiduOAuth.GetCaptcha called: void");
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
                // first time generating the captcha
                if (string.IsNullOrEmpty(_codestring)) return null;
                _captcha_generated = true;
                return _cgi_bin_genimage(_codestring);
            }
        }
        /// <summary>
        /// 设置验证码的值
        /// </summary>
        /// <param name="verifycode">新的验证码</param>
        public void SetVerifyCode(string verifycode)
        {
            _verifycode = verifycode;
        }
        public string VerifyCode { get { return _verifycode; } set { _verifycode = value; } }
        #endregion

        #endregion

        #region event callback
        public delegate void LoginEventHandler();
        /// <summary>
        /// 登陆成功
        /// </summary>
        public event LoginEventHandler LoginSucceeded;
        /// <summary>
        /// 登陆失败
        /// </summary>
        public event LoginEventHandler LoginFailed;
        /// <summary>
        /// 登陆需验证码
        /// </summary>
        public event LoginEventHandler LoginCaptchaRequired;
        /// <summary>
        /// 登陆失败的原因
        /// </summary>
        private string _failed_reason;
        public string GetLastFailedReason { get { return _failed_reason; } }
        private int _failed_code;
        public int GetLastFailedCode { get { return _failed_code; } }
        #endregion

        #region authentication variables (from cookie)
        private string _bduss, _baiduid, _stoken;
        private void _init_login_data()
        {
            //todo: 支持更改key
            if (!NetStream.DefaultCookieContainer.ContainsKey(_cookie_identifier)) return;
            var cc = NetStream.DefaultCookieContainer[_cookie_identifier].GetCookies(new Uri("https://passport.baidu.com/"));
            foreach (Cookie item in cc)
            {
                switch (item.Name)
                {
                    case "BDUSS":
                        _bduss = item.Value;
                        break;
                    case "BAIDUID":
                        _baiduid = item.Value;
                        break;
                    case "STOKEN":
                        _stoken = item.Value;
                        break;
                    default:
                        break;
                }
            }
        }
        public string bduss
        {
            get
            {
                if (string.IsNullOrEmpty(_bduss)) _init_login_data();
                return _bduss;
            }
        }
        public string baiduid
        {
            get
            {
                if (string.IsNullOrEmpty(_baiduid)) _init_login_data();
                return _baiduid;
            }
        }
        public string stoken
        {
            get
            {
                if (string.IsNullOrEmpty(_stoken)) _init_login_data();
                return _stoken;
            }
        }
        #endregion

        #region account info
        private string _nickname;
        /// <summary>
        /// 用户名称
        /// </summary>
        public string NickName { get { if (string.IsNullOrEmpty(_nickname)) _init_user_nickname(); return _nickname; } }

        /// <summary>
        /// 获取当前用户的名称
        /// </summary>
        /// <returns></returns>
        private void _init_user_nickname()
        {
            try
            {
                var ns = new NetStream();
                ns.CookieKey = _cookie_identifier;
                ns.RetryDelay = 1000;
                ns.RetryTimes = 3;
                ns.HttpGet(_BAIDU_ROOT_URL);
                var response_data = ns.ReadResponseString().Replace("\r", "").Replace("\n", "");
                var reg = Regex.Match(response_data, "bds\\.comm\\.user\\s*=\\s*\"(?<user>[^\"]*)\";");
                if (reg.Success)
                    _nickname = reg.Result("${user}");
                ns.Close();
            }
            catch (Exception ex)
            {
                Tracer.GlobalTracer.TraceError(ex);
            }
        }
        #endregion

        #region pcs api auth
        private Thread __next_update_thread;
        private object _external_auth_lock = new object();
        //登陆的一些参数，由抓包得来
        private void _init_pcs_auth_data()
        {
            lock (_external_auth_lock)
            {
                if (!string.IsNullOrEmpty(_sign2) && (DateTime.Now - util.FromUnixTimestamp(long.Parse(_timestamp))).TotalMinutes < 10) return;
                Tracer.GlobalTracer.TraceInfo("BaiduOAuth._init_pcs_auth_data called: void");
                try
                {
                    var ns = new NetStream();
                    ns.CookieKey = _cookie_identifier;
                    ns.RetryDelay = 1000;
                    ns.RetryTimes = 3;
                    ns.HttpGet(_PAN_ROOT_URL);

                    var str = ns.ReadResponseString();
                    ns.Close();

                    //_trace.TraceInfo(str);

                    var match = Regex.Match(str, "\"bdstoken\":\"(\\w+)\"");
                    if (match.Success) _bdstoken = match.Result("$1");
                    match = Regex.Match(str, "\"sign1\":\"(\\w+)\"");
                    if (match.Success) _sign1 = match.Result("$1");
                    match = Regex.Match(str, "\"sign3\":\"(\\w+)\"");
                    if (match.Success) _sign3 = match.Result("$1");
                    match = Regex.Match(str, "\"timestamp\":(\\d+)");
                    if (match.Success) _timestamp = match.Result("$1");

                    //calculate for sign2
                    var j = Encoding.UTF8.GetBytes(_sign3);
                    var r = Encoding.UTF8.GetBytes(_sign1);
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
                    _sign2 = Convert.ToBase64String(o);

                    Tracer.GlobalTracer.TraceInfo("Initialization complete.\r\nbdstoken=" + _bdstoken + "\r\nsign1=" + _sign1 + "\r\nsign2=" + _sign2 + "\r\nsign3=" + _sign3 + "\r\ntimestamp=" + _timestamp);
                    //test
                    TestFunc();

                    //next update thread
                    if (__next_update_thread != null)
                    {
                        try { var thd = __next_update_thread; __next_update_thread = null; ThreadPool.QueueUserWorkItem(delegate { thd.Abort(); }); } catch { }
                    }
                    __next_update_thread = new Thread(() =>
                    {
                        var ts = TimeSpan.FromMinutes(10);
                        Thread.Sleep(ts);
                        __next_update_thread = null;
                        _init_pcs_auth_data();
                    });
                    __next_update_thread.IsBackground = true;
                    __next_update_thread.Name = "网盘登陆数据刷新线程";
                    __next_update_thread.Start();
                }
                catch (ThreadAbortException) { }
                catch (Exception ex)
                {
                    Tracer.GlobalTracer.TraceError(ex.ToString());
                    //next update thread (exception raised mode)
                    if (__next_update_thread != null)
                    {
                        try { var thd = __next_update_thread; __next_update_thread = null; ThreadPool.QueueUserWorkItem(delegate { thd.Abort(); }); } catch { }
                    }
                    __next_update_thread = new Thread(() =>
                    {
                        var ts = TimeSpan.FromSeconds(15);
                        Thread.Sleep(ts);
                        _init_pcs_auth_data();
                        __next_update_thread = null;
                    });
                    __next_update_thread.IsBackground = true;
                    __next_update_thread.Name = "网盘登陆数据刷新线程";
                    __next_update_thread.Start();
                    __next_update_thread.Join();
                }
            }
        }
        private string _bdstoken, _sign1, _sign2, _sign3, _timestamp;
        public string bdstoken
        {
            get
            {
                if (string.IsNullOrEmpty(_bdstoken))
                    _init_pcs_auth_data();
                return _bdstoken;
            }
        }
        public string sign1
        {
            get
            {
                if (string.IsNullOrEmpty(_sign1))
                    _init_pcs_auth_data();
                return _sign1;
            }
        }
        public string sign2
        {
            get
            {
                if (string.IsNullOrEmpty(_sign2))
                    _init_pcs_auth_data();
                return _sign2;
            }
        }
        public string sign3
        {
            get
            {
                if (string.IsNullOrEmpty(_sign3))
                    _init_pcs_auth_data();
                return _sign3;
            }
        }
        public string timestamp
        {
            get
            {
                if (string.IsNullOrEmpty(_timestamp))
                    _init_pcs_auth_data();
                return _timestamp;
            }
        }
        private void TestFunc()
        {
            var url = "https://pan.baidu.com/api/report/user";
            var query_param = new Parameters();
            query_param.Add("channel", "chunlei");
            query_param.Add("web", 1);
            query_param.Add("app_id", BaiduPCS.APPID);
            query_param.Add("bdstoken", _bdstoken);
            query_param.Add("logid", BaiduPCS.GetLogid());
            query_param.Add("clienttype", 0);

            var post_param = new Parameters();
            post_param.Add("timestamp", (long)util.ToUnixTimestamp(DateTime.Now));
            post_param.Add("action", "fm_self");

            var ns = new NetStream();
            ns.CookieKey = _cookie_identifier;

            var header = new Parameters();

            header.Add("X-Requested-With", "XMLHttpRequest");
            header.Add("Origin", "http://pan.baidu.com");
            header.Add("Referer", BaiduPCS.BAIDU_NETDISK_URL);

            ns.HttpPost(url, post_param, headerParam: header, urlParam: query_param);
            var rep = ns.ReadResponseString();
            ns.Close();
        }
        #endregion
    }
}
