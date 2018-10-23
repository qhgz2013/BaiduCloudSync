using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace GlobalUtil.http
{
    public class HttpSession
    {
        #region Constants
        //请求方法
        public const string DEFAULT_GET_METHOD = "GET";
        public const string DEFAULT_POST_METHOD = "POST";
        public const string DEFAULT_HEAD_METHOD = "HEAD";
        public const string DEFAULT_OPTION_METHOD = "OPTION";

        //HTTP 1.1 请求头的内容
        public const string STR_SETCOOKIE = "Set-Cookie";
        public const string STR_ACCEPT_ENCODING = "Accept-Encoding";
        public const string STR_ACCEPT_LANGUAGE = "Accept-Language";
        public const string STR_HOST = "Host";
        public const string STR_CONNECTION = "Connection";
        public const string STR_ACCEPT = "Accept";
        public const string STR_USER_AGENT = "User-Agent";
        public const string STR_REFERER = "Referer";
        public const string STR_CONTENT_TYPE = "Content-Type";
        public const string STR_CONTENT_LENGTH = "Content-Length";
        public const string STR_ORIGIN = "Origin";
        public const string STR_COOKIE = "Cookie";
        public const string STR_EXPECT = "Expect";
        public const string STR_DATE = "Date";
        public const string STR_IF_MODIFIED_SINCE = "If-Modified-Since";
        public const string STR_RANGE = "Range";
        public const string STR_TRANSFER_ENCODING = "Transfer-Encoding";

        public const string STR_CONNECTION_KEEP_ALIVE = "keep-alive";
        public const string STR_CONNECTION_CLOSE = "close";
        public const string STR_ACCEPT_ENCODING_GZIP = "gzip";
        public const string STR_ACCEPT_ENCODING_DEFLATE = "deflate";
        public const string STR_100_CONTINUE = "100-continue";
        public const string STR_CHUNKED = "chunked";

        //默认设置

        //默认接受的数据流类型
        public const string DEFAULT_ACCEPT_ENCODING = STR_ACCEPT_ENCODING_GZIP + ", " + STR_ACCEPT_ENCODING_DEFLATE;
        //默认接受的数据类型（文件类型）
        public const string DEFAULT_ACCEPT = "*/*";
        //默认超时时间（ms）
        public const int DEFAULT_TIMEOUT = int.MaxValue;
        public const int DEFAULT_READ_WRITE_TIMEOUT = 30000;
        //默认的代理url
        public const string DEFAULT_PROXY_URL = "";
        //默认是否忽略系统代理（在默认代理为空的时候起效）
        public const bool DEFAULT_IGNORE_SYSTEM_PROXY = false;
        //默认的user agent（截取自chrome）
        public const string DEFAULT_USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/68.0.3440.106 Safari/537.36";
        //默认发送的数据类型（MIME type）
        public const string DEFAULT_CONTENT_TYPE_PARAM = "application/x-www-form-urlencoded; charset=" + DEFAULT_ENCODING;
        public const string DEFAULT_CONTENT_TYPE_BINARY = "application/octet-stream";
        //默认接受的语言
        public const string DEFAULT_ACCEPT_LANGUAGE = "zh/cn,zh,en";
        //默认的文字编码类型
        public const string DEFAULT_ENCODING = "utf-8";

        //默认连接重试次数
        public const uint DEFAULT_RETRY_TIMES = 0;
        //默认重试的等待时间（ms）
        public const uint DEFAULT_RETRY_DELAY = 0;

        //默认保存cookie的文件名
        public const string DEFAULT_COOKIE_FILE_NAME = "cookie.dat";
        //默认使用的cookie的key（用于分辨和使用不同cookie container而创立的）
        public const string DEFAULT_COOKIE_GROUP = "default";
        //默认的TCP连接数
        public const int DEFAULT_TCP_CONNECTION = 1000;
        #endregion


        #region Cookie Segment
        //默认保存cookie的容器
        public static Dictionary<string, CookieContainer> DefaultCookieContainer;
        private static object __global_lock;
        /// <summary>
        /// 从文件中读取cookie
        /// </summary>
        /// <param name="file">文件路径，若此处留空则使用默认文件名</param>
        /// <exception cref="Exception">文件格式错误时引发的异常。</exception>
        public static void LoadCookie(string file = DEFAULT_COOKIE_FILE_NAME)
        {
            try
            {
                lock (__global_lock)
                {
                    var fi = new FileInfo(file);
                    if (fi.Exists && fi.Length > 0)
                    {
                        var stream = fi.OpenRead();
                        var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                        DefaultCookieContainer = (Dictionary<string, CookieContainer>)formatter.Deserialize(stream);
                        stream.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                Tracer.GlobalTracer.TraceError(ex);
                throw;
            }
        }

        /// <summary>
        /// 从文件中写入cookie
        /// </summary>
        /// <param name="file">文件路径，若此处留空则使用默认文件名</param>
        /// <exception cref="Exception">系统IO错误时引发的异常</exception>
        public static void SaveCookie(string file = DEFAULT_COOKIE_FILE_NAME)
        {
            try
            {
                lock (__global_lock)
                {
                    Stream stream;
                    try
                    {
                        stream = File.Create(file);
                    }
                    catch (DirectoryNotFoundException)
                    {
                        var parent = new FileInfo(file).Directory;
                        util.CreateDirectory(parent.FullName);
                        stream = File.Create(file);
                    }
                    var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    formatter.Serialize(stream, DefaultCookieContainer);
                    stream.Close();
                }
            }
            catch (Exception ex)
            {
                Tracer.GlobalTracer.TraceError(ex);
                throw;
            }
        }
        #endregion

        //static initialize setting
        static HttpSession()
        {
            __global_lock = new object();
            DefaultCookieContainer = new Dictionary<string, CookieContainer>();
            DefaultCookieContainer.Add(DEFAULT_COOKIE_GROUP, new CookieContainer());
            LoadCookie(); //读取cookie
            ServicePointManager.DefaultConnectionLimit = DEFAULT_TCP_CONNECTION;
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.MaxServicePointIdleTime = 2000;
            ServicePointManager.SetTcpKeepAlive(false, 0, 0);
        }

        //是否开启调试输出
        public static bool EnableTracing = false;
        //测试用的计算HTTP头部和本体字节长度
        private long _request_header_length;
        private long _request_protocol_length;
        private long _request_body_length;
        private long _response_header_length;
        private long _response_protocol_length;
        private long _response_body_length;
        //当前的失败统计次数
        private int _fail_times;
        //异步重试保存的变量
        private Parameters _header_param;
        private Parameters _url_param;
        private Range _range;
        private object _state;
        private string _url;
        private EventHandler<HttpFinishedResponseEventArgs> _callback;
        private string _post_content_type;
        private long _post_length;

        #region properties
        /// <summary>
        /// HTTP请求
        /// </summary>
        public HttpWebRequest HTTP_Request { get; private set; }
        /// <summary>
        /// HTTP响应
        /// </summary>
        public HttpWebResponse HTTP_Response { get; private set; }
        /// <summary>
        /// 请求的数据流（仅限POST）
        /// </summary>
        public Stream RequestStream { get; private set; }
        /// <summary>
        /// 响应的数据流
        /// </summary>
        public Stream ResponseStream { get; private set; }
        /// <summary>
        /// 是否使用cookie
        /// </summary>
        public bool UseCookie { get; set; }
        /// <summary>
        /// 网页代理
        /// </summary>
        public WebProxy Proxy { get; set; }
        /// <summary>
        /// 网页代理URL
        /// </summary>
        public string ProxyUrl { get { return Proxy == null ? null : Proxy.Address.ToString(); } set { Proxy = new WebProxy(value); } }
        /// <summary>
        /// 连接超时（ms）
        /// </summary>
        public int TimeOut { get; set; }
        /// <summary>
        /// 接受的数据压缩编码类型
        /// </summary>
        public string AcceptEncoding { get; set; }
        /// <summary>
        /// 接受的语言类型
        /// </summary>
        public string AcceptLanguage { get; set; }
        /// <summary>
        /// 接受的数据类型
        /// </summary>
        public string Accept { get; set; }
        /// <summary>
        /// User Agent
        /// </summary>
        public string UserAgent { get; set; }
        /// <summary>
        /// 发送的数据类型（仅限POST）
        /// </summary>
        public string ContentType { get; set; }
        /// <summary>
        /// 错误重试次数（仅限GET）
        /// </summary>
        public int RetryTimes { get; set; }
        /// <summary>
        /// 错误重试延时（ms）
        /// </summary>
        public int RetryDelay { get; set; }
        /// <summary>
        /// 读写超时事件（ms）
        /// </summary>
        public int ReadWriteTimeOut { get; set; }
        /// <summary>
        /// 使用哪一个cookie
        /// </summary>
        public string CookieKey { get; set; }
        /// <summary>
        /// 当代理url为空时是否跳过系统IE代理
        /// </summary>
        public bool IgnoreSystemProxy { get; set; }
        public long RequestProtocolLength { get { return _request_protocol_length; } }
        public long RequestHeaderLength { get { return _request_header_length; } }
        public long RequestBodyLength { get { return _request_body_length; } }
        public long ResponseProtocolLength { get { return _response_protocol_length; } }
        public long ResponseHeaderLength { get { return _response_header_length; } }
        public long ResponseBodyLength { get { return _response_body_length; } }
        #endregion

        /// <summary>
        /// 重置当前会话的所有相关变量
        /// </summary>
        private void _reset_session()
        {
            // reset all internal variables
            _request_body_length = 0;
            _request_header_length = 0;
            _request_protocol_length = 0;
            _response_body_length = 0;
            _response_header_length = 0;
            _response_protocol_length = 0;
            _range = null;
            _url = null;
            _callback = null;
            _state = null;
            _post_content_type = null;
            _post_length = 0;
            _fail_times = 0;
            _header_param = null;
            _url_param = null;
            // closing request resources
            if (RequestStream != null)
            {
                try
                {
                    RequestStream.Close();
                    RequestStream.Dispose();
                }
                catch { }
                RequestStream = null;
            }
            if (ResponseStream != null)
            {
                try
                {
                    ResponseStream.Close();
                    ResponseStream.Dispose();
                }
                catch { }
                ResponseStream = null;
            }
            if (HTTP_Response != null)
            {
                try
                {
                    HTTP_Response.Close();
                }
                catch { }
                HTTP_Response = null;
            }
            if (HTTP_Request != null)
            {
                try
                {
                    HTTP_Request.Abort();
                }
                catch { }
                HTTP_Request = null;
            }
        }

        public IAsyncResult HttpGetAsync(string url, EventHandler<HttpFinishedResponseEventArgs> callback, object callback_param = null, Parameters header = null, Parameters query = null, Range range = null)
        {
            // reset session
            _reset_session();

            // stores the exception during executing the retry loop
            List<Exception> raised_exceptions = new List<Exception>();
            while (RetryTimes == -1 || _fail_times < RetryTimes)
            {
                // retry loop
                try
                {
                    var final_url = url;
                    // appending query parameters to url
                    if (query != null)
                    {
                        var query_params = query.BuildQueryString();
                        if (query_params.Length > 0)
                            final_url += "?" + query_params;
                    }

                    _header_param = header;
                }
                catch (Exception ex)
                {

                }
            }
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 在获得HTTP响应进行回调时的参数
    /// </summary>
    public class HttpFinishedResponseEventArgs : EventArgs
    {
        public object State { get; protected set; }
        public HttpSession Session { get; protected set; }
        public HttpFinishedResponseEventArgs(HttpSession session, object callback_state = null) : base()
        {
            Session = session;
            State = callback_state;
        }
    }

    internal sealed class HttpWebRequestHelper
    {
        public static void MergeParametersToWebRequestHeader(Parameters header, HttpWebRequest request)
        {
            if (header == null)
                return;
            var merge_attributes = new Dictionary<string, _prototype_reflect_assign>();
            merge_attributes.Add(HttpSession.STR_ACCEPT, _str_reflect_assign);
            merge_attributes.Add(HttpSession.STR_CONNECTION, _connection_assign);
            merge_attributes.Add(HttpSession.STR_CONTENT_LENGTH, _parsable_reflect_assign<long>);
            merge_attributes.Add(HttpSession.STR_CONTENT_TYPE, _str_reflect_assign);
            merge_attributes.Add(HttpSession.STR_EXPECT, _expect_assign);
            merge_attributes.Add(HttpSession.STR_DATE, _parsable_reflect_assign<DateTime>);
            merge_attributes.Add(HttpSession.STR_HOST, _str_reflect_assign);
            merge_attributes.Add(HttpSession.STR_IF_MODIFIED_SINCE, _parsable_reflect_assign<DateTime>);
            merge_attributes.Add(HttpSession.STR_RANGE, _range_assign);
            merge_attributes.Add(HttpSession.STR_REFERER, _str_reflect_assign);
            merge_attributes.Add(HttpSession.STR_TRANSFER_ENCODING, _transfer_encoding_assign);
            merge_attributes.Add(HttpSession.STR_USER_AGENT, _str_reflect_assign);
            foreach (var param in header)
            {
                if (merge_attributes.ContainsKey(param.Key))
                    merge_attributes[param.Key].Invoke(request, param.Key, param.Value);
                else
                    _default_assign(request, param.Key, param.Value);
            }
        }
        private delegate void _prototype_reflect_assign(HttpWebRequest request, string key, string value);
        private static void _default_assign(HttpWebRequest request, string key, string value)
        {
            request.Headers.Add(key, value);
        }
        private static void _str_reflect_assign(HttpWebRequest request, string key, string value)
        {
            request.GetType().GetProperty(key.Replace("-", "")).SetValue(request, value, null);
        }
        private static void _parsable_reflect_assign<T>(HttpWebRequest request, string key, string value)
        {
            T result = (T)Convert.ChangeType(value, typeof(T));
            request.GetType().GetProperty(key.Replace("-", "")).SetValue(request, result, null);
        }
        private static void _connection_assign(HttpWebRequest request, string key, string value)
        {
            if (value.ToLower() == HttpSession.STR_CONNECTION_KEEP_ALIVE)
                request.KeepAlive = true;
            else if (value.ToLower() == HttpSession.STR_CONNECTION_CLOSE)
                request.KeepAlive = false;
            else
                throw new ArgumentException("Invalid Connection status");
        }
        private static void _expect_assign(HttpWebRequest request, string key, string value)
        {
            Tracer.GlobalTracer.TraceWarning("Changing header field 'Expect' will affect all connections in the same ServicePoint.");
            if (value.ToLower() == HttpSession.STR_100_CONTINUE)
                request.ServicePoint.Expect100Continue = true;
            else if (string.IsNullOrEmpty(value))
                request.ServicePoint.Expect100Continue = false;
            else
                throw new ArgumentException("Invalid Expect value");
        }
        private static void _range_assign(HttpWebRequest request, string key, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            if (value.ToLower().StartsWith("bytes="))
                value = value.Substring(6);
            var range = Range.Parse(value);
            if (range.From == null && range.To == null) return;
            if (range.To == null)
                request.AddRange(range.From.Value);
            else if (range.From == null)
                request.AddRange(-range.To.Value);
            else
                request.AddRange(range.From.Value, range.To.Value);
        }
        private static void _transfer_encoding_assign(HttpWebRequest request, string key, string value)
        {
            if (value.ToLower() == HttpSession.STR_CHUNKED)
                request.SendChunked = true;
            else if (string.IsNullOrEmpty(value))
                request.SendChunked = false;
            else
                throw new ArgumentException("Invalid Transfer-Encoding value");
        }
    }
}
