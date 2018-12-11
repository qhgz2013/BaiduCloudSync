using System;
using System.Collections.Generic;
using GlobalUtil;
using GlobalUtil.http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using System.Drawing;
using System.IO;

namespace BaiduCloudSync.oauth
{
    public class OAuthPCWebImpl : IOAuth
    {
        // 配置文件
        private Config _config;
        // 用于区分不同账号的cookie所需要的key
        private string _identifier;
        // 用于重复调用login需要的持久变量
        private string _token; // 登陆所需的token，惰性加载，null时执行初始化（调用http请求）
        private string _gid; // 随机生成的guid，只不过是31位的
        // 验证码相关
        private string _codestring;
        private string _vcodetype;

        private bool _captcha_generated; // 指示验证码是否已经生成，用于刷新验证码
        public OAuthPCWebImpl(string identifier = null, Config config = null)
        {
            if (identifier == null)
            {
                // 通过表单的随机生成算法生成当前cookie所属的key
                identifier = util.GenerateFormDataBoundary();
            }
            if (config == null)
            {
                // 使用全局配置
                config = Config.GlobalConfig;
            }
            _identifier = identifier;
            _config = config;
            _gid = Guid.NewGuid().ToString().Substring(1);
        }
        /// <summary>
        /// 获取一个新的验证码，返回一个System.Drawing.Image实例
        /// </summary>
        /// <returns></returns>
        public object GetCaptcha()
        {
            _get_token();
            if (string.IsNullOrEmpty(_vcodetype)) return null;
            if (_captcha_generated)
            {
                // re-generate the captcha
                var new_param = _v2__reggetcodestr(_token, _vcodetype);
                Tracer.GlobalTracer.TraceInfo(new_param.ToString());
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

        public bool IsLogin
        {
            get
            {
                if (HttpSession.DefaultCookieContainer.ContainsKey(_identifier))
                {
                    var cookies = HttpSession.DefaultCookieContainer[_identifier].GetCookies(new Uri("https://passport.baidu.com/"));
                    foreach (System.Net.Cookie item in cookies)
                    {
                        if (item.Name.ToLower() == "stoken")
                            return true;
                    }
                }
                return false;
            }
        }

        // 下面这一堆函数都是跟服务器通讯的，又臭又长，还要来一堆异常处理
        // ps: 有个参数dv是根据js随机生成的，我没看js代码，这个字段我用了一个固定值来代替，暂时没有影响登陆的过程

        // method implementation of GET /v2/api/?getapi
        private string _v2_api__getapi(string gid)
        {
            var ns = new HttpSession();
            ns.CookieGroup = _identifier;

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

                ns.HttpGet("https://passport.baidu.com/v2/api/?getapi&" + query_param.BuildQueryString(), header: referer);
                var api_result = ns.ReadResponseString();
                api_result = util.EscapeCallbackFunction(api_result);
                var json_api_result = JsonConvert.DeserializeObject(api_result) as JObject;
                var errno = json_api_result["errInfo"].Value<string>("no");
                if (errno != "0") throw new LoginFailedException("failed to get token: " + json_api_result.ToString());

                return json_api_result["data"].Value<string>("token");
            }
            catch (LoginFailedException)
            {
                throw;
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
            public override string ToString()
            {
                return "{codestring=" + codestring + ", vcodetype=" + vcodetype + "}";
            }
        }
        private _logincheck_result _v2_api__logincheck(string token, string username)
        {
            var ns = new HttpSession();
            ns.CookieGroup = _identifier;

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

                ns.HttpGet("https://passport.baidu.com/v2/api/?logincheck&" + param.BuildQueryString(), header: referer);

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
            catch (LoginFailedException)
            {
                throw;
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
            public override string ToString()
            {
                return "{key=" + key + ", pubkey=" + pubkey + "}";
            }
        }
        private _getpublickey_result _v2_getpublickey(string token, string gid)
        {
            var ns = new HttpSession();
            ns.CookieGroup = _identifier;
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

                ns.HttpGet("https://passport.baidu.com/v2/getpublickey", query: param, header: referer);

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
            catch (LoginFailedException)
            {
                throw;
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
            public override string ToString()
            {
                return "{errno=" + errno + ", codestring=" + codestring + ", vcodetype=" + vcodetype + "}";
            }
        }
        private _login_result _v2_api__login(string token, string codestring, string gid, string username, string password, string rsakey, string rsa_publickey, string verify_code)
        {
            var ns = new HttpSession();
            ns.CookieGroup = _identifier;

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

                ns.HttpPost("https://passport.baidu.com/v2/api/?login", body, header: header);

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

                Tracer.GlobalTracer.TraceInfo("Regex has parsed the following params from HTML:");
                foreach (var item in response_key_values)
                    Tracer.GlobalTracer.TraceInfo(item.Key + ": " + item.Value);

                if (!response_key_values.TryGetValue("err_no", out ret.errno)) throw new LoginFailedException("could not get errno from HTML response");
                if (!response_key_values.TryGetValue("codeString", out ret.codestring)) throw new LoginFailedException("could not get codestring from HTML response");
                if (!response_key_values.TryGetValue("vcodetype", out ret.vcodetype)) throw new LoginFailedException("could not get vcodetype from HTML response");

                _captcha_generated = false;
                return ret;
            }
            catch (LoginFailedException)
            {
                throw;
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
            var ns = new HttpSession();
            ns.CookieGroup = _identifier;
            try
            {
                Tracer.GlobalTracer.TraceInfo("Fetching: genimage");

                ns.HttpGet("https://passport.baidu.com/cgi-bin/genimage?" + codestring);
                var binary_data = ns.ReadResponseBinary();
                var ms = new MemoryStream(binary_data);
                return Image.FromStream(ms);
            }
            catch (Exception)
            {
                throw;
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
            public override string ToString()
            {
                return "{verifysign=" + verifysign + ", verifystr=" + verifystr + "}";
            }
        }
        private _regetcodestr_result _v2__reggetcodestr(string token, string vcodetype)
        {
            var ns = new HttpSession();
            ns.CookieGroup = _identifier;

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

                ns.HttpGet("https://passport.baidu.com/v2/?reggetcodestr&" + param.BuildQueryString(), header: referer);

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


        //初始化token并返回token
        private string _get_token()
        {
            var ns = new HttpSession();
            ns.CookieGroup = _identifier;
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
        /// <summary>
        /// 通过网盘的登陆接口登陆到百度
        /// </summary>
        /// <param name="username">用户名/手机/邮箱</param>
        /// <param name="password">密码</param>
        /// <param name="captcha">验证码，不需要时为null，在调用Login捕获到CaptchaRequiredException或是InvalidCaptchaException时，都需要传入该参数</param>
        /// <returns>登陆是否成功</returns>
        /// <exception cref="InvalidCaptchaException">验证码错误时引发的异常</exception>
        /// <exception cref="CaptchaRequiredException">需要验证码才能进行登陆时引发的异常</exception>
        /// <exception cref="WrongPasswordException">密码错误时引发的异常</exception>
        /// <exception cref="LoginFailedException">其他原因造成登陆失败时引发的异常</exception>
        /// <exception cref="NotImplementedException">登陆的状态码对应的处理方式仍未实现时引发的异常</exception>
        public bool Login(string username, string password, object captcha = null)
        {
            try
            {
                // #1 HTTP request: token request
                _get_token();

                // #2 HTTP request: login check
                var captcha_data = _v2_api__logincheck(_token, username);
                Tracer.GlobalTracer.TraceInfo(captcha_data.ToString());
                //需要验证码并且验证码为空时，引发CaptchaRequiredException
                if (!string.IsNullOrEmpty(captcha_data.codestring) && captcha == null)
                {
                    _codestring = captcha_data.codestring;
                    _vcodetype = captcha_data.vcodetype;
                    throw new CaptchaRequiredException();
                }

                // #3 HTTP request: get public key (RSA password encryption)
                var rsa_key = _v2_getpublickey(_token, _gid);
                Tracer.GlobalTracer.TraceInfo(rsa_key.ToString());

                // #4 HTTP request: post login
                var login_result = _v2_api__login(_token, _codestring, _gid, username, password, rsa_key.key, rsa_key.pubkey, (string)captcha);
                Tracer.GlobalTracer.TraceInfo(login_result.ToString());
                // 对登陆结果返回的验证码字段进行赋值
                if (!string.IsNullOrEmpty(login_result.vcodetype)) _vcodetype = login_result.vcodetype;
                if (!string.IsNullOrEmpty(login_result.codestring)) _codestring = login_result.codestring;
                // jump as whatever it likes
                switch (login_result.errno)
                {
                    case "0":
                        // 登陆成功
                        HttpSession.SaveCookie(_config.CookieFileName);
                        return true;
                    // https://my.oschina.net/mingyuejingque/blog/521176
                    case "-1":
                    case "100005":
                        throw new LoginFailedException("系统错误,请您稍后再试", fail_code: int.Parse(login_result.errno));
                    case "1":
                        throw new LoginFailedException("输入的账号格式不正确", fail_code: int.Parse(login_result.errno));
                    case "2":
                        throw new LoginFailedException("输入的账号不存在", fail_code: int.Parse(login_result.errno));
                    case "3":
                    case "200010":
                        throw new InvalidCaptchaException("验证码不存在或已过期，请重新输入");
                    case "4":
                        throw new WrongPasswordException("您输入的帐号或密码有误");
                    case "5":
                    case "120019":
                    case "120021":
                    case "400031":
                        throw new NotImplementedException("请在弹出的窗口操作,或重新登录");
                    case "6":
                        throw new InvalidCaptchaException("您输入的验证码有误");
                    case "7":
                        throw new WrongPasswordException("密码错误");
                    case "16":
                        throw new LoginFailedException("您的帐号因安全问题已被限制登录", fail_code: int.Parse(login_result.errno));
                    case "17":
                        throw new LoginFailedException("您的帐号已锁定", fail_code: int.Parse(login_result.errno));
                    case "257":
                        throw new CaptchaRequiredException("请输入验证码");
                    case "100023":
                        throw new LoginFailedException("开启Cookie之后才能登录", fail_code: int.Parse(login_result.errno));
                    case "110024":
                        throw new LoginFailedException("此帐号暂未激活", fail_code: int.Parse(login_result.errno));
                    case "120027":
                        throw new LoginFailedException("百度正在进行系统升级，暂时不能提供服务，敬请谅解", fail_code: int.Parse(login_result.errno));
                    case "401007":
                        throw new LoginFailedException("您的手机号关联了其他帐号，请选择登录", fail_code: int.Parse(login_result.errno));
                    case "500010":
                        throw new LoginFailedException("登录过于频繁,请24小时后再试", fail_code: int.Parse(login_result.errno));
                    default:
                        int code = -1;
                        int.TryParse(login_result.errno, out code);
                        throw new LoginFailedException("Login failed with response code " + login_result.errno, fail_code: code);
                }

            }
            catch (LoginFailedException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new LoginFailedException("Login failed", ex);
            }
        }

        /// <summary>
        /// 退出登陆
        /// </summary>
        /// <returns>是否成功退出登陆</returns>
        public bool Logout()
        {
            try
            {
                // TODO: request logout to baidu
                if (!string.IsNullOrEmpty(_identifier) && HttpSession.DefaultCookieContainer.ContainsKey(_identifier))
                    HttpSession.DefaultCookieContainer[_identifier] = new System.Net.CookieContainer();
                return true;
            }
            catch (Exception ex)
            {
                Tracer.GlobalTracer.TraceError(ex);
                return false;
            }
        }

        public string GetBaiduID
        {
            get
            {
                return _get_cookie_val("baiduid");
            }
        }

        public string GetBDUSS
        {
            get
            {
                return _get_cookie_val("bduss");
            }
        }

        public string GetSToken
        {
            get
            {
                return _get_cookie_val("stoken");
            }
        }

        private string _get_cookie_val(string name)
        {
            if (!IsLogin)
                throw new NotLoggedInException();
            var cookies = HttpSession.DefaultCookieContainer[_identifier].GetCookies(new Uri("https://passport.baidu.com/"));
            foreach (System.Net.Cookie cookie in cookies)
                if (cookie.Name.ToLower() == name.ToLower())
                    return cookie.Value;
            throw new NotLoggedInException("Cookie was not found in the cookie container, please retry logging in");
        }
    }
}
