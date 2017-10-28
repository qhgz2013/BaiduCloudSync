using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using BaiduCloudSync.NetUtils;
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
        /// 获取api信息
        /// </summary>
        /// <returns>api token</returns>
        /// <remarks>throwable</remarks>
        private JObject _oauth_getapi()
        {
            Tracer.GlobalTracer.TraceInfo("BaiduOAuth._oauth_getapi called: void");
            try
            {
                _http.HttpGet(_BAIDU_ROOT_URL);
                _http.Close();
            }
            catch (Exception)
            {

                throw new Exception("访问百度失败，请检查互联网设置");
            }
            var param = new Parameters();
            param.Add("tpl", "mn");
            param.Add("apiver", "v3");
            param.Add("tt", _get_unixtime());
            param.Add("class", "login");
            param.Add("gid", _get_guid());
            param.Add("logintype", "dialogLogin");
            param.Add("callback", "bd__cbs__7s1rgg");

            var querystring = param.BuildQueryString();
            var url = _OAUTH_GETAPI_URL + "&" + querystring;
            try
            {
                _http.HttpGet(url);
                var responseText = _http.ReadResponseString();
                _http.Close();

                //sample returns:
                /*
                 * bd__cbs__vyf7wy({"errInfo":{        "no": "0"    },    "data": {        "rememberedUserName" : "",        "codeString" : "",        "token" : "c06a130938733657c4dcf79c18f0ef5f",        "cookie" : "1",        "usernametype":"",        "spLogin" : "rate",        "disable":"",        "loginrecord":{            'email':[            ],            'phone':[            ]        }    }})
                 */
                responseText = _escape_callback_function(responseText);
                return (JObject)JsonConvert.DeserializeObject(responseText);
            }
            catch (Exception ex)
            {

                throw new Exception("获取登陆信息失败，请检查互联网设置: -1", ex);
            }
        }
        /// <summary>
        /// 登陆检测
        /// </summary>
        /// <param name="token">获得的token</param>
        /// <param name="username">用户名</param>
        /// <returns></returns>
        /// <remarks>throwable</remarks>
        private JObject _oauth_logincheck(string token, string username)
        {
            Tracer.GlobalTracer.TraceInfo("BaiduOAuth._oauth_logincheck called: token=" + token + ", username=" + username);
            var param = new Parameters();
            param.Add("token", token);
            param.Add("tpl", "mn");
            param.Add("apiver", "v3");
            param.Add("tt", _get_unixtime());
            param.Add("sub_source", "leadsetpwd");
            param.Add("username", username);
            param.Add("isphone", false);
            param.Add("callback", "bd__cbs__1nfmz7");

            var querystring = param.BuildQueryString();
            var url = _OAUTH_GETAPI_URL + "&" + querystring;
            try
            {
                _http.HttpGet(url);
                var responseText = _http.ReadResponseString();
                _http.Close();

                //sample return:
                /*
                 * bd__cbs__l0wi2m({"errInfo":{        "no": "0"    },    "data": {        "codeString" : "",        "vcodetype" : "",        "userid" : "",        "mobile" : ""    }})
                 */
                responseText = _escape_callback_function(responseText);
                return (JObject)JsonConvert.DeserializeObject(responseText);
            }
            catch (Exception ex)
            {

                throw new Exception("获取登陆信息失败，请检查互联网设置: -2", ex);
            }

        }
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
        /// <summary>
        /// 登陆主函数
        /// </summary>
        /// <param name="token">token</param>
        /// <param name="username">用户名称</param>
        /// <param name="password">密码</param>
        /// <param name="codestring">验证码的code</param>
        /// <param name="verifycode">验证码的值</param>
        /// <returns></returns>
        /// <remarks>throwable</remarks>
        private string _oauth_login(string token, string username, string password, string codestring = "", string verifycode = "")
        {
            Tracer.GlobalTracer.TraceInfo("BaiduOAuth._oauth_login called: token=" + token + ", username=" + username + ", password=*, codestring=" + codestring + ", verifycode=" + verifycode);
            var param = new Parameters();
            param.Add("staticpage", "http://www.baidu.com/cache/user/html/v3Jump.html");
            param.Add("charset", "UTF-8");
            param.Add("token", token);
            param.Add("tpl", "mn");
            param.Add("subpro", "");
            param.Add("apiver", "v3");
            param.Add("tt", _get_unixtime());
            param.Add("codestring", codestring);
            param.Add("safeflg", 0);
            param.Add("u", "https://www.baidu.com/");
            param.Add("isPhone", "");
            param.Add("detect", 1);
            param.Add("gid", _get_guid());
            param.Add("quick_user", 0);
            param.Add("logintype", "dialogLogin");
            param.Add("logLoginType", "pc_loginDialog");
            param.Add("idc", "");
            param.Add("loginmerge", true);
            param.Add("splogin", "rate");
            param.Add("username", username);
            param.Add("password", password);
            if (!string.IsNullOrEmpty(codestring)) param.Add("verifycode", verifycode);
            param.Add("mem_pass", "on");
            var rand = new Random();
            param.Add("ppui_logintime", rand.Next(5000, 20000)); //i don't know what is it but it is safer to generate a random number
            param.Add("countrycode", "");
            param.Add("callback", "parent.bd__pcbs__1zc9vx");

            try
            {
                _http.HttpPost(_OAUTH_LOGIN_URL, param);
                var str = _http.ReadResponseString();
                _http.Close();

                //sample return:
                /*
                <!DOCTYPE html>
                <html>
                <head>
                <meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
                </head>
                <body>
                <script type="text/javascript">


	                var href = decodeURIComponent("https:\/\/www.baidu.com\/cache\/user\/html\/v3Jump.html")+"?"

                var accounts = '&accounts='

                href += "err_no=257&callback=parent.bd__pcbs__1zc9vx&codeString=tcG8106c18e7ffae2eb02f314e64301db7e585e980765047b18&userName=13560119976&phoneNumber=&mail=&hao123Param=&u=https://www.baidu.com/&tpl=&secstate=&gotourl=&authtoken=&loginproxy=&resetpwd=&vcodetype=24f1cjZNRjKMAuXXx\/jh6GM1UiN1NrTR\/1rG+Lvz++HwkK+stBNbKqZSN1uOvaY1ZsPBcGKEcbDe2\/Gkp0z1b5YMEXNUP\/hqug99&lstr=&ltoken=&bckv=&bcsync=&bcchecksum=&code=&bdToken=&realnameswitch=&setpwdswitch=&bctime=&bdstoken=&authsid=&jumpset=&appealurl="+accounts;


                if(window.location){
                    window.location.replace(href);
                }else{
                   document.location.replace(href); 
                }
                </script>
                </body>
                </html>
                */
                return str;
            }
            catch (Exception ex)
            {

                throw new Exception("登陆请求发送失败", ex);
            }
        }
        /// <summary>
        /// 获取验证码图片
        /// </summary>
        /// <param name="codestring">验证码的code</param>
        /// <returns></returns>
        /// <remarks>throwable</remarks>
        private Image _oauth_genimage(string codestring)
        {
            Tracer.GlobalTracer.TraceInfo("BaiduOAuth._oauth_genimage called: codestring=" + codestring);
            try
            {
                var url = _OAUTH_CAPTCHA_URL + "?" + codestring;
                _http.HttpGet(url);

                //transfer to local memory stream
                var ss = new MemoryStream();
                int readcount = 0;
                byte[] buf = new byte[4096];
                do
                {
                    readcount = _http.ResponseStream.Read(buf, 0, 4096);
                    ss.Write(buf, 0, readcount);
                } while (readcount != 0);
                ss.Position = 0;

                //creating image from local memory stream
                //sample return: an image(binary data)
                return Image.FromStream(ss);
            }
            catch (Exception ex)
            {

                throw new Exception("获取验证码图片失败", ex);
            }
        }
        /// <summary>
        /// 检查验证码是否输入正确
        /// </summary>
        /// <param name="token">token</param>
        /// <param name="codestring">验证码code</param>
        /// <param name="verifycode">验证码值</param>
        /// <returns></returns>
        /// <remarks>throwable</remarks>
        private JObject _oauth_checkvcode(string token, string codestring, string verifycode)
        {
            Tracer.GlobalTracer.TraceInfo("BaiduOAuth._oauth_checkvcode called: token=" + token + ", codestring=" + codestring + ", verifycode=" + verifycode);
            var param = new Parameters();
            param.Add("token", token);
            param.Add("tpl", "mn");
            param.Add("apiver", "v3");
            param.Add("tt", _get_unixtime());
            param.Add("verifycode", verifycode);
            param.Add("codestring", codestring);
            param.Add("callback", "bd__cbs__1l7o0h");

            var querystring = param.BuildQueryString();
            var url = _OAUTH_CHECKVCODE_URL + "&" + querystring;
            try
            {
                _http.HttpGet(url);
                var responseText = _http.ReadResponseString();
                _http.Close();
                //sample returns:
                /*
                 * bd__cbs__1l7o0h({"errInfo":{        "no": "500002",        "msg": "验证码错误."    },    "data": {    }})
                 * bd__cbs__gbeza5({"errInfo":{        "no": "0",        "msg": ""    },    "data": {    }})
                */
                responseText = _escape_callback_function(responseText);
                return (JObject)JsonConvert.DeserializeObject(responseText);
            }
            catch (Exception ex)
            {

                throw new Exception("检查验证码失败", ex);
            }
        }
        /// <summary>
        /// 重新获得验证码的code
        /// </summary>
        /// <param name="token">token</param>
        /// <param name="vcodetype"></param>
        /// <returns></returns>
        private JObject _reget_codestring(string token, string vcodetype)
        {
            Tracer.GlobalTracer.TraceInfo("BaiduOAuth._reget_codestring called: token=" + token + ", vcodetype=" + vcodetype);
            var param = new Parameters();
            param.Add("token", token);
            param.Add("tpl", "mn");
            param.Add("apiver", "v3");
            param.Add("tt", _get_unixtime());
            param.Add("fr", "login");
            param.Add("vcodetype", vcodetype);
            param.Add("callback", "bd__cbs__rxnynd");

            var url = _OAUTH_REGET_CODESTR_URL + "&" + param.BuildQueryString();
            try
            {
                _http.HttpGet(url);
                var str = _http.ReadResponseString();
                _http.Close();
                var json = _escape_callback_function(str);
                return (JObject)JsonConvert.DeserializeObject(json);
            }
            catch (Exception ex)
            {
                throw new Exception("重新获取验证码失败", ex);
            }

        }
        #endregion

        #region login variables
        //用户名和明文密码
        private string _username, _password;
        //token 验证码code跟验证码
        private string _token, _codestring, _verifycode;
        //vcodetype...不知道怎么形容好
        private string _vcodetype;

        private NetStream _http;
        //分辨多用户的标识key
        private string _cookie_identifier;
        public string CookieIdentifier { get { return _cookie_identifier; } }
        public BaiduOAuth(string cookieKey = null)
        {
            _http = new NetStream();
            //由表单边界格式生成cookie
            _cookie_identifier = string.IsNullOrEmpty(cookieKey) ? util.GenerateFormDataBoundary() : cookieKey;
            _http.CookieKey = _cookie_identifier;
            _http.RetryDelay = 1000;
            _http.RetryTimes = 3;
            _username = "";
            _password = "";
            _token = "";
            _verifycode = "";
            _codestring = "";
            _vcodetype = "";
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
                var json_getapi = _oauth_getapi();
                var status = json_getapi["errInfo"].Value<string>("no");
                if (status == "0")
                {
                    _token = json_getapi["data"].Value<string>("token");
                    if (string.IsNullOrEmpty(_token)) throw new ArgumentNullException("获取的token为空");
                }
                else
                    throw new ArgumentException("获取错误：code=" + status);

                var json_logincheck = _oauth_logincheck(_token, _username);
                status = json_getapi["errInfo"].Value<string>("no");
                if (status != "0")
                    throw new ArgumentException("获取错误：code=" + status);

                var html_login = _oauth_login(_token, _username, _password, _codestring, _verifycode);
                var html_login_noline = html_login.Replace("\r", "").Replace("\n", "");
                var errno = int.Parse(Regex.Match(html_login_noline, @"err_no=(?<errno>\d+)").Result("${errno}"));
                _failed_code = errno;
                _codestring = "";
                _vcodetype = "";
                switch (errno)
                {
                    case 0:
                        LoginSucceeded?.Invoke();
                        _init_login_data();
                        return true;
                    case 2:
                        _failed_reason = "2: 用户名或密码错误";
                        LoginFailed?.Invoke();
                        return false;
                    case 7:
                        _failed_reason = "7: 密码错误";
                        LoginFailed?.Invoke();
                        return false;
                    case 257:
                        _failed_reason = "257: 需要验证码";
                        _codestring = Regex.Match(html_login_noline, @"codeString=(?<codeString>[0-9a-zA-Z]*)").Result("${codeString}");
                        _vcodetype = Regex.Match(html_login_noline, @"vcodetype=(?<vcodetype>[0-9a-zA-Z\\/\+=]+)").Result("${vcodetype}");
                        _vcodetype = _vcodetype.Replace(@"\/", @"/");
                        LoginCaptchaRequired?.Invoke();
                        LoginFailed?.Invoke();
                        return false;
                    default:
                        _failed_reason = errno + ": 未知错误";
                        LoginFailed?.Invoke();
                        return false;
                }
            }
            catch (Exception ex)
            {
                _failed_reason = ex.ToString();
                Tracer.GlobalTracer.TraceError(_failed_reason);
                LoginExceptionRaised?.Invoke();
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
            try
            {
                return _oauth_genimage(_codestring);
            }
            catch (Exception ex)
            {
                _failed_reason = ex.ToString();
                Tracer.GlobalTracer.TraceError(_failed_reason);
                LoginExceptionRaised?.Invoke();
                return new Bitmap(1, 1);
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
        /// <summary>
        /// 检查验证码是否输入正确
        /// </summary>
        /// <param name="verifycode">验证码</param>
        /// <returns>验证码是否正确</returns>
        /// <remarks>no throw, return false if failed</remarks>
        public bool CheckVCode(string verifycode)
        {
            Tracer.GlobalTracer.TraceInfo("BaiduOAuth.CheckVCode called: verifycode=" + verifycode);
            if (string.IsNullOrEmpty(verifycode)) return false;
            _verifycode = verifycode;
            try
            {
                JObject json = _oauth_checkvcode(_token, _codestring, _verifycode);
                var errno = json["errInfo"].Value<string>("no");
                var errmsg = json["errInfo"].Value<string>("msg");

                if (errno == "0")
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                _failed_reason = ex.ToString();
                Tracer.GlobalTracer.TraceError(_failed_reason);
                LoginExceptionRaised?.Invoke();
                return false;
            }
        }
        /// <summary>
        /// 刷新验证码
        /// </summary>
        /// <returns>新的验证码</returns>
        /// <remarks>no throw, return 1x1 bitmap if failed</remarks>
        public Image RefreshCaptcha()
        {
            Tracer.GlobalTracer.TraceInfo("BaiduOAuth.RefreshCaptcha called: void");
            try
            {
                var json = _reget_codestring(_token, _vcodetype);
                _codestring = json["data"].Value<string>("verifyStr");
                return _oauth_genimage(_codestring);
            }
            catch (Exception ex)
            {
                _failed_reason = ex.ToString();
                Tracer.GlobalTracer.TraceError(_failed_reason);
                LoginExceptionRaised?.Invoke();
                return new Bitmap(1, 1);
            }
        }

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
        /// 登陆过程发生异常错误
        /// </summary>
        public event LoginEventHandler LoginExceptionRaised;
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
                _http.HttpGet(_BAIDU_ROOT_URL);
                var response_data = _http.ReadResponseString();
                var reg = Regex.Match(response_data, "bds.comm.user\\s*=\\s*\"(?<user>[^\"])\";");
                if (reg.Success)
                    _nickname = reg.Result("${user}");
                _http.Close();
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
                if (!string.IsNullOrEmpty(_sign2)) return;
                Tracer.GlobalTracer.TraceInfo("BaiduOAuth._init_pcs_auth_data called: void");
                try
                {
                    _http.HttpGet(_PAN_ROOT_URL);

                    var str = _http.ReadResponseString();
                    _http.Close();

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
                    //TestFunc();

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
                        _init_login_data();
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
        //private void TestFunc()
        //{
        //    var url = "http://pan.baidu.com/api/report/user";
        //    var query_param = new Parameters();
        //    query_param.Add("channel", "chunlei");
        //    query_param.Add("web", 1);
        //    query_param.Add("app_id", APPID);
        //    query_param.Add("bdstoken", _bdstoken);
        //    query_param.Add("logid", _get_logid());
        //    query_param.Add("clienttype", 0);

        //    var post_param = new Parameters();
        //    post_param.Add("timestamp", (long)util.ToUnixTimestamp(DateTime.Now));
        //    post_param.Add("action", "fm_self");

        //    var ns = new NetStream();
        //    ns.CookieKey = _auth.CookieIdentifier;
        //    ns.HttpPost(url, post_param, headerParam: _get_xhr_param(), urlParam: query_param);
        //    var rep = ns.ReadResponseString();
        //    ns.Close();
        //}
        #endregion
    }
}
