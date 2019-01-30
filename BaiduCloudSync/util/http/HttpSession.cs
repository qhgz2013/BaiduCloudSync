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
    // todo: implement this class using another third-party .net modules, to avoid some strange bug while using System.Net.*
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
        //默认是否使用cookie进行HTTP请求
        public const bool DEFAULT_USE_COOKIE = true;
        #endregion


        #region Cookie Segment
        //默认保存cookie的容器
        private static Dictionary<string, CookieContainer> DefaultCookieContainer;
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
                        Util.CreateDirectory(parent.FullName);
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

        // v1.1 removed global access for DefaultCookieContainer (for multi-thread access propose)

        /// <summary>
        /// 根据group name获取对应的cookie容器
        /// </summary>
        /// <param name="group_name">cookie的分组名，用于区分不同的cookie</param>
        /// <returns>当group_name存在时返回对应的容器，否则返回null</returns>
        /// <exception cref="ArgumentNullException">group_name为空时引发的异常</exception>
        public static CookieContainer GetCookieContainer(string group_name)
        {
            if (string.IsNullOrEmpty(group_name))
                throw new ArgumentNullException("group_name");
            lock (__global_lock)
            {
                if (DefaultCookieContainer.ContainsKey(group_name))
                    return DefaultCookieContainer[group_name];
                return null;
            }
        }
        /// <summary>
        /// 设置或删除指定group name下的cookie容器
        /// </summary>
        /// <param name="group_name">cookie的分组名，用于区分不同的cookie</param>
        /// <param name="container">要设置的新的cookie容器，若该参数置为null，则删除该分组的容器</param>
        /// <exception cref="ArgumentNullException">group_name为空时引发的异常</exception>
        /// <exception cref="KeyNotFoundException">当尝试删除一个不存在的cookie group时引发的异常</exception>
        public static void SetCookieContainer(string group_name, CookieContainer container)
        {
            if (string.IsNullOrEmpty(group_name))
                throw new ArgumentNullException("group_name");
            lock (__global_lock)
            {
                if (container == null)
                {
                    if (DefaultCookieContainer.ContainsKey(group_name))
                        DefaultCookieContainer.Remove(group_name);
                    else throw new KeyNotFoundException(group_name + " is not registered yet");
                }
                else
                {
                    if (DefaultCookieContainer.ContainsKey(group_name))
                        DefaultCookieContainer[group_name] = container;
                    else
                        DefaultCookieContainer.Add(group_name, container);
                }
            }
        }
        /// <summary>
        /// 现存的所有cookie分组名称
        /// </summary>
        public static string[] ExistCookieGroupName
        {
            get
            {
                lock (__global_lock)
                    return DefaultCookieContainer.Keys.ToArray();
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
        public static bool EnableInfoTracing = true;
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

        /// <summary>
        /// 获取异步请求时捕获到的内部异常
        /// </summary>
        public Exception AsyncException { get { return _exception_thrown_on_max_retry_exceeded.Count > 0 ? _exception_thrown_on_max_retry_exceeded : null; } }
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

            // reset all internal variables
            _url = null;
            _method = null;
            _callback = null;
            _state = null;
            //_fail_times = 0;
            _header_param = null;
            _exception_thrown_on_max_retry_exceeded = new StackedHttpException("Max retry exceeded");
            if (_post_origin_stream != null)
            {
                //try
                //{
                //    _post_origin_stream.Close();
                //    _post_origin_stream.Dispose();
                //}
                //catch { }

                // not managing origin stream here
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
            // add the default content-type to POST requests
            if (_post_origin_stream != null && ContentType != null)
                _append_default_header_internal(ret, STR_CONTENT_TYPE, ContentType);
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

        private Parameters _merge_range(Parameters origin, Range range)
        {
            if (range == null)
                return origin;
            var ret = new Parameters();
            ret.Add(STR_RANGE, range);
            if (origin != null)
                foreach (var item in origin)
                {
                    ret.Add(item.Key, item.Value);
                }
            return ret;
        }

        public HttpSession(bool use_cookie = DEFAULT_USE_COOKIE, int retry_times = DEFAULT_RETRY_TIMES, int retry_delay = DEFAULT_RETRY_DELAY, int timeout = DEFAULT_TIMEOUT, int read_write_timeout = DEFAULT_READ_WRITE_TIMEOUT,
            string proxy_url = DEFAULT_PROXY_URL, bool ignore_system_proxy = DEFAULT_IGNORE_SYSTEM_PROXY, string cookie_group = DEFAULT_COOKIE_GROUP, string user_agent = DEFAULT_USER_AGENT, string content_type = null)
        {
            // initialize using default values
            UseCookie = use_cookie;
            RetryTimes = retry_times;
            RetryDelay = retry_delay;
            TimeOut = timeout;
            ReadWriteTimeOut = read_write_timeout;
            if (!string.IsNullOrEmpty(proxy_url))
                ProxyUrl = proxy_url;
            IgnoreSystemProxy = ignore_system_proxy;
            CookieGroup = cookie_group;

            Accept = DEFAULT_ACCEPT;
            AcceptEncoding = DEFAULT_ACCEPT_ENCODING;
            AcceptLanguage = DEFAULT_ACCEPT_LANGUAGE;
            UserAgent = user_agent;
            ContentType = content_type;
        }

        /// <summary>
        /// 关闭HTTP会话，释放占用资源
        /// </summary>
        public void Close()
        {
            _reset_session();
        }

        /// <summary>
        /// 异步HTTP GET请求
        /// </summary>
        /// <param name="url">请求url</param>
        /// <param name="callback">回调函数</param>
        /// <param name="callback_param">回调函数的附加参数</param>
        /// <param name="header">HTTP请求头</param>
        /// <param name="query">URL查询参数</param>
        /// <param name="range">HTTP响应的内容范围（并非所有服务器都提供该功能）</param>
        /// <returns>HTTP请求的异步结果</returns>
        /// <exception cref="StackedHttpException">在未开始异步请求时引发的异常</exception>
        public IAsyncResult HttpGetAsync(string url, EventHandler<HttpFinishedResponseEventArgs> callback, object callback_param = null, Parameters header = null, Parameters query = null, Range range = null)
        {
            _fail_times = 0;
            return _http_async(DEFAULT_GET_METHOD, url, callback, callback_param, _merge_range(header, range), query, null);
        }

        /// <summary>
        /// HTTP GET请求
        /// </summary>
        /// <param name="url">请求url</param>
        /// <param name="header">HTTP请求头</param>
        /// <param name="query">URL查询参数</param>
        /// <param name="range">HTTP响应的内容范围（并非所有服务器都提供该功能）</param>
        /// <exception cref="StackedHttpException">在请求发生错误时引发的异常</exception>
        public void HttpGet(string url, Parameters header = null, Parameters query = null, Range range = null)
        {
            var wait_handle = new ManualResetEventSlim();
            var iar = HttpGetAsync(url, (s, e) =>
            {
                wait_handle.Set();
            }, wait_handle, header, query, range);
            iar.AsyncWaitHandle.WaitOne();
            wait_handle.Wait();
            // exception check
            if (AsyncException != null)
                throw AsyncException;
        }


        public void HttpPost(string url, Parameters body, Parameters header = null, Parameters query = null, Range range = null)
        {
            HttpPost(url, Encoding.GetEncoding(DEFAULT_ENCODING).GetBytes(body.BuildQueryString()), header, query, range);
        }

        public void HttpPost(string url, byte[] body, Parameters header = null, Parameters query = null, Range range = null)
        {
            HttpPost(url, new MemoryStream(body), header, query, range);
        }
        public void HttpPost(string url, Stream post_stream, Parameters header = null, Parameters query = null, Range range = null)
        {
            var wait_handle = new ManualResetEventSlim();
            var iar = HttpPostAsync(url, post_stream, (s, e) =>
            {
                wait_handle.Set();
            }, wait_handle, header, query, range);
            iar.AsyncWaitHandle.WaitOne();
            wait_handle.Wait();
            // exception check
            if (AsyncException != null)
                throw AsyncException;
        }
        public IAsyncResult HttpPostAsync(string url, Parameters body, EventHandler<HttpFinishedResponseEventArgs> callback, object callback_param = null, Parameters header = null, Parameters query = null, Range range = null)
        {
            return HttpPostAsync(url, Encoding.GetEncoding(DEFAULT_ENCODING).GetBytes(body.BuildQueryString()), callback, callback_param, header, query, range);
        }
        public IAsyncResult HttpPostAsync(string url, byte[] body, EventHandler<HttpFinishedResponseEventArgs> callback, object callback_param = null, Parameters header = null, Parameters query = null, Range range = null)
        {
            return HttpPostAsync(url, new MemoryStream(body), callback, callback_param, header, query, range);
        }
        public IAsyncResult HttpPostAsync(string url, Stream post_stream, EventHandler<HttpFinishedResponseEventArgs> callback, object callback_param = null, Parameters header = null, Parameters query = null, Range range = null)
        {
            _fail_times = 0;
            return _http_async(DEFAULT_POST_METHOD, url, callback, callback_param, _merge_range(header, range), query, post_stream);
        }
        /// <summary>
        /// 从ResponseStream中读取所有字节，并使用指定编码返回其字符串
        /// </summary>
        /// <param name="encoding">编码类型，如为null则使用DEFAULT_ENCODING编码</param>
        /// <returns>若ResponseStream为null，则返回null，否则返回对应的string</returns>
        /// <exception cref="IOException">在读取ResponseStream失败时引发的异常</exception>
        /// <exception cref="ArgumentException">在获取Encoding失败时引发的异常</exception>
        public string ReadResponseString(Encoding encoding = null)
        {
            try
            {
                byte[] binary_array = ReadResponseBinary();
                if (binary_array == null) return null;
                if (encoding == null)
                    encoding = Encoding.GetEncoding(DEFAULT_ENCODING);
                return encoding.GetString(binary_array);
            }
            catch (ArgumentException)
            {
                throw new ArgumentException("Could not find default encoding (" + DEFAULT_ENCODING + ") in the system runtime environment");
            }


        }

        /// <summary>
        /// 从ResponseStream中读取所有字节，并返回其字节数组（在content过大时会消耗大量内存）
        /// </summary>
        /// <returns>若ResponseStream为null，则返回null，否则返回对应大小的byte[]数组</returns>
        /// <exception cref="IOException">在读取ResponseStream失败时引发的异常</exception>
        public byte[] ReadResponseBinary()
        {
            if (ResponseStream == null) return null;
            if (HTTP_Response.ContentLength == 0) return new byte[0];
            MemoryStream ms = null;
            bool is_ms_resizable = HTTP_Response.ContentLength < 0;
            if (is_ms_resizable)
                // adaptive size
                ms = new MemoryStream();
            else
                // fixed size
                ms = new MemoryStream(new byte[HTTP_Response.ContentLength]);

            try
            {
                long total_readed_bytes = 0;
                int readed_bytes = 0;
                byte[] buffer = new byte[4096];
                bool warn_on_buffer_overflow = false;
                // warn the buffer overflow if buffer size is larger than 100MB
                const long MIN_BYTES_TO_WARN_ON_BUFFER_OVERFLOW = 104857600;
                do
                {
                    readed_bytes = ResponseStream.Read(buffer, 0, 4096);
                    if (!is_ms_resizable && !warn_on_buffer_overflow && total_readed_bytes + readed_bytes > ms.Length)
                    {
                        warn_on_buffer_overflow = true;
                        // bytes overflow
                        if (total_readed_bytes >= MIN_BYTES_TO_WARN_ON_BUFFER_OVERFLOW)
                            Tracer.GlobalTracer.TraceWarning("Buffer overflow detected while reading response stream, converting non-resizable memory stream to resizable memory stream (this operation will consume a lot of memory and time)");
                        var new_adaptive_ms = new MemoryStream((int)Math.Min(total_readed_bytes + readed_bytes, int.MaxValue));
                        // copying stream
                        byte[] memory_buffer = new byte[1048576]; // using 1M memory block
                        long original_ms_position = ms.Position;
                        ms.Position = 0;
                        int temp_read = 0;
                        do
                        {
                            temp_read = ms.Read(memory_buffer, 0, 1048576);
                            new_adaptive_ms.Write(memory_buffer, 0, temp_read);
                        } while (temp_read > 0 && ms.Position < original_ms_position);
                        // closing origin memory stream and replace new memory stream
                        ms.Close();
                        ms.Dispose();
                        ms = new_adaptive_ms;
                        ms.Position = original_ms_position;
                    }
                    total_readed_bytes += readed_bytes;
                    ms.Write(buffer, 0, readed_bytes);
                } while (readed_bytes > 0);

            }
            catch (IOException) { throw; }
            catch (Exception ex)
            {
                throw new IOException("Read response stream failed", ex);
            }
            return ms.ToArray();
        }


        #region privte callbacks
        private IAsyncResult _http_async(string method, string url, EventHandler<HttpFinishedResponseEventArgs> callback, object callback_param, Parameters header, Parameters query, Stream request_stream)
        {
            _reset_session();
            // initializing variables
            _method = method;
            _url = url;
            _header_param = header;
            _state = callback_param;
            _callback = callback;
            _post_origin_stream = request_stream;
            if (_post_origin_stream != null)
            {
                _post_origin_stream = HttpWebRequestHelper.ConvertToSeekableStream(_post_origin_stream);
                _post_origin_stream.Position = 0;
            }

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
                            var session_container = new CookieContainer();
                            session_container.Add(DefaultCookieContainer[CookieGroup].GetCookies(new Uri(_url)));
                            HTTP_Request.CookieContainer = session_container;
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

        /*
         * Legend: ---> Async call   ===> Sync call
         * 
         *     with request stream                                                                                                  with response stream
         * _http_async ---> _http_async_send_request_body_callback ===> _http_async_get_response_callback ---> _http_async_response_callback ===> _adapt_response_stream
         *      |                                                               A
         *      +===============================================================+
         *                  without request stream
         * 
         * Exception raised in _http_async: throw
         * Exception raised in others: retrying _http_async if retry count is not exceeded, otherwise, calling _handle_mid_stage_exception and calling callback function with failed status
         */


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
                stream_out.Flush();
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
            // POST http request, PRE http response

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
                    HTTP_Response = (HttpWebResponse)ex2.Response;
                }

                // handling response cookie
                if (UseCookie)
                {
                    var headers = HTTP_Response.Headers;
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

                // adapting response stream
                ResponseStream = _adapt_response_stream(HTTP_Response);
                _invoke_callback();
            }
            catch (Exception ex)
            {
                _handle_mid_stage_exception(ex);
            }
        }
        // check the exception is raised by cancelling the request
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

        #endregion
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
