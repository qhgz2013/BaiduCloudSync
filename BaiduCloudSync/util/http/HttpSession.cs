using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
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
        public const int DEFAULT_RETRY_TIMES = 5;
        //默认重试的等待时间（ms）
        public const int DEFAULT_RETRY_DELAY = 0;

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
        public static bool EnableTracing = true;
        //是否开启异常输出
        public static bool EnableErrorTracing = true;
        //是否开启调试输出
        public static bool EnableInfoTracing = false;
        //测试用的计算HTTP头部和本体字节长度
        private long _request_header_length;
        private long _request_protocol_length;
        private long _request_body_length;
        private long _response_header_length;
        private long _response_protocol_length;
        private long _response_body_length;
        //当前的失败统计次数
        private int _fail_times;
        private StackedHttpException _exception_thrown_on_max_retry_exceeded;
        private static int _max_stacked_exception_count = 1000;
        //异步重试保存的变量
        private string _method;
        private Parameters _header_param;
        private object _state;
        private string _url;
        private EventHandler<HttpFinishedResponseEventArgs> _callback;
        private Stream _post_origin_stream;

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
        //public Stream RequestStream { get; private set; }
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
        public string CookieGroup { get; set; }
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
            // validating property
            if (RetryDelay < 0)
                RetryDelay = 0;
            if (RetryTimes < -1) RetryTimes = -1;
            if (TimeOut <= 0)
                TimeOut = int.MaxValue;
            if (ReadWriteTimeOut <= 0)
                ReadWriteTimeOut = int.MaxValue;
            // reset property 
            if (string.IsNullOrEmpty(CookieGroup))
                CookieGroup = DEFAULT_COOKIE_GROUP;
            if (string.IsNullOrEmpty(Accept))
                Accept = DEFAULT_ACCEPT;
            if (string.IsNullOrEmpty(AcceptEncoding))
                AcceptEncoding = DEFAULT_ACCEPT_ENCODING;
            if (string.IsNullOrEmpty(AcceptLanguage))
                AcceptLanguage = DEFAULT_ACCEPT_LANGUAGE;
            if (string.IsNullOrEmpty(UserAgent))
                UserAgent = DEFAULT_USER_AGENT;
            ContentType = null;

            // reset all internal variables
            _request_body_length = 0;
            _request_header_length = 0;
            _request_protocol_length = 0;
            _response_body_length = 0;
            _response_header_length = 0;
            _response_protocol_length = 0;
            _url = null;
            _method = null;
            _callback = null;
            _state = null;
            _fail_times = 0;
            _header_param = null;
            _exception_thrown_on_max_retry_exceeded = new StackedHttpException("Max retry exceeded");
            if (_post_origin_stream != null)
            {
                try
                {
                    _post_origin_stream.Close();
                    _post_origin_stream.Dispose();
                }
                catch { }
                _post_origin_stream = null;
            }
            // closing request resources
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

        /// <summary>
        /// 在自定义HTTP头前添加默认的HTTP头
        /// </summary>
        /// <param name="custom_headers"></param>
        /// <returns></returns>
        private Parameters _append_default_header(Parameters custom_headers)
        {
            var ret = new Parameters();
            _append_default_header_internal(ret, STR_ACCEPT, Accept);
            _append_default_header_internal(ret, STR_ACCEPT_ENCODING, AcceptEncoding);
            _append_default_header_internal(ret, STR_ACCEPT_LANGUAGE, AcceptLanguage);
            _append_default_header_internal(ret, STR_USER_AGENT, UserAgent);
            _merge_parameters(ret, custom_headers);
            return ret;
        }
        private void _merge_parameters(Parameters to, Parameters from)
        {
            if (to == null || from == null)
                return;
            foreach (var item in from)
                to.Add(item.Key, item.Value);
        }
        private void _append_default_header_internal<T>(Parameters header, string key, T value)
        {
            if (header != null && !string.IsNullOrEmpty(value.ToString()))
                header.Add(key, value);
        }

        private void _update_request_length()
        {
            if (HTTP_Request != null)
            {
                // protocol
                // <Method> SP <URI> SP "HTTP/" <ProtocolVersion> CR LF
                _request_protocol_length = Encoding.UTF8.GetByteCount(HTTP_Request.Method) +
                    Encoding.UTF8.GetByteCount(HTTP_Request.RequestUri.PathAndQuery) +
                    Encoding.UTF8.GetByteCount(HTTP_Request.ProtocolVersion.ToString()) +
                    9;
                // headers
                _request_header_length = _get_header_length(HTTP_Request);
                // specifying HOST and CONNECTION header (not via headers container)
                _request_header_length += Encoding.UTF8.GetByteCount((HTTP_Request.KeepAlive ? STR_CONNECTION_KEEP_ALIVE: STR_CONNECTION_CLOSE) + HTTP_Request.Host) + 22;
                // body
                _request_body_length = _post_origin_stream == null ? 0 : _post_origin_stream.Length;
            }
        }
        private long _get_header_length(object request_or_response)
        {
            // headers
            // CR LF splitting header and body
            var length = 2;
            var headers = _reflect_headers(request_or_response);
            if (headers == null) return 0;
            foreach (var key in headers.AllKeys)
            {
                // <HeaderName> ":" SP <HeaderValue> CR LF
                foreach (var value in headers.GetValues(key))
                {
                    length += Encoding.UTF8.GetByteCount(key) +
                        Encoding.UTF8.GetByteCount(value) + 4;
                }
            }
            return length;
        }
        private void _update_response_length()
        {
            if (HTTP_Response != null)
            {
                // protocol
                // "HTTP/" <ProtocolVersion> SP <StatusCode> SP <StatusDescription> CR LF
                _response_protocol_length = Encoding.UTF8.GetByteCount(HTTP_Response.ProtocolVersion.ToString()) +
                    Encoding.UTF8.GetByteCount(((int)HTTP_Response.StatusCode).ToString()) +
                    Encoding.UTF8.GetByteCount(HTTP_Response.StatusDescription) +
                    9;
                // headers
                _response_header_length = _get_header_length(HTTP_Response);
                // body
                _response_body_length = HTTP_Response.ContentLength;
            }
        }
        private NameValueCollection _reflect_headers(object request_or_response)
        {
            Type base_type = null;
            if (typeof(WebRequest).IsInstanceOfType(request_or_response))
                base_type = typeof(WebRequest);
            else if (typeof(WebResponse).IsInstanceOfType(request_or_response))
                base_type = typeof(WebResponse);
            else
                return null;

            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            var header_property = base_type.GetProperty("Headers").GetValue(request_or_response, null);
            var inner_container = typeof(WebHeaderCollection).GetField("m_InnerCollection", flags).GetValue(header_property) as NameValueCollection;
            return inner_container;
        }
        public HttpSession()
        {
            // initialize using default values
            RetryTimes = DEFAULT_RETRY_TIMES;
            RetryDelay = DEFAULT_RETRY_DELAY;
            TimeOut = DEFAULT_TIMEOUT;
            ReadWriteTimeOut = DEFAULT_READ_WRITE_TIMEOUT;
            if (!string.IsNullOrEmpty(DEFAULT_PROXY_URL))
                ProxyUrl = DEFAULT_PROXY_URL;
            IgnoreSystemProxy = DEFAULT_IGNORE_SYSTEM_PROXY;
            CookieGroup = DEFAULT_COOKIE_GROUP;

            Accept = DEFAULT_ACCEPT;
            AcceptEncoding = DEFAULT_ACCEPT_ENCODING;
            AcceptLanguage = DEFAULT_ACCEPT_LANGUAGE;
            UserAgent = DEFAULT_USER_AGENT;
            ContentType = null;
        }

        public void Close()
        {
            _reset_session();
        }
        public IAsyncResult HttpGetAsync(string url, EventHandler<HttpFinishedResponseEventArgs> callback, object callback_param = null, Parameters header = null, Parameters query = null, Range range = null)
        {
            return _http_async(DEFAULT_GET_METHOD, url, callback, callback_param, header, query, null);
        }

        public void HttpGet(string url, Parameters header = null, Parameters query = null, Range range = null)
        {
            var wait_handle = new ManualResetEventSlim();
            var iar = HttpGetAsync(url, (s, e) =>
            {
                wait_handle.Set();
            }, wait_handle, header, query, range);
            iar.AsyncWaitHandle.WaitOne();
            wait_handle.Wait();
        }


        public void HttpPost(string url, Parameters body, Parameters header = null, Parameters query = null, Range range = null)
        {

        }

        public string ReadResponseString(Encoding encoding = null)
        {
            throw new NotImplementedException();
        }

        public byte[] ReadResponseBinary()
        {
            throw new NotImplementedException();
        }
        private IAsyncResult _http_async(string method, string url, EventHandler<HttpFinishedResponseEventArgs> callback, object callback_param, Parameters header, Parameters query, Stream request_stream)
        {
            // reset session
            _reset_session();

            // initializing variables
            _method = method;
            _url = url;
            _header_param = header;
            _state = callback_param;
            _callback = callback;
            _post_origin_stream = request_stream;
            if (_post_origin_stream != null)
                _post_origin_stream = HttpWebRequestHelper.ConvertToSeekableStream(_post_origin_stream);

            // appending query parameters to url
            if (query != null)
            {
                var query_params = query.BuildQueryString();
                if (query_params.Length > 0)
                    _url += "?" + query_params;
            }

            while (RetryTimes == -1 || _fail_times <= RetryTimes)
            {
                // retry loop
                #region retry loop
                try
                {
                    if (EnableTracing && EnableInfoTracing)
                        Tracer.GlobalTracer.TraceInfo(method + " " + _url);

                    // creating http request
                    HTTP_Request = (HttpWebRequest)WebRequest.Create(_url);


                    // proxy negotiation
                    if (IgnoreSystemProxy) HTTP_Request.Proxy = null;
                    else if (Proxy != null) HTTP_Request.Proxy = Proxy;

                    // appending default header which was set by properties (Accept, Accept-Encoding, Accept-Language, User-Agent)
                    var header_param = _append_default_header(_header_param);

                    // appending header param to http request
                    HttpWebRequestHelper.MergeParametersToWebRequestHeader(header_param, HTTP_Request);

                    // cookie
                    if (UseCookie && !header_param.Contains(STR_COOKIE))
                    {
                        lock (__global_lock)
                        {
                            // create new cookie container with specified CookieKey if necessary
                            if (!DefaultCookieContainer.ContainsKey(CookieGroup))
                                DefaultCookieContainer.Add(CookieGroup, new CookieContainer());
                            // set the specified CookieContainer to this session
                            HTTP_Request.CookieContainer = DefaultCookieContainer[CookieGroup];
                        }
                    }

                    // request method and timeout value
                    HTTP_Request.Method = method;
                    HTTP_Request.ReadWriteTimeout = ReadWriteTimeOut;
                    HTTP_Request.Timeout = TimeOut;

                    // writing post stream
                    if (_post_origin_stream != null)
                    {
                        _post_origin_stream.Seek(0, SeekOrigin.Begin);
                        return HTTP_Request.BeginGetRequestStream(_http_async_send_request_body_callback, null);
                    }
                    else
                    {
                        // otherwise, directly get response
                        return _http_async_get_response_callback();
                    }
                }
                catch (Exception ex)
                {
                    // appending exceptions
                    _fail_times++;
                    if (_exception_thrown_on_max_retry_exceeded.Count < _max_stacked_exception_count)
                        _exception_thrown_on_max_retry_exceeded.Add(ex);
                    if (EnableTracing && EnableErrorTracing)
                        Tracer.GlobalTracer.TraceError(ex);
                }
                #endregion
            }

            // loop exited, the only scene is that _fail_times >= RetryTimes
            // raise exception here
            throw _exception_thrown_on_max_retry_exceeded;
        }

        // BeginGetRequestStream后的回调函数
        private void _http_async_send_request_body_callback(IAsyncResult iar)
        {
            try
            {
                byte[] buffer = new byte[4096];
                // async end
                var stream_out = HTTP_Request.EndGetRequestStream(iar);

                // put the stream
                while (_post_origin_stream.Position < _post_origin_stream.Length)
                {
                    int n_read = _post_origin_stream.Read(buffer, 0, 4096);
                    if (n_read > 0)
                        stream_out.Write(buffer, 0, n_read);
                }
                stream_out.Close();

                // ask for response
                _http_async_get_response_callback();
            }
            catch (Exception ex)
            {
                _handle_mid_stage_exception(ex);
            }
        }
        private IAsyncResult _http_async_get_response_callback()
        {
            // updating length properties
            _update_request_length();

            // async requesting
            return HTTP_Request.BeginGetResponse(_http_async_response_callback, null);
        }

        private void _http_async_response_callback(IAsyncResult iar)
        {
            try
            {
                if (HTTP_Request == null) return;

                // get response, ignoring protocol error
                try
                {
                    HTTP_Response = (HttpWebResponse)HTTP_Request.EndGetResponse(iar);
                }
                catch (WebException ex2)
                {
                    if (ex2.Status != WebExceptionStatus.ProtocolError)
                        throw;
                }

                // handling response cookie
                if (UseCookie)
                {
                    var headers = _reflect_headers(HTTP_Response);
                    foreach (var header in headers.AllKeys)
                        if (header.ToLower() == STR_SETCOOKIE.ToLower())
                        {
                            var cookies = headers.GetValues(header);
                            var cc = new CookieCollection();
                            foreach (var str_cookie in cookies)
                            {
                                var cookie = CookieParser.ParseCookie(STR_SETCOOKIE + ": " + str_cookie);
                                if (string.IsNullOrEmpty(cookie.Domain))
                                    cookie.Domain = new Uri(_url).Host;
                                cc.Add(cookie);
                            }
                            lock (__global_lock)
                            {
                                if (!DefaultCookieContainer.ContainsKey(CookieGroup))
                                    DefaultCookieContainer.Add(CookieGroup, new CookieContainer());
                                DefaultCookieContainer[CookieGroup].Add(cc);
                            }
                            break;
                        }
                }

                // updating length
                _update_response_length();

                // adapting response stream
                ResponseStream = _adapt_response_stream(HTTP_Response);
                _invoke_callback();
            }
            catch (Exception ex)
            {
                _handle_mid_stage_exception(ex);
            }
        }

        private bool _check_request_cancelled(Exception ex)
        {
            return (typeof(WebRequest).IsInstanceOfType(ex) && ((WebException)ex).Status == WebExceptionStatus.RequestCanceled);
        }

        private Stream _adapt_response_stream(HttpWebResponse response)
        {
            switch (response.ContentEncoding)
            {
                case STR_ACCEPT_ENCODING_GZIP:
                    return new System.IO.Compression.GZipStream(response.GetResponseStream(), System.IO.Compression.CompressionMode.Decompress);
                case STR_ACCEPT_ENCODING_DEFLATE:
                    return new System.IO.Compression.DeflateStream(response.GetResponseStream(), System.IO.Compression.CompressionMode.Decompress);
                default:
                    return HTTP_Response.GetResponseStream();
            }
        }

        private void _handle_mid_stage_exception(Exception ex)
        {
            // oops, something went wrong when putting stream to internet
            _fail_times++;

            // log the exception
            if (_exception_thrown_on_max_retry_exceeded.Count < _max_stacked_exception_count)
                _exception_thrown_on_max_retry_exceeded.Add(ex);
            if (EnableTracing && EnableErrorTracing)
                Tracer.GlobalTracer.TraceError(ex);

            if (!_check_request_cancelled(ex) && (RetryTimes == -1 || _fail_times < RetryTimes))
            {
                // if request is not cancelled yet, retry request
                try
                {
                    _http_async(_method, _url, _callback, _state, _header_param, null, _post_origin_stream);
                }
                catch (Exception)
                {
                    // retry failed, invoking failure callback
                    _invoke_callback();
                }
            }
            else
            {
                // callback when retry failed or cancelled
                _invoke_callback();
            }
        }

        // 调用回调函数
        private void _invoke_callback()
        {
            try
            {
                _callback?.Invoke(this, new HttpFinishedResponseEventArgs(this, _state));
            }
            catch (Exception ex)
            {
                // unexpected exception while invoking callback function
                if (EnableTracing && EnableErrorTracing)
                {
                    Tracer.GlobalTracer.TraceError("Unexpected exception caught while invoking HttpSession callback");
                    Tracer.GlobalTracer.TraceError(ex);
                }
            }
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

}
