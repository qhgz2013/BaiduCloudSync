// net-util.cs
//
// 整合命令发送同步/异步HTTP请求
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Collections;
using System.Threading;

namespace GlobalUtil
{
    namespace NetUtils
    {
        public partial class NetStream
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
            public const string DEFAULT_USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/53.0.2785.143 Safari/537.36";
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
            public static Dictionary<string, CookieContainer> DefaultCookieContainer = new Dictionary<string, CookieContainer>();
            private static ReaderWriterLock _slock = new ReaderWriterLock();
            /// <summary>
            /// 从文件中读取cookie
            /// </summary>
            /// <param name="file">文件路径，若此处留空则使用默认文件名</param>
            public static void LoadCookie(string file = DEFAULT_COOKIE_FILE_NAME)
            {
                try
                {
                    _slock.AcquireWriterLock(Timeout.Infinite);
                    var fi = new FileInfo(file);
                    if (fi.Exists && fi.Length > 0)
                    {
                        var stream = fi.OpenRead();
                        var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                        DefaultCookieContainer = (Dictionary<string, CookieContainer>)formatter.Deserialize(stream);
                        stream.Close();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.Print("在读取cookie文件时捕获到异常:\n" + ex.ToString());
                }
                finally
                {
                    _slock.ReleaseWriterLock();
                }
            }

            /// <summary>
            /// 从文件中写入cookie
            /// </summary>
            /// <param name="file">文件路径，若此处留空则使用默认文件名</param>
            public static void SaveCookie(string file = DEFAULT_COOKIE_FILE_NAME)
            {
                try
                {
                    _slock.AcquireWriterLock(Timeout.Infinite);
                    var stream = File.Create(file);
                    var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    formatter.Serialize(stream, DefaultCookieContainer);
                    stream.Close();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.Print("在写入cookie文件时捕获到异常:\n" + ex.ToString());
                }
                finally
                {
                    _slock.ReleaseWriterLock();
                }
            }
            #endregion

        }
        #region Parameter Segment
        public class Parameters : ICollection<KeyValuePair<string, string>>
        {
            private List<KeyValuePair<string, string>> _list;
            public Parameters()
            {
                _list = new List<KeyValuePair<string, string>>();
            }
            /// <summary>
            /// 添加参数
            /// </summary>
            /// <typeparam name="T">参数类型</typeparam>
            /// <param name="key">参数名称</param>
            /// <param name="value">参数的值</param>
            public void Add<T>(string key, T value)
            {
                _list.Add(new KeyValuePair<string, string>(key, value.ToString()));
            }
            /// <summary>
            /// 对所有参数按名称进行排序
            /// </summary>
            /// <param name="desc">是否使用倒序排序（默认为正序）</param>
            public void SortParameters(bool desc = false)
            {
                var n = new List<KeyValuePair<string, string>>();
                IOrderedEnumerable<KeyValuePair<string, string>> sec = null;
                if (desc) sec = from KeyValuePair<string, string> item in _list orderby item.Key ascending select item;
                else sec = from KeyValuePair<string, string> item in _list orderby item.Key descending select item;
                foreach (var item in sec)
                {
                    n.Add(item);
                }
                _list = n;
            }
            /// <summary>
            /// 构造url的查询参数
            /// </summary>
            /// <param name="enableUrlEncode">是否使用url转义</param>
            /// <returns>与参数等价的query string</returns>
            public string BuildQueryString(bool enableUrlEncode = true)
            {
                var sb = new StringBuilder();
                foreach (var item in _list)
                {
                    sb.Append(item.Key);
                    if (!string.IsNullOrEmpty(item.Key)) sb.Append('=');
                    if (enableUrlEncode) sb.Append(Uri.EscapeDataString(item.Value));
                    else sb.Append(item.Value);
                    sb.Append('&');
                }
                sb.Remove(sb.Length - 1, 1);
                return sb.ToString();
            }
            /// <summary>
            /// 移除首个匹配项
            /// </summary>
            /// <param name="key">参数名称</param>
            /// <returns>是否移除成功</returns>
            public bool Remove(string key)
            {
                for (int i = 0; i < _list.Count; i++)
                {
                    if (_list[i].Key == key)
                    {
                        _list.RemoveAt(i);
                        return true;
                    }
                }
                return false;
            }
            /// <summary>
            /// 移除指定下标的参数
            /// </summary>
            /// <param name="index">下标编号</param>
            /// <returns>是否移除成功</returns>
            public bool RemoveAt(int index)
            {
                if (index < _list.Count)
                {
                    _list.RemoveAt(index);
                    return true;
                }
                return false;
            }
            /// <summary>
            /// 移除所有匹配项
            /// </summary>
            /// <param name="key">参数名称</param>
            /// <returns>是否移除成功</returns>
            public bool RemoveAll(string key)
            {
                bool suc = false;
                for (int i = 0; i < _list.Count; i++)
                {
                    if (_list[i].Key == key)
                    {
                        _list.RemoveAt(i);
                        suc = true;
                    }
                }
                return suc;
            }
            /// <summary>
            /// 列表中是否包含指定名称的参数
            /// </summary>
            /// <param name="key">参数名称</param>
            /// <returns>是否存在该参数</returns>
            public bool Contains(string key)
            {
                for (int i = 0; i < _list.Count; i++)
                {
                    if (_list[i].Key == key)
                    {
                        return true;
                    }
                }
                return false;
            }
            /// <summary>
            /// 返回参数个数
            /// </summary>
            int ICollection<KeyValuePair<string, string>>.Count
            {
                get
                {
                    return _list.Count;
                }
            }
            /// <summary>
            /// 是否只读
            /// </summary>
            bool ICollection<KeyValuePair<string, string>>.IsReadOnly
            {
                get
                {
                    return false;
                }
            }
            /// <summary>
            /// 添加参数
            /// </summary>
            /// <param name="item">要添加的参数</param>
            void ICollection<KeyValuePair<string, string>>.Add(KeyValuePair<string, string> item)
            {
                _list.Add(item);
            }
            /// <summary>
            /// 清空参数列表
            /// </summary>
            void ICollection<KeyValuePair<string, string>>.Clear()
            {
                _list.Clear();
            }
            /// <summary>
            /// 是否包含某个参数（名称和数值全匹配）
            /// </summary>
            /// <param name="item">参数</param>
            /// <returns>是否存在该参数</returns>
            bool ICollection<KeyValuePair<string, string>>.Contains(KeyValuePair<string, string> item)
            {
                return _list.Contains(item);
            }
            /// <summary>
            /// 将列表复制到数组
            /// </summary>
            /// <param name="array">输出的数组</param>
            /// <param name="arrayIndex">要复制的下标开始点</param>
            void ICollection<KeyValuePair<string, string>>.CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
            {
                _list.CopyTo(array, arrayIndex);
            }
            /// <summary>
            /// 获取枚举器
            /// </summary>
            /// <returns>列表的枚举器</returns>
            IEnumerator IEnumerable.GetEnumerator()
            {
                return _list.GetEnumerator();
            }
            /// <summary>
            /// 获取枚举器
            /// </summary>
            /// <returns>列表的枚举器</returns>
            IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator()
            {
                return _list.GetEnumerator();
            }
            /// <summary>
            /// 移除匹配的参数
            /// </summary>
            /// <param name="item">参数</param>
            /// <returns></returns>
            bool ICollection<KeyValuePair<string, string>>.Remove(KeyValuePair<string, string> item)
            {
                return _list.Remove(item);
            }

