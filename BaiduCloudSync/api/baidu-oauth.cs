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

namespace BaiduCloudSync
{

    /// <summary>
    /// 百度登陆验证
    /// </summary>
    public class BaiduOAuth
    {
        private static NetStream _http;
        static BaiduOAuth()
        {
            _http = new NetStream();
            _http.RetryDelay = 1000;
            _http.RetryTimes = 3;
            _username = "";
            _password = "";
            _token = "";
            _verifycode = "";
            _codestring = "";
            _vcodetype = "";
        }


        private const string _OAUTH_GETAPI_URL = "https://passport.baidu.com/v2/api/?getapi";
        private const string _OAUTH_LOGINCHECK_URL = "https://passport.baidu.com/v2/api/?logincheck";
        private const string _OAUTH_LOGIN_URL = "https://passport.baidu.com/v2/api/?login";
        private const string _OAUTH_CAPTCHA_URL = "http://passport.baidu.com/cgi-bin/genimage";
        private const string _OAUTH_CHECKVCODE_URL = "https://passport.baidu.com/v2/?checkvcode";
        private const string _OAUTH_REGET_CODESTR_URL = "https://passport.baidu.com/v2/?reggetcodestr";
        private const string _BAIDU_ROOT_URL = "https://www.baidu.com/";
        /// <summary>
        /// 获取api信息
        /// </summary>
        /// <returns>api token</returns>
        /// <remarks>throwable</remarks>
        private static JObject _oauth_getapi()
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
        private static JObject _oauth_logincheck(string token, string username)
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
        private static string _get_guid()
        {
            return Guid.NewGuid().ToString().ToUpper();
        }
        /// <summary>
        /// 获取当前的时间戳
        /// </summary>
        /// <returns></returns>
        private static long _get_unixtime()
        {
            return Convert.ToInt64(util.ToUnixTimestamp(DateTime.Now) * 1000);
        }
        /// <summary>
        /// 获取回调函数中的json值
        /// </summary>
        /// <param name="strIn">输入的字符串</param>
        /// <returns></returns>
        private static string _escape_callback_function(string strIn)
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
        private static string _oauth_login(string token, string username, string password, string codestring = "", string verifycode = "")
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
        private static Image _oauth_genimage(string codestring)
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
        private static JObject _oauth_checkvcode(string token, string codestring, string verifycode)
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
        private static JObject _reget_codestring(string token, string vcodetype)
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
        //用户名和明文密码
        private static string _username, _password;
        //token 验证码code跟验证码
        private static string _token, _codestring, _verifycode;
        //vcodetype...不知道怎么形容好
        private static string _vcodetype;
        public delegate void LoginEventHandler();
        /// <summary>
        /// 登陆成功
        /// </summary>
        public static event LoginEventHandler LoginSucceeded;
        /// <summary>
        /// 登陆失败
        /// </summary>
        public static event LoginEventHandler LoginFailed;
        /// <summary>
        /// 登陆需验证码
        /// </summary>
        public static event LoginEventHandler LoginCaptchaRequired;
        /// <summary>
        /// 登陆过程发生异常错误
        /// </summary>
        public static event LoginEventHandler LoginExceptionRaised;
        /// <summary>
        /// 登陆失败的原因
        /// </summary>
        private static string _failed_reason;
        public static string GetLastFailedReason { get { return _failed_reason; } }
        private static int _failed_code;
        public static int GetLastFailedCode { get { return _failed_code; } }
        private static string _bduss, _baiduid, _stoken;
        private static void _init_login_data()
        {
            //todo: 支持更改key
            if (!NetStream.DefaultCookieContainer.ContainsKey("default")) return;
            var cc = NetStream.DefaultCookieContainer["default"].GetCookies(new Uri("https://www.baidu.com/"));
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
        public static string bduss
        {
            get
            {
                if (string.IsNullOrEmpty(_bduss)) _init_login_data();
                return _bduss;
            }
        }
        public static string baiduid
        {
            get
            {
                if (string.IsNullOrEmpty(_baiduid)) _init_login_data();
                return _baiduid;
            }
        }
        public static string stoken
        {
            get
            {
                if (string.IsNullOrEmpty(_stoken)) _init_login_data();
                return _stoken;
            }
        }
        /// <summary>
        /// 登陆到百度
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码</param>
        /// <returns>返回登陆是否成功</returns>
        /// <remarks>no throw, return false if failed</remarks>
        public static bool Login(string username, string password)
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
        public static Image GetCaptcha()
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
        public static void SetVerifyCode(string verifycode)
        {
            _verifycode = verifycode;
        }
        public static string VerifyCode { get { return _verifycode; } set { _verifycode = value; } }
        /// <summary>
        /// 检查验证码是否输入正确
        /// </summary>
        /// <param name="verifycode">验证码</param>
        /// <returns>验证码是否正确</returns>
        /// <remarks>no throw, return false if failed</remarks>
        public static bool CheckVCode(string verifycode)
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
        public static Image RefreshCaptcha()
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
        
    }
}