            private string GetItems(int index)
            {
                if (index < 0 || index >= _list.Count) return string.Empty;
                return _list[index].Value;
            }
            private string GetItems(string name)
            {
                foreach (var item in _list)
                {
                    if (item.Key == name)
                        return item.Value;
                }
                return string.Empty;
            }
            private void SetItem(int index, string value)
            {
                if (index < 0 || index >= _list.Count) return;
                SetItem(index, _list[index].Key, value);
            }
            private void SetItem(int index, string key, string value)
            {
                if (index < 0 || index >= _list.Count) return;
                _list[index] = new KeyValuePair<string, string>(key, value);
            }
            private void SetItem(int index, KeyValuePair<string, string> data)
            {
                SetItem(index, data.Key, data.Value);
            }
            private void SetItem(string key, string value)
            {
                int index = _list.FindIndex((x) => { if (x.Key == key) return true; else return false; });
                if (index == -1) throw new KeyNotFoundException(key);
                _list[index] = new KeyValuePair<string, string>(key, value);
            }
            private void SetItem(KeyValuePair<string, string> data)
            {
                SetItem(data.Key, data.Value);
            }
            public string this[int index]
            {
                get
                {
                    return GetItems(index);
                }
                set
                {
                    SetItem(index, value);
                }
            }
            public override string ToString()
            {
                return BuildQueryString();
            }
        }
        #endregion
        public partial class NetStream : IDisposable
        {
            //static initialize setting
            static NetStream()
            {
                LoadCookie(); //读取cookie
                ServicePointManager.DefaultConnectionLimit = DEFAULT_TCP_CONNECTION;
                ServicePointManager.Expect100Continue = false;
                ServicePointManager.MaxServicePointIdleTime = 2000;
                ServicePointManager.SetTcpKeepAlive(false, 0, 0);
            }
            //是否开启调试输出
            private static bool _enableTracing = false;
            //HTTP请求和响应
            private HttpWebRequest _http_request;
            private HttpWebResponse _http_response;
            //全局锁
            private ReaderWriterLock _lock;
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
            private long _range;
            private object _state;
            private string _url;
            private HttpFinishedResponseCallback _callback;
            private string _post_content_type;
            private long _post_length;

            #region properties
            /// <summary>
            /// HTTP请求
            /// </summary>
            public HttpWebRequest HTTP_Request { get { return _http_request; } private set { _http_request = value; } }
            /// <summary>
            /// HTTP响应
            /// </summary>
            public HttpWebResponse HTTP_Response { get { return _http_response; } private set { _http_response = value; } }
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
            public string ProxyUrl { get { return Proxy.Address.ToString(); } set { Proxy = new WebProxy(value); } }
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

            #region cookie parser (instead of the origin method, which cause bugs)
            /// <summary>
            /// 解析Set Cookie的字符串
            /// </summary>
            /// <param name="header">Set Cookie的字符串值</param>
            /// <param name="defaultDomain">默认域名</param>
            /// <returns></returns>
            public static CookieCollection ParseCookie(string header, string defaultDomain)
            {
                if (_enableTracing)
                {
                    Tracer.GlobalTracer.TraceInfo("ParseCookie called: string header=" + header + ", string defaultDomain=" + defaultDomain);
                }
                if (string.IsNullOrEmpty(defaultDomain)) throw new ArgumentNullException(defaultDomain);

                var ret = new CookieCollection();
                int i = 0;
                var arg = new Dictionary<string, string>();

                if (string.IsNullOrEmpty(header)) return ret;

                int len = header.Length;
                while (i < len)
                {
                    _skipChar(header, ref i);
                    string name = _parseCookieValue(header, ref i, ";;name");
                    i += 1;
                    _skipChar(header, ref i);
                    string value = _parseCookieValue(header, ref i, ";;value");
                    while (i < len && header[i] != ',')
                    {
                        i++;
                        _skipChar(header, ref i);
                        string pkey = _parseCookieValue(header, ref i, ";;name").ToLower();
                        if (i < len && header[i] != ',') i++;
                        _skipChar(header, ref i);
                        string pvalue = _parseCookieValue(header, ref i, pkey);
                        _skipChar(header, ref i);
                        arg.Add(pkey, pvalue);
                    }

                    if (i >= header.Length || header[i] == ',')
                    {
                        i++;
                        bool skipflg = false;
                        if (!arg.ContainsKey("path")) skipflg = true;
                        if (!skipflg)
                        {
                            var domain = arg.ContainsKey("domain") ? arg["domain"] : defaultDomain;
                            var c = new Cookie(name, value, arg["path"], domain);
                            c.HttpOnly = arg.ContainsKey("httponly");

                            if (arg.ContainsKey("max-age"))
                                c.Expires = DateTime.Now.AddSeconds(int.Parse(arg["max-age"]));
                            else if (arg.ContainsKey("maxage"))
                                c.Expires = DateTime.Now.AddSeconds(int.Parse(arg["maxage"]));
                            else if (arg.ContainsKey("expires"))
                                c.Expires = _parseCookieExpireTime(arg["expires"]);
                            else
                                c.Expires = DateTime.MinValue; //session cookie

                            if (!skipflg) ret.Add(c);
                        }
                        arg.Clear();
                    }
                }
                return ret;
            }
            //跳过空格字符
            private static void _skipChar(string header, ref int index)
            {
                while (index < header.Length && header[index] == ' ')
                    index++;
            }
            //解析cookie的数据，直到分隔符为止
            private static string _parseCookieValue(string header, ref int index, string propertyName)
            {
                if (_enableTracing)
                {
                    //Tracer.GlobalTracer.TraceInfo("_parseCookieValue called");
                }
                string value = string.Empty;
                string limitstr = string.Empty;
                if (propertyName == ";;name") limitstr = ";,=";
                else if (propertyName == ";;value") limitstr = ";,";
                else if (propertyName == "expires") limitstr = ";=";
                else if (propertyName == "httponly") return string.Empty;
                else limitstr = ";,";
                while (index < header.Length && !limitstr.Contains(header[index]))
                {
                    value += header[index];
                    index++;
                }
                value = value.Trim('"');
                return value;
            }
            //解析cookie的有效时长
            private static DateTime _parseCookieExpireTime(string str)
            {
                if (_enableTracing)
                {
                    //Tracer.GlobalTracer.TraceInfo("_parseCookieExpireTime called");
                }
                int i = 4;
                _skipChar(str, ref i);
                int day = int.Parse(str.Substring(i, 2));
                i += 3;
                string smonth = str.Substring(i, 3);
                const string csmonth = "JanFebMarAprMayJunJulAugSepOctNovDec";
                int month = csmonth.IndexOf(smonth) / 3 + 1;
                if (month < 1 || month > 12) throw new ArgumentOutOfRangeException("Could not parse month string: " + smonth);
                i += 4;
                int year;
                if (int.TryParse(str.Substring(i, 4), out year))
                {
                    i += 5;
                }
                else if (int.TryParse(str.Substring(i, 2), out year))
                {
                    i += 3;
                    year += DateTime.Now.Year / 100 * 100;
                }
                else
                    throw new FormatException("Year format incorrect");

                _skipChar(str, ref i);
                int hour = int.Parse(str.Substring(i, 2));
                i += 3;
                int minute = int.Parse(str.Substring(i, 2));
                i += 3;
                int second = int.Parse(str.Substring(i, 2));
                i += 3;
                _skipChar(str, ref i);

                bool gmt_marker = false;
                if (i < str.Length)
                {
                    var marker = str.Substring(i, 3);
                    if (string.Equals(marker, "GMT", StringComparison.CurrentCultureIgnoreCase)) gmt_marker = true;
                    i += 4;
                }
                var date = new DateTime(0, gmt_marker ? DateTimeKind.Utc : DateTimeKind.Unspecified);
                date = date.AddYears(year - 1).AddMonths(month - 1).AddDays(day - 1);
                date = date.AddHours(hour).AddMinutes(minute).AddSeconds(second);
                return date;
            }
            #endregion

            //从param中添加参数到webrequest中（因为其中一些参数不能直接通过header设置）
            private static void _add_param_to_request_header(Parameters param, ref HttpWebRequest request)
            {
                if (param != null)
                {
                    foreach (var e in param)
                    {
                        switch (e.Key)
                        {
                            case STR_ACCEPT:
                                request.Accept = e.Value;
                                break;
                            case STR_CONNECTION:
                                switch (e.Value)
                                {
                                    case STR_CONNECTION_KEEP_ALIVE:
                                        request.Connection = "";
                                        request.KeepAlive = true;
                                        break;
                                    case STR_CONNECTION_CLOSE:
                                        request.KeepAlive = false;
                                        break;
                                    default:
                                        throw new ArgumentException("Invalid headerParam: " + STR_CONNECTION);
                                }
                                break;
                            case STR_CONTENT_LENGTH:
                                request.ContentLength = int.Parse(e.Value);
                                break;
                            case STR_CONTENT_TYPE:
                                request.ContentType = e.Value;
                                break;
                            case STR_EXPECT:
                                request.Expect = e.Value;
                                break;
                            case STR_DATE:
                                request.Date = DateTime.Parse(e.Value);
                                break;
                            case STR_HOST:
                                request.Host = e.Value;
                                break;
                            case STR_IF_MODIFIED_SINCE:
                                request.IfModifiedSince = DateTime.Parse(e.Value);
                                break;
                            case STR_RANGE:
                                string[] rangesplit = e.Value.Split('-');
                                if (rangesplit.Length != 2) throw new ArgumentException("Range format incorrect");
                                if (string.IsNullOrEmpty(rangesplit[0]))
                                    if (string.IsNullOrEmpty(rangesplit[1]))
                                        throw new ArgumentNullException("Range");
                                    else
                                        request.AddRange(-long.Parse(rangesplit[1]));
                                else
                                    if (string.IsNullOrEmpty(rangesplit[1]))
                                    request.AddRange(long.Parse(rangesplit[1]));
                                else
                                    request.AddRange(long.Parse(rangesplit[0]), long.Parse(rangesplit[1]));
                                break;
                            case STR_REFERER:
                                request.Referer = e.Value;
                                break;
                            case STR_TRANSFER_ENCODING:
                                request.TransferEncoding = e.Value;
                                break;
                            case STR_USER_AGENT:
                                request.UserAgent = e.Value;
                                break;
                            default:
                                request.Headers.Add(e.Key, e.Value);
                                break;
                        }
                    }
                }
            }
            //异步请求的回调函数，代替IAsyncResult
            public delegate void HttpFinishedResponseCallback(NetStream ns, object state);
            //传递参数的临时结构
            private struct _tmp_struct { public HttpFinishedResponseCallback cb; public object state; public Thread callthd; public bool get; public _tmp_struct(HttpFinishedResponseCallback c, object s, Thread t, bool g) { cb = c; state = s; callthd = t; get = g; } }
            /// <summary>
            /// 异步发送HTTP get请求
            /// </summary>
            /// <param name="url">url</param>
            /// <param name="callback">响应时的回调函数</param>
            /// <param name="state">响应时的参数</param>
            /// <param name="headerParam">请求头附加参数</param>
            /// <param name="urlParam">请求url附加参数</param>
            /// <param name="range">文件范围（该功能不一定支持）</param>
            /// <returns></returns>
            public IAsyncResult HttpGetAsync(string url, HttpFinishedResponseCallback callback, object state = null, Parameters headerParam = null, Parameters urlParam = null, long range = -1)
            {
                try
                {
                    _lock.AcquireWriterLock(Timeout.Infinite);
                    _fail_times = 0;
                    return _httpGetAsync(url, callback, state, headerParam, urlParam, range);
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                finally
                {
                    _lock.ReleaseWriterLock();
                }
            }
            private IAsyncResult _httpGetAsync(string url, HttpFinishedResponseCallback callback, object state, Parameters headerParam, Parameters urlParam, long range)
            {
                try { if (HTTP_Response != null) HTTP_Response.Close(); } finally { HTTP_Response = null; }
                try { if (RequestStream != null) { RequestStream.Close(); RequestStream.Dispose(); } } finally { RequestStream = null; }
                try { if (ResponseStream != null) { ResponseStream.Close(); ResponseStream.Dispose(); } } finally { ResponseStream = null; }
                _response_body_length = 0;
                _response_header_length = 0;
                _response_protocol_length = 0;
                do
                {
                    try
                    {
                        var post_url = url;
                        if (urlParam != null) post_url += "?" + urlParam.BuildQueryString();

                        HTTP_Request = (HttpWebRequest)WebRequest.Create(post_url);
                        //HTTP_Request.KeepAlive = true;
                        HTTP_Request.ConnectionGroupName = "defaultConnectionGroup";
                        _add_param_to_request_header(headerParam, ref _http_request);

                        var keyList = HTTP_Request.Headers.AllKeys.ToList();
                        if (!keyList.Contains(STR_ACCEPT)) HTTP_Request.Accept = Accept;
                        if (!keyList.Contains(STR_ACCEPT_ENCODING)) HTTP_Request.Headers.Add(STR_ACCEPT_ENCODING, AcceptEncoding);
                        if (!keyList.Contains(STR_ACCEPT_LANGUAGE)) HTTP_Request.Headers.Add(STR_ACCEPT_LANGUAGE, AcceptLanguage);
                        if (!keyList.Contains(STR_USER_AGENT)) HTTP_Request.UserAgent = UserAgent;

                        if (Proxy != null) HTTP_Request.Proxy = Proxy;
                        else if (IgnoreSystemProxy) HTTP_Request.Proxy = null;

                        if (UseCookie && !keyList.Contains(STR_COOKIE))
                        {
                            _slock.AcquireWriterLock(Timeout.Infinite);
                            if (!string.IsNullOrEmpty(CookieKey) && !DefaultCookieContainer.ContainsKey(CookieKey))
                                DefaultCookieContainer.Add(CookieKey, new CookieContainer());
                            HTTP_Request.CookieContainer = DefaultCookieContainer[CookieKey];
                            _slock.ReleaseWriterLock();
                        }

                        HTTP_Request.Method = DEFAULT_GET_METHOD;
                        HTTP_Request.ReadWriteTimeout = ReadWriteTimeOut;
                        HTTP_Request.Timeout = TimeOut;

                        if (range >= 0)
                        {
                            if (keyList.Contains(STR_RANGE))
                            {
                                if (_enableTracing)
                                    Tracer.GlobalTracer.TraceWarning("HTTP头已经包含Range信息，range参数将会忽略");
                            }
                            else
                                HTTP_Request.AddRange(range);
                        }

                        //length calculation
                        _request_protocol_length = 0;
                        _request_protocol_length += HTTP_Request.Method.Length; //"GET"
                        _request_protocol_length += 1; //empty space
                        _request_protocol_length += HTTP_Request.RequestUri.AbsoluteUri.Length; //uri
                        _request_protocol_length += 6; // empty space + "HTTP/"
                        _request_protocol_length += HTTP_Request.ProtocolVersion.ToString().Length; //"1.*"
                        _request_body_length = 0;
                        _request_header_length = 0;
                        foreach (string item in HTTP_Request.Headers)
                        {
                            _request_header_length += item.Length; //header name
                            _request_header_length += 2; // ":" + empty space
                            _request_header_length += HTTP_Request.Headers[item].Length; //header value
                        }
                        //special statistics for host and connection
                        _request_header_length += STR_HOST.Length + 2 + HTTP_Request.Host.Length;
                        _request_header_length += STR_CONNECTION.Length + 2 + (HTTP_Request.Connection == null ? STR_CONNECTION_KEEP_ALIVE.Length : HTTP_Request.Connection.Length);

                        //storing variables to class members
                        _url = url;
                        _header_param = headerParam;
                        _range = range;
                        _state = state;
                        _url_param = urlParam;
                        _callback = callback;

                        return HTTP_Request.BeginGetResponse(_httpAsyncResponse, new _tmp_struct(callback, state, Thread.CurrentThread, true));
                    }
                    catch (ThreadAbortException) { throw; }
                    catch (Exception ex)
                    {
                        if (_enableTracing) Tracer.GlobalTracer.TraceError(ex.ToString());
                        _fail_times++;
                        if (RetryTimes >= 0 && _fail_times > RetryTimes) throw ex;
                        if (RetryDelay > 0) Thread.Sleep(RetryDelay);
                    }
                } while (true);
            }
            //请求的回调函数，用于更新类里面的成员和调用自定义的回调函数
            private void _httpAsyncResponse(IAsyncResult iar)
            {
                try
                {
                    _lock.AcquireWriterLock(Timeout.Infinite);
                    if (HTTP_Request == null) throw new ArgumentNullException("HTTP_Request");
                    HTTP_Response = (HttpWebResponse)HTTP_Request.EndGetResponse(iar);
                    if (HTTP_Response != null)
                    {
                        if (UseCookie)
                        {
                            _slock.AcquireWriterLock(Timeout.Infinite);
                            if (!string.IsNullOrEmpty(CookieKey) && !DefaultCookieContainer.ContainsKey(CookieKey))
                                DefaultCookieContainer.Add(CookieKey, new CookieContainer());
                            DefaultCookieContainer[CookieKey].Add(ParseCookie(HTTP_Response.Headers[STR_SETCOOKIE], HTTP_Response.ResponseUri.Host));
                            _slock.ReleaseWriterLock();
                        }

                        //length calculation
                        _response_protocol_length = 5; //"HTTP/"
                        _response_protocol_length += HTTP_Response.ProtocolVersion.ToString().Length; //"1.*"
                        _response_protocol_length += 1; //empty space
                        _response_protocol_length += ((int)HTTP_Response.StatusCode).ToString().Length; //status code
                        _response_protocol_length += 1; //empty space
                        _response_protocol_length += HTTP_Response.StatusDescription.Length; //status string
                        _response_header_length = 0;
                        foreach (string item in HTTP_Response.Headers)
                        {
                            _response_header_length += item.Length; //header name
                            _response_header_length += 2; // ":" + empty space
                            _response_header_length += HTTP_Response.Headers[item].Length; //header value
                        }

                        _response_body_length = HTTP_Response.ContentLength;

                        switch (HTTP_Response.ContentEncoding)
                        {
                            case STR_ACCEPT_ENCODING_GZIP:
                                ResponseStream = new System.IO.Compression.GZipStream(HTTP_Response.GetResponseStream(), System.IO.Compression.CompressionMode.Decompress);
                                break;
                            case STR_ACCEPT_ENCODING_DEFLATE:
                                ResponseStream = new System.IO.Compression.DeflateStream(HTTP_Response.GetResponseStream(), System.IO.Compression.CompressionMode.Decompress);
                                break;
                            default:
                                ResponseStream = HTTP_Response.GetResponseStream();
                                break;
                        }

                        try
                        {
                            var data = (_tmp_struct)iar.AsyncState;
                            data.cb.Invoke(this, data.state);
                        }
                        catch (Exception ex)
                        {
                            if (_enableTracing) Tracer.GlobalTracer.TraceError(ex.ToString());
                        }
                    }
                }
                catch (ThreadAbortException) { throw; }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.RequestCanceled) return;// throw ex;
                    _fail_times++;

                    if (_enableTracing) Tracer.GlobalTracer.TraceError(ex.ToString());

                    //retries
                    if (_fail_times <= RetryTimes && ((_tmp_struct)iar.AsyncState).get)
                    {
                        try
                        {
                            _httpGetAsync(_url, _callback, _state, _header_param, _url_param, _range);
                        }
                        catch (Exception ex2)
                        {
                            throw ex2;
                        }
                        return; //ignore responsing error data
                    }

                    if (ex.Response != null)
                    {
                        try
                        {
                            _slock.AcquireWriterLock(Timeout.Infinite);
                            HTTP_Response = (HttpWebResponse)ex.Response;
                            if (!string.IsNullOrEmpty(CookieKey) && !DefaultCookieContainer.ContainsKey(CookieKey))
                                DefaultCookieContainer.Add(CookieKey, new CookieContainer());
                            DefaultCookieContainer[CookieKey].Add(ParseCookie(HTTP_Response.Headers[STR_SETCOOKIE], HTTP_Response.ResponseUri.Host));
                            _slock.ReleaseWriterLock();

                            switch (HTTP_Response.ContentEncoding)
                            {
                                case STR_ACCEPT_ENCODING_GZIP:
                                    ResponseStream = new System.IO.Compression.GZipStream(HTTP_Response.GetResponseStream(), System.IO.Compression.CompressionMode.Decompress);
                                    break;
                                case STR_ACCEPT_ENCODING_DEFLATE:
                                    ResponseStream = new System.IO.Compression.DeflateStream(HTTP_Response.GetResponseStream(), System.IO.Compression.CompressionMode.Decompress);
                                    break;
                                default:
                                    ResponseStream = HTTP_Response.GetResponseStream();
                                    break;
                            }
                        }
                        catch (Exception ex2) { throw ex2; }
                    }

                    try
                    {
                        var data = (_tmp_struct)iar.AsyncState;
                        data.cb.Invoke(this, data.state);
                    }
                    catch (Exception ex2)
                    {
                        if (_enableTracing) Tracer.GlobalTracer.TraceError(ex2.ToString());
                    }
                }
                catch (Exception ex)
                {
                    if (_enableTracing) Tracer.GlobalTracer.TraceError(ex.ToString());
                    try
                    {
                        var data = (_tmp_struct)iar.AsyncState;
                        data.cb.Invoke(this, data.state);
                    }
                    catch (Exception ex2)
                    {
                        if (_enableTracing) Tracer.GlobalTracer.TraceError(ex2.ToString());
                    }
                }
                finally
                {
                    _lock.ReleaseWriterLock();
                }
            }

            /// <summary>
            /// 异步发送HTTP post请求
            /// </summary>
            /// <param name="url">url</param>
            /// <param name="postLength">数据长度（必填，不能为负）</param>
            /// <param name="callback">获取RequestStream时的回调函数</param>
            /// <param name="state">获取RequestStream时的参数</param>
            /// <param name="postContentType">要发送的数据类型</param>
            /// <param name="headerParam">请求头附加参数</param>
            /// <param name="urlParam">请求url附加参数</param>
            /// <param name="range">文件范围（该功能不一定支持）</param>
            /// <returns></returns>
            public IAsyncResult HttpPostAsync(string url, long postLength, HttpFinishedResponseCallback callback, object state = null, string postContentType = DEFAULT_CONTENT_TYPE_BINARY, Parameters headerParam = null, Parameters urlParam = null, long range = -1)
            {
                try
                {
                    _lock.AcquireWriterLock(Timeout.Infinite);
                    _fail_times = 0;
                    return _httpPostAsync(url, postLength, callback, state, postContentType, headerParam, urlParam, range);
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                finally
                {
                    _lock.ReleaseWriterLock();
                }
            }
            private IAsyncResult _httpPostAsync(string url, long postLength, HttpFinishedResponseCallback callback, object state, string postContentType, Parameters headerParam, Parameters urlParam, long range)
            {
                try { if (HTTP_Response != null) HTTP_Response.Close(); } finally { HTTP_Response = null; }
                try { if (RequestStream != null) { RequestStream.Close(); RequestStream.Dispose(); } } finally { RequestStream = null; }
                try { if (ResponseStream != null) { ResponseStream.Close(); ResponseStream.Dispose(); } } finally { ResponseStream = null; }
                do
                {
                    try
                    {
                        var post_url = url;
                        if (urlParam != null) post_url += "?" + urlParam.BuildQueryString();

                        HTTP_Request = (HttpWebRequest)WebRequest.Create(post_url);
                        HTTP_Request.KeepAlive = true;
                        HTTP_Request.ConnectionGroupName = "defaultConnectionGroup";

                        _add_param_to_request_header(headerParam, ref _http_request);
                        var keyList = HTTP_Request.Headers.AllKeys.ToList();
                        if (!keyList.Contains(STR_ACCEPT)) HTTP_Request.Accept = Accept;
                        if (!keyList.Contains(STR_ACCEPT_ENCODING)) HTTP_Request.Headers.Add(STR_ACCEPT_ENCODING, AcceptEncoding);
                        if (!keyList.Contains(STR_ACCEPT_LANGUAGE)) HTTP_Request.Headers.Add(STR_ACCEPT_LANGUAGE, AcceptLanguage);
                        if (!keyList.Contains(STR_USER_AGENT)) HTTP_Request.UserAgent = UserAgent;

                        if (Proxy != null) HTTP_Request.Proxy = Proxy;
                        else if (IgnoreSystemProxy) HTTP_Request.Proxy = null;

                        if (UseCookie && !keyList.Contains(STR_COOKIE))
                        {
                            _slock.AcquireWriterLock(Timeout.Infinite);
                            if (!string.IsNullOrEmpty(CookieKey) && !DefaultCookieContainer.ContainsKey(CookieKey))
                                DefaultCookieContainer.Add(CookieKey, new CookieContainer());
                            HTTP_Request.CookieContainer = DefaultCookieContainer[CookieKey];
                            _slock.ReleaseWriterLock();
                        }

                        HTTP_Request.Method = DEFAULT_POST_METHOD;
                        HTTP_Request.ReadWriteTimeout = ReadWriteTimeOut;
                        HTTP_Request.Timeout = TimeOut;

                        HTTP_Request.ContentLength = postLength;
                        HTTP_Request.ContentType = postContentType;

                        //HTTP_Request.SendChunked = true;
                        HTTP_Request.AllowWriteStreamBuffering = false;

                        if (range >= 0)
                        {
                            if (keyList.Contains(STR_RANGE))
                            {
                                if (_enableTracing)
                                    Tracer.GlobalTracer.TraceWarning("HTTP头已经包含Range信息，range参数将会忽略");
                            }
                            else
                                HTTP_Request.AddRange(range);
                        }

                        //length calculation
                        _request_protocol_length = 0;
                        _request_protocol_length += HTTP_Request.Method.Length; //"GET"
                        _request_protocol_length += 1; //empty space
                        _request_protocol_length += HTTP_Request.RequestUri.AbsoluteUri.Length; //uri
                        _request_protocol_length += 6; // empty space + "HTTP/"
                        _request_protocol_length += HTTP_Request.ProtocolVersion.ToString().Length; //"1.*"
                        _request_body_length = postLength;
                        _request_header_length = 0;
                        foreach (string item in HTTP_Request.Headers)
                        {
                            _request_header_length += item.Length; //header name
                            _request_header_length += 2; // ":" + empty space
                            _request_header_length += HTTP_Request.Headers[item].Length; //header value
                        }
                        //special statistics for host and connection
                        _request_header_length += STR_HOST.Length + 2 + HTTP_Request.Host.Length;
                        _request_header_length += STR_CONNECTION.Length + 2 + (HTTP_Request.Connection == null ? STR_CONNECTION_KEEP_ALIVE.Length : HTTP_Request.Connection.Length);

                        //storing variables to class members
                        _url = url;
                        _header_param = headerParam;
                        _range = range;
                        _state = state;
                        _url_param = urlParam;
                        _callback = callback;
                        _post_content_type = postContentType;
                        _post_length = postLength;

                        return HTTP_Request.BeginGetRequestStream(_httpPostAsyncRequest, new _tmp_struct(callback, state, Thread.CurrentThread, false));
                    }
                    catch (ThreadAbortException) { throw; }
                    catch (WebException ex)
                    {
                        if (_enableTracing) Tracer.GlobalTracer.TraceError(ex.ToString());
                        _fail_times++;

                        if (ex.Response != null)
                        {
                            try
                            {
                                HTTP_Response = (HttpWebResponse)ex.Response;
                                switch (HTTP_Response.ContentEncoding)
                                {
                                    case STR_ACCEPT_ENCODING_GZIP:
                                        ResponseStream = new System.IO.Compression.GZipStream(HTTP_Response.GetResponseStream(), System.IO.Compression.CompressionMode.Decompress);
                                        break;
                                    case STR_ACCEPT_ENCODING_DEFLATE:
                                        ResponseStream = new System.IO.Compression.DeflateStream(HTTP_Response.GetResponseStream(), System.IO.Compression.CompressionMode.Decompress);
                                        break;
                                    default:
                                        ResponseStream = HTTP_Response.GetResponseStream();
                                        break;
                                }
                            }
                            catch (Exception ex2)
                            {
                                throw ex2;
                            }
                        }
                        if (RetryTimes >= 0 && _fail_times > RetryTimes) throw ex;
                        if (RetryDelay > 0) Thread.Sleep(RetryDelay);
                    }
                    catch (Exception ex)
                    {
                        if (_enableTracing) Tracer.GlobalTracer.TraceError(ex.ToString());
                        _fail_times++;
                        if (RetryTimes >= 0 && _fail_times > RetryTimes) throw ex;
                        if (RetryDelay > 0) Thread.Sleep(RetryDelay);
                    }
                } while (true);
            }
            //post时用于EndGetRequestStream的回调函数，用于更新类
            private void _httpPostAsyncRequest(IAsyncResult iar)
            {
                //排斥调用方线程的代码，.net不知为什么在GetRequestStream的异步方法会调用本身的线程（非线程池线程）
                if (((_tmp_struct)iar.AsyncState).callthd == Thread.CurrentThread)
                {
                    var thd_start_event = new ManualResetEventSlim();
                    ThreadPool.QueueUserWorkItem(delegate { thd_start_event.Set(); _httpPostAsyncRequest(iar); });
                    thd_start_event.Wait();
                    return;
                }

                try
                {
                    _lock.AcquireWriterLock(Timeout.Infinite);
                    if (HTTP_Request == null) return;
                    RequestStream = HTTP_Request.EndGetRequestStream(iar);
                }
                catch (ThreadAbortException) { }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.RequestCanceled) return; // throw ex;
                    _fail_times++;

                    //retries
                    if (_fail_times <= RetryTimes)
                    {
                        try
                        {
                            _httpPostAsync(_url, _post_length, _callback, _state, _post_content_type, _header_param, _url_param, _range);
                        }
                        catch (Exception ex2)
                        {
                            throw ex2;
                        }
                        return; //ignore responsing error data
                    }

                    if (_enableTracing) Tracer.GlobalTracer.TraceError(ex.ToString());
                    if (ex.Response != null)
                    {
                        try
                        {
                            HTTP_Response = (HttpWebResponse)ex.Response;
                            _slock.AcquireWriterLock(Timeout.Infinite);
                            if (!string.IsNullOrEmpty(CookieKey) && !DefaultCookieContainer.ContainsKey(CookieKey))
                                DefaultCookieContainer.Add(CookieKey, new CookieContainer());
                            HTTP_Request.CookieContainer = DefaultCookieContainer[CookieKey];
                            _slock.ReleaseWriterLock();

                            switch (HTTP_Response.ContentEncoding)
                            {
                                case STR_ACCEPT_ENCODING_GZIP:
                                    ResponseStream = new System.IO.Compression.GZipStream(HTTP_Response.GetResponseStream(), System.IO.Compression.CompressionMode.Decompress);
                                    break;
                                case STR_ACCEPT_ENCODING_DEFLATE:
                                    ResponseStream = new System.IO.Compression.DeflateStream(HTTP_Response.GetResponseStream(), System.IO.Compression.CompressionMode.Decompress);
                                    break;
                                default:
                                    ResponseStream = HTTP_Response.GetResponseStream();
                                    break;
                            }
                        }
                        catch (Exception) { }
                    }
                }
                finally
                {
                    _lock.ReleaseWriterLock();
                    try
                    {
                        var data = (_tmp_struct)iar.AsyncState;
                        data.cb.Invoke(this, data.state);
                    }
                    catch (Exception ex)
                    {
                        if (_enableTracing) Tracer.GlobalTracer.TraceError(ex.ToString());
                    }
                }
            }
            /// <summary>
            /// 异步获取HTTP post响应
            /// </summary>
            /// <param name="callback">响应时的回调函数</param>
            /// <param name="state">响应时的参数</param>
            /// <returns></returns>
            public IAsyncResult HttpPostResponseAsync(HttpFinishedResponseCallback callback, object state = null)
            {
                try
                {
                    _lock.AcquireWriterLock(Timeout.Infinite);
                    return _httpPostAsyncResponse(callback, state);
                }
                finally
                {
                    _lock.ReleaseWriterLock();
                }
            }
            private IAsyncResult _httpPostAsyncResponse(HttpFinishedResponseCallback callback, object state)
            {
                if (HTTP_Request == null) return null;
                try
                {
                    if (RequestStream != null && RequestStream.CanWrite)
                    {
                        RequestStream.Close();
                        RequestStream.Dispose();
                        RequestStream = null;
                    }
                }
                catch (Exception) { }

                try
                {
                    return HTTP_Request.BeginGetResponse(_httpAsyncResponse, new _tmp_struct(callback, state, Thread.CurrentThread, false));
                }
                catch (ThreadAbortException) { throw; }
                catch (WebException ex)
                {
                    if (_enableTracing) Tracer.GlobalTracer.TraceError(ex.ToString());

                    if (ex.Response != null)
                    {
                        try
                        {
                            HTTP_Response = (HttpWebResponse)ex.Response;
                            switch (HTTP_Response.ContentEncoding)
                            {
                                case STR_ACCEPT_ENCODING_GZIP:
                                    ResponseStream = new System.IO.Compression.GZipStream(HTTP_Response.GetResponseStream(), System.IO.Compression.CompressionMode.Decompress);
                                    break;
                                case STR_ACCEPT_ENCODING_DEFLATE:
                                    ResponseStream = new System.IO.Compression.DeflateStream(HTTP_Response.GetResponseStream(), System.IO.Compression.CompressionMode.Decompress);
                                    break;
                                default:
                                    ResponseStream = HTTP_Response.GetResponseStream();
                                    break;
                            }
                        }
                        catch (Exception) { }
                    }
                    throw ex;
                }
                catch (Exception ex)
                {
                    if (_enableTracing) Tracer.GlobalTracer.TraceError(ex.ToString());
                    throw ex;
                }
            }
            /// <summary>
            /// 关闭该数据流，释放所有使用的网络和内存资源
            /// </summary>
            public void Close()
            {
                if (RequestStream != null)
                {
                    try
                    {
                        RequestStream.Close();
                        RequestStream.Dispose();
                    }
                    catch (Exception)
                    {
                    }
                    RequestStream = null;
                }
                if (ResponseStream != null)
                {
                    try
                    {
                        ResponseStream.Close();
                        ResponseStream.Dispose();
                    }
                    catch (Exception)
                    {
                    }
                    ResponseStream = null;
                }
                if (HTTP_Response != null)
                {
                    try
                    {
                        HTTP_Response.Close();
                    }
                    catch (Exception)
                    {
                    }
                    HTTP_Response = null;
                }
                if (HTTP_Request != null)
                {
                    try
                    {
                        HTTP_Request.Abort();
                    }
                    catch (Exception)
                    {
                    }
                    HTTP_Request = null;
                }
            }
            /// <summary>
            /// 释放所有使用的网络和内存资源
            /// </summary>
            public void Dispose()
            {
                Close();
            }
            /// <summary>
            /// 从ResponseStream中读取所有数据并返回字符串（同步执行）
            /// </summary>
            /// <param name="encoding">可选编码类型，默认utf-8</param>
            /// <returns></returns>
            public string ReadResponseString(Encoding encoding = null)
            {
                try
                {
                    _lock.AcquireWriterLock(Timeout.Infinite);
                    if (encoding == null)
                        encoding = Encoding.GetEncoding(DEFAULT_ENCODING);

                    if (ResponseStream == null || !ResponseStream.CanRead) return string.Empty;
                    var sr = new StreamReader(ResponseStream, encoding);
                    var str = sr.ReadToEnd();
                    sr.Close();
                    sr.Dispose();
                    ResponseStream = null;
                    return str;
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                finally
                {
                    _lock.ReleaseWriterLock();
                }
            }

            /// <summary>
            /// 从ResponseStream中读取所有数据并返回响应的字节数组
            /// 注：该方法仅用于较小的数据量
            /// </summary>
            /// <returns></returns>
            private byte[] ReadResponseBinary()
            {
                try
                {
                    _lock.AcquireWriterLock(Timeout.Infinite);
                    byte[] container = null;
                    if (HTTP_Response == null) return null;
                    if (ResponseStream == null) return null;
                    if (HTTP_Response.ContentLength < 0)
                    {
                        var ls_container = new List<byte>(4096);
                        var buffer = new byte[4096];
                        var bytes_readed = 0;
                        do
                        {
                            bytes_readed = ResponseStream.Read(buffer, 0, 4096);
                            var available_bytes = new byte[bytes_readed];
                            Array.Copy(buffer, 0, available_bytes, 0, bytes_readed);
                            ls_container.AddRange(available_bytes);
                        } while (bytes_readed > 0);
                        container = ls_container.ToArray();
                        ls_container.Clear();
                    }
                    else
                    {
                        container = new byte[HTTP_Response.ContentLength];
                        var index = 0;
                        var buffer = new byte[4096];
                        var bytes_readed = 0;
                        do
                        {
                            bytes_readed = ResponseStream.Read(buffer, 0, 4096);
                            Array.Copy(buffer, 0, container, index, bytes_readed);
                            index += bytes_readed;
                        } while (bytes_readed > 0);
                    }
                    return container;
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                finally
                {
                    _lock.ReleaseWriterLock();
                }
            }
            public NetStream()
            {
                UseCookie = true;
                Proxy = null;
                TimeOut = DEFAULT_TIMEOUT;
                ReadWriteTimeOut = DEFAULT_READ_WRITE_TIMEOUT;
                AcceptEncoding = DEFAULT_ACCEPT_ENCODING;
                AcceptLanguage = DEFAULT_ACCEPT_LANGUAGE;
                Accept = DEFAULT_ACCEPT;
                UserAgent = DEFAULT_USER_AGENT;
                ContentType = DEFAULT_CONTENT_TYPE_BINARY;
                RetryDelay = 0;
                RetryTimes = 0;
                CookieKey = DEFAULT_COOKIE_GROUP;
                _lock = new ReaderWriterLock();
            }

            #region sync method
            /// <summary>
            /// 同步发送HTTP get请求
            /// </summary>
            /// <param name="url">url</param>
            /// <param name="headerParam">请求头附加参数</param>
            /// <param name="urlParam">请求url附加参数</param>
            /// <param name="range">文件范围（该功能不一定支持）</param>
            public void HttpGet(string url, Parameters headerParam = null, Parameters urlParam = null, long range = -1)
            {
                var set_event = new ManualResetEventSlim();
                var iar = HttpGetAsync(url, (ns, state) => { set_event.Set(); }, null, headerParam, urlParam, range);
                iar.AsyncWaitHandle.WaitOne();
                set_event.Wait();
            }
            /// <summary>
            /// 同步发送HTTP post请求
            /// </summary>
            /// <param name="url">url</param>
            /// <param name="length">数据长度（必填，不能为负）</param>
            /// <param name="contentType">要发送的数据类型</param>
            /// <param name="headerParam">请求头附加参数</param>
            /// <param name="urlParam">请求url附加参数</param>
            /// <param name="range">文件范围（该功能不一定支持）</param>
            /// <returns></returns>
            public Stream HttpPost(string url, long length, string contentType = DEFAULT_CONTENT_TYPE_BINARY, Parameters headerParam = null, Parameters urlParam = null, long range = -1)
            {
                var set_event = new ManualResetEventSlim();
                var iar = HttpPostAsync(url, length, (ns, state) => { set_event.Set(); }, null, contentType, headerParam, urlParam, range);
                iar.AsyncWaitHandle.WaitOne();
                set_event.Wait();
                return RequestStream;
            }
            /// <summary>
            /// 同步获取HTTP post响应（结束HTTP post请求）
            /// </summary>
            public void HttpPostClose()
            {
                var set_event = new ManualResetEventSlim();
                var iar = HttpPostResponseAsync((ns, state) => { set_event.Set(); }, null);
                iar.AsyncWaitHandle.WaitOne();
                set_event.Wait();
            }
            /// <summary>
            /// 同步获取HTTP post响应
            /// </summary>
            /// <param name="url">url</param>
            /// <param name="data">数据的字节数组</param>
            /// <param name="contentType">要发送的数据类型</param>
            /// <param name="headerParam">请求头附加参数</param>
            /// <param name="urlParam">请求url附加参数</param>
            /// <param name="range">文件范围（该功能不一定支持）</param>
            public void HttpPost(string url, byte[] data, string contentType = DEFAULT_CONTENT_TYPE_BINARY, Parameters headerParam = null, Parameters urlParam = null, long range = -1)
            {
                var stream = HttpPost(url, data.Length, contentType, headerParam, urlParam, range);
                if (stream == null)
                {
                    Close();
                    return;
                }
                stream.Write(data, 0, data.Length);
                stream.Close();
                HttpPostClose();
            }
            /// <summary>
            /// 同步获取HTTP post响应
            /// </summary>
            /// <param name="url">url</param>
            /// <param name="postParam">要发送的数据</param>
            /// <param name="contentType">要发送的数据类型</param>
            /// <param name="headerParam">请求头附加参数</param>
            /// <param name="urlParam">请求url附加参数</param>
            /// <param name="range">文件范围（该功能不一定支持）</param>
            public void HttpPost(string url, Parameters postParam, string contentType = DEFAULT_CONTENT_TYPE_PARAM, Parameters headerParam = null, Parameters urlParam = null, long range = -1)
            {
                HttpPost(url, Encoding.GetEncoding(DEFAULT_ENCODING).GetBytes(postParam.BuildQueryString()), contentType, headerParam, urlParam, range);
            }
            #endregion
        }


    }

}
