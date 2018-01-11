using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GlobalUtil;
using GlobalUtil.NetUtils;
using static BaiduCloudSync.BaiduPCS;
using System.IO;
using System.Threading;

namespace BaiduCloudSync
{
    /// <summary>
    /// 百度网盘的文件下载器类，用于进行多线程并行下载。
    /// </summary>
    public class Downloader : IDisposable, ITransfer
    {
        //错误输出
        private const bool _enable_error_tracing = true;
        //默认下载线程
        public const int DEFAULT_MAX_THREAD = 96;
        //每个间隔的请求数
        private const int _PARALLEL_START_REQUEST_COUNT = 2;
        //每个缓存段进行IO写入时需要达到的数据量
        private const int _MIN_IO_FLUSH_DATA_LENGTH = 32768; //32KB

        //api请求，用于获得下载链接
        private RemoteFileCacher _api;
        //下载文件的数据
        private ObjectMetadata _data;
        //保存地址
        private string _output_path;
        private string _cookie_key;
        private KeyManager _key_manager;

        //线程标识
        private volatile int _download_thread_flag;
        private const int _DOWNLOAD_THREAD_FLAG_READY = 128;
        private const int _DOWNLOAD_THREAD_FLAG_PAUSE_REQUESTED = 1;
        private const int _DOWNLOAD_THREAD_FLAG_PAUSED = 2;
        private const int _DOWNLOAD_THREAD_FLAG_STOP_REQUESTED = 4;
        private const int _DOWNLOAD_THREAD_FLAG_STOPPED = 8;
        private const int _DOWNLOAD_THREAD_FLAG_START_REQUESTED = 16;
        private const int _DOWNLOAD_THREAD_FLAG_STARTED = 32;
        private const int _DOWNLOAD_THREAD_FLAG_FINISHED = 64;
        private const int _DOWNLOAD_THREAD_FLAG_ERROR = int.MinValue;
        private const int _DOWNLOAD_THREAD_FLAG_DECRYPTING = 0x40000000;

        //单任务最大连接数
        private int _max_thread;

        private TaskDispatcher _dispatcher; //分段分配
        private string[] _urls; //url
        private int _url_fail_to_fetch_count; //连续获取url失败次数
        private const int _MAX_URL_FAIL_COUNT = 3; //达到此数值时会标志为错误

        private Guid[] _guid_list; //分段的id
        private DateTime[] _last_receive; //分段最后接收数据的时间，用于主动模式下的timeout
        private QueueStream[] _buffer_stream; //缓存数据流
        private NetStream[] _request; //分段的http请求
        private ulong[] _position; //分段的位置
        private long[] _io_position; //分段的写入位置 (seek _io_position->write data)
        private FileStream _file_stream; //本地文件数据流
        private Thread _monitor_thread; //后台监控线程
        private ManualResetEventSlim _monitor_thread_created; //是否已创建监控线程
        //status
        private DateTime _start_time, _end_time; //任务开始时间
        private double _average_speed_total; //全局平均速度
        private double _average_speed_5s; //5秒内的平均速度
        private LinkedList<ulong> _last_5s_length;
        private long _downloaded_size; //已下载的字节数
        private DateTime _url_expire_time; //url失效时间

        private long _current_bytes; //当前时间下载的字节数
        private int _speed_limit; //速度显示，单位为Bps，0为无限制
        //locks
        private object _external_lock;
        private object[] _thread_data_lock;
        private object _url_lock;
        private object _thread_flag_lock;
        public Downloader(RemoteFileCacher pcs, ObjectMetadata remote_file, string local_file, int max_thread = DEFAULT_MAX_THREAD, int speed_limit = 0, KeyManager key_manager = null)
        {
            if (pcs == null) throw new ArgumentNullException("pcs");
            if (string.IsNullOrEmpty(remote_file.Path)) throw new ArgumentNullException("remote_file.Path");
            if (remote_file.AccountID < 0) throw new ArgumentOutOfRangeException("remote_file.AccountID");
            if (max_thread <= 0) throw new ArgumentOutOfRangeException("max_thread");
            if (string.IsNullOrEmpty(local_file)) throw new ArgumentNullException("local_file");

            _external_lock = new object();
            _url_lock = new object();
            _thread_flag_lock = new object();
            _api = pcs;
            _data = remote_file;
            _output_path = local_file;
            _max_thread = max_thread;
            _key_manager = key_manager;

            _dispatcher = new TaskDispatcher(_data.Size);
            _download_thread_flag = _DOWNLOAD_THREAD_FLAG_READY;
            var fileinfo = new FileInfo(_output_path);
            if (!fileinfo.Directory.Exists) fileinfo.Directory.Create();
            _file_stream = new FileStream(_output_path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            _monitor_thread_created = new ManualResetEventSlim();

            _last_5s_length = new LinkedList<ulong>();
            for (int i = 0; i < 6; i++)
                _last_5s_length.AddFirst(0);
            _cookie_key = _api.GetAccount(_data.AccountID).Auth.CookieIdentifier;

            _current_bytes = 0;
            _speed_limit = speed_limit;
        }

        ~Downloader()
        {
            Dispose();
        }

        #region public func
        /// <summary>
        /// 开始下载任务
        /// </summary>
        public void Start()
        {
            lock (_external_lock)
            {
                lock (_thread_flag_lock)
                {
                    //clear error flag
                    _download_thread_flag = _download_thread_flag & ~_DOWNLOAD_THREAD_FLAG_ERROR;

                    if (((_download_thread_flag & 0xffffff) & (_DOWNLOAD_THREAD_FLAG_READY | _DOWNLOAD_THREAD_FLAG_PAUSED)) != 0)
                    {
                        //Tracer.GlobalTracer.TraceInfo("---STARTED---");
                        _download_thread_flag = (_download_thread_flag | _DOWNLOAD_THREAD_FLAG_START_REQUESTED) & ~_DOWNLOAD_THREAD_FLAG_READY;
                        _url_fail_to_fetch_count = 0;
                        //_api.GetAccount(_data.AccountID).GetLocateDownloadLinkAsync(_data.Path, _main_url_request_callback);
                        _url_expire_time = DateTime.Now;
                        _monitor_thread = new Thread(_monitor_thread_callback);
                        _monitor_thread.Name = "Download Monitor";
                        _monitor_thread.IsBackground = false;
                        _monitor_thread.Start();

                    }
                }
            }
            try { TaskStarted?.Invoke(this, new EventArgs()); }
            catch { }
        }
        /// <summary>
        /// 暂停下载任务
        /// </summary>
        public void Pause()
        {
            lock (_external_lock)
            {
                lock (_thread_flag_lock)
                {
                    if ((_download_thread_flag & 0xffffff) == _DOWNLOAD_THREAD_FLAG_READY)
                    {
                        _download_thread_flag = (_download_thread_flag | _DOWNLOAD_THREAD_FLAG_PAUSED) & ~_DOWNLOAD_THREAD_FLAG_READY;
                        return;
                    }
                    if (((_download_thread_flag & 0xffffff) & (_DOWNLOAD_THREAD_FLAG_START_REQUESTED | _DOWNLOAD_THREAD_FLAG_STARTED)) != 0)
                    {
                        //Tracer.GlobalTracer.TraceInfo("---PAUSED---");
                        _download_thread_flag |= _DOWNLOAD_THREAD_FLAG_PAUSE_REQUESTED;
                    }
                    else
                        return;
                }
                _monitor_thread_created.Wait();
                _monitor_thread.Join();
                _monitor_thread_created.Reset();
            }
            try { TaskPaused?.Invoke(this, new EventArgs()); }
            catch { }
        }
        /// <summary>
        /// 取消下载任务
        /// </summary>
        public void Cancel()
        {
            lock (_external_lock)
            {
                lock (_thread_flag_lock)
                {
                    //Tracer.GlobalTracer.TraceInfo("---CANCELLED---");
                    if (((_download_thread_flag & 0xffffff) & (_DOWNLOAD_THREAD_FLAG_STOPPED | _DOWNLOAD_THREAD_FLAG_FINISHED)) != 0)
                        return;
                    if ((_download_thread_flag & 0xffffff) == _DOWNLOAD_THREAD_FLAG_READY)
                    {
                        _download_thread_flag = (_download_thread_flag | _DOWNLOAD_THREAD_FLAG_STOPPED) & ~_DOWNLOAD_THREAD_FLAG_READY;
                        return;
                    }
                    else if ((_download_thread_flag & 0xffffff) == _DOWNLOAD_THREAD_FLAG_PAUSED)
                    {
                        _download_thread_flag = (_download_thread_flag | _DOWNLOAD_THREAD_FLAG_STOPPED) & ~_DOWNLOAD_THREAD_FLAG_PAUSED;
                        return;
                    }
                    else if (((_download_thread_flag & 0xffffff) & (_DOWNLOAD_THREAD_FLAG_STARTED | _DOWNLOAD_THREAD_FLAG_START_REQUESTED)) != 0)
                    {
                        _download_thread_flag |= _DOWNLOAD_THREAD_FLAG_STOP_REQUESTED;
                    }
                }
                _monitor_thread_created.Wait();
                _monitor_thread.Join();
                _monitor_thread_created.Reset();
                Dispose();
            }
            try { TaskCancelled?.Invoke(this, new EventArgs()); }
            catch { }
        }
        #endregion

        #region public properties
        /// <summary>
        /// 网盘的文件信息
        /// </summary>
        public ObjectMetadata RemoteFile { get { return _data; } }
        /// <summary>
        /// 任务开始时间
        /// </summary>
        public DateTime StartTime { get { return _start_time; } }
        /// <summary>
        /// 任务经历的时间
        /// </summary>
        public TimeSpan EllapsedTime { get { return ((_download_thread_flag & _DOWNLOAD_THREAD_FLAG_STARTED) != 0) ? (DateTime.Now - _start_time) : (_end_time - _start_time); } }
        /// <summary>
        /// 总平均速率
        /// </summary>
        public double AverageSpeedTotal { get { return _average_speed_total; } }
        /// <summary>
        /// 过去5秒的平均速率
        /// </summary>
        public double AverageSpeed5s { get { return _average_speed_5s; } }
        /// <summary>
        /// 已下载大小
        /// </summary>
        public long DownloadedSize { get { return _downloaded_size; } }
        /// <summary>
        /// 下载最大的线程数
        /// </summary>
        public int MaxThread { get { return _max_thread; } set { if (value <= 0) throw new ArgumentOutOfRangeException("MaxThread"); _max_thread = value; } }
        /// <summary>
        /// 本地文件的完整文件路径
        /// </summary>
        public string LocalFilePath { get { return _output_path; } }
        [Flags]
        public enum State
        {
            READY = _DOWNLOAD_THREAD_FLAG_READY,
            START_REQUESTED = _DOWNLOAD_THREAD_FLAG_START_REQUESTED,
            STARTED = _DOWNLOAD_THREAD_FLAG_STARTED,
            PAUSE_REQUESTED = _DOWNLOAD_THREAD_FLAG_PAUSE_REQUESTED,
            PAUSED = _DOWNLOAD_THREAD_FLAG_PAUSED,
            CANCEL_REQUEST = _DOWNLOAD_THREAD_FLAG_STOP_REQUESTED,
            CANCELLED = _DOWNLOAD_THREAD_FLAG_STOPPED,
            FINISHED = _DOWNLOAD_THREAD_FLAG_FINISHED,
            ERROR = _DOWNLOAD_THREAD_FLAG_ERROR,
            DECRYPTING = _DOWNLOAD_THREAD_FLAG_DECRYPTING
        }
        /// <summary>
        /// 任务状态
        /// </summary>
        public State TaskState { get { return (State)_download_thread_flag; } }
        /// <summary>
        /// 速度限制，单位：B/s
        /// </summary>
        public int SpeedLimit { get { return _speed_limit; } set { if (value < 0) throw new ArgumentOutOfRangeException("SpeedLimit"); _speed_limit = value; } }
        /// <summary>
        /// 下载器附加数据
        /// </summary>
        public object Tag { get; set; }
        /// <summary>
        /// 文件大小
        /// </summary>
        public long Size { get { return (long)_data.Size; } }
        #endregion

        #region Event handler
        public event EventHandler TaskStarted, TaskFinished, TaskPaused, TaskCancelled, TaskError, DecryptStarted, DecryptFinished, PreAllocBlockStarted, PreAllocBlockFinished;
        #endregion

        private struct _temp_strcut
        {
            public Guid id;
            public int index;
        }

        #region callbacks
        //刷新url的回调函数
        private void _main_url_refresh_callback(bool suc, string[] urls, object state)
        {
            if (suc && urls != null && urls.Length > 0)
                lock (_url_lock)
                {
                    //成功，并且url数大于0时，覆盖原url，将url的有效时长设为半个小时
                    _urls = urls;
                    _url_expire_time = DateTime.Now.AddHours(0.5);
                }
            else
            {
                //失败，增加url的时长为30s（避免多次触发url刷新），然后在3s后重试，增加重试的失败次数
                _url_expire_time = DateTime.Now.AddSeconds(30);
                Thread.Sleep(3000);
                _url_fail_to_fetch_count++;
                if (_url_fail_to_fetch_count < _MAX_URL_FAIL_COUNT && (_download_thread_flag & _DOWNLOAD_THREAD_FLAG_STARTED) != 0)
                    _api.GetAccount(_data.AccountID).GetLocateDownloadLinkAsync(_data.Path, _main_url_refresh_callback);
                else
                {
                    //重试失败次数多于最大阈值时，增加error的标志，并且暂停该任务（跨线程暂停）
                    if (_url_fail_to_fetch_count >= _MAX_URL_FAIL_COUNT)
                    {
                        lock (_thread_flag_lock)
                            _download_thread_flag |= _DOWNLOAD_THREAD_FLAG_ERROR;
                        Pause();
                        try { TaskError?.Invoke(this, new EventArgs()); }
                        catch { }
                    }
                }
            }
        }
        //RAM stream -> File stream
        private void _monitor_thread_callback()
        {
            //设置线程启动相关变量
            _monitor_thread_created.Set();
            _start_time = DateTime.Now;
            //管理任务状态
            bool pause_to_start = false;
            lock (_thread_flag_lock)
            {
                pause_to_start = ((_download_thread_flag & 0xffffff) & (_DOWNLOAD_THREAD_FLAG_PAUSED | _DOWNLOAD_THREAD_FLAG_PAUSE_REQUESTED)) != 0;
                _download_thread_flag = (_download_thread_flag | _DOWNLOAD_THREAD_FLAG_STARTED) & ~_DOWNLOAD_THREAD_FLAG_START_REQUESTED;
                if (pause_to_start)
                    _download_thread_flag = (_download_thread_flag & ~_DOWNLOAD_THREAD_FLAG_PAUSED);
            }
            //缓存最大线程数，避免中途更改
            var max_thread = _max_thread;

            //预分配空间（todo：改为set-length，提高效率）
            _downloaded_size = 0;
            try { PreAllocBlockStarted?.Invoke(this, new EventArgs()); } catch { }
            if (_file_stream.Length != (long)_data.Size)
            {
                if (_file_stream.Length > (long)_data.Size)
                    _file_stream.SetLength((long)_data.Size); //文件长度大于预分配长度，直接截断
                else
                {
                    //从当前长度开始按0填充
                    _file_stream.Seek(0, SeekOrigin.End);
                    _downloaded_size = _file_stream.Length;
                    var blank_buffer = new byte[_MIN_IO_FLUSH_DATA_LENGTH];
                    long write_length = 0;
                    do
                    {
                        write_length = (long)_data.Size - _file_stream.Length;
                        int len = (int)Math.Min(_MIN_IO_FLUSH_DATA_LENGTH, write_length);
                        _file_stream.Write(blank_buffer, 0, len);
                        _downloaded_size += len;
                    } while (write_length > 0 && ((_download_thread_flag & 0xffffff) & (_DOWNLOAD_THREAD_FLAG_PAUSE_REQUESTED | _DOWNLOAD_THREAD_FLAG_STOP_REQUESTED)) == 0);
                }
                _file_stream.Seek(0, SeekOrigin.Begin);
            }
            try { PreAllocBlockFinished?.Invoke(this, new EventArgs()); } catch { }
            _downloaded_size = 0;

            //分配多线程的变量数组
            _guid_list = new Guid[max_thread];
            _last_receive = new DateTime[max_thread];
            _buffer_stream = new QueueStream[max_thread];
            _thread_data_lock = new object[max_thread];
            _request = new NetStream[max_thread];
            _position = new ulong[max_thread];
            _io_position = new long[max_thread];
            for (int i = 0; i < max_thread; i++)
            {
                _buffer_stream[i] = new QueueStream();
                _thread_data_lock[i] = new object();
                _request[i] = new NetStream();
                _request[i].CookieKey = _cookie_key;
            }

            var buffer = new byte[_MIN_IO_FLUSH_DATA_LENGTH];
            var next_loop_time = DateTime.Now.AddSeconds(1);
            //监控循环
            #region monitor loop
            while (true)
            {

                //当url的有效时间已过时，刷新url
                if (_url_expire_time < DateTime.Now)
                {
                    _url_expire_time = _url_expire_time.AddSeconds(30);
                    _url_fail_to_fetch_count = 0;
                    _api.GetAccount(_data.AccountID).GetLocateDownloadLinkAsync(_data.Path, _main_url_refresh_callback);
                }

                //管理任务标识
                #region thread flag handling
                bool ispause = false, iscancel = false;

                if (((_download_thread_flag & 0xffffff) & _DOWNLOAD_THREAD_FLAG_PAUSE_REQUESTED) != 0)
                    ispause = true;
                if (((_download_thread_flag & 0xffffff) & _DOWNLOAD_THREAD_FLAG_STOP_REQUESTED) != 0)
                    iscancel = true;

                if (ispause)
                {
                    //pause event
                    for (int i = 0; i < _request.Length; i++)
                    {
                        lock (_thread_data_lock[i])
                        {
                            _request[i].Close();
                            if (_guid_list[i] != Guid.Empty)
                            {
                                _dispatcher.ReleaseTask(_guid_list[i]);
                                _guid_list[i] = Guid.Empty;
                            }
                            //flushing buffer stream
                            if ((_guid_list[i] == Guid.Empty && _buffer_stream[i].Length > 0) || (_buffer_stream[i].Length > _MIN_IO_FLUSH_DATA_LENGTH))
                            {
                                _file_stream.Seek(_io_position[i], SeekOrigin.Begin);
                                while ((_guid_list[i] == Guid.Empty && _buffer_stream[i].Length > 0) || (_buffer_stream[i].Length > _MIN_IO_FLUSH_DATA_LENGTH))
                                {
                                    int rc = _buffer_stream[i].Read(buffer, 0, _MIN_IO_FLUSH_DATA_LENGTH);
                                    _file_stream.Write(buffer, 0, rc);
                                    _io_position[i] += rc;
                                }
                            }
                        }

                        _file_stream.Flush();
                    }
                    _request = null;
                    _urls = null;
                    _guid_list = null;
                    _last_receive = null;
                    _position = null;
                    _thread_data_lock = null;
                    _monitor_thread = null;
                    _end_time = DateTime.Now;
                    _download_thread_flag = (_download_thread_flag | _DOWNLOAD_THREAD_FLAG_PAUSED) & ~(_DOWNLOAD_THREAD_FLAG_PAUSE_REQUESTED | _DOWNLOAD_THREAD_FLAG_STARTED);
                    return;
                }
                if (iscancel)
                {
                    //cancel event
                    _file_stream.Close();
                    for (int i = 0; i < _request.Length; i++)
                    {
                        lock (_thread_data_lock[i])
                        {
                            _request[i].Close();
                            if (_guid_list[i] != Guid.Empty)
                            {
                                _dispatcher.ReleaseTask(_guid_list[i]);
                                _guid_list[i] = Guid.Empty;
                            }
                        }

                    }
                    _request = null;
                    _urls = null;
                    _guid_list = null;
                    _last_receive = null;
                    _position = null;
                    _thread_data_lock = null;
                    _file_stream.Dispose();
                    _file_stream = null;
                    _monitor_thread = null;
                    _end_time = DateTime.Now;
                    _download_thread_flag = (_download_thread_flag | _DOWNLOAD_THREAD_FLAG_STOPPED) & ~(_DOWNLOAD_THREAD_FLAG_STOP_REQUESTED | _DOWNLOAD_THREAD_FLAG_STARTED);
                    return;
                }
                #endregion

                //url合理检测
                if (_urls == null || _urls.Length == 0)
                {
                    Thread.Sleep(100);
                    continue;
                }

                int started_tasks = 0;
                for (int i = 0; i < _request.Length; i++)
                {
                    lock (_thread_data_lock[i])
                    {
                        //将内存的缓存数据流写入到硬盘中
                        if ((_guid_list[i] == Guid.Empty && _buffer_stream[i].Length > 0) || (_buffer_stream[i].Length > _MIN_IO_FLUSH_DATA_LENGTH))
                        {
                            _file_stream.Seek(_io_position[i], SeekOrigin.Begin);
                            while ((_guid_list[i] == Guid.Empty && _buffer_stream[i].Length > 0) || (_buffer_stream[i].Length > _MIN_IO_FLUSH_DATA_LENGTH))
                            {
                                int rc = _buffer_stream[i].Read(buffer, 0, _MIN_IO_FLUSH_DATA_LENGTH);
                                _file_stream.Write(buffer, 0, rc);
                                _io_position[i] += rc;
                            }
                        }

                        //检测任务分配状态，自动开始新的分段下载
                        if (_guid_list[i] == Guid.Empty && (DateTime.Now - _last_receive[i]).TotalSeconds > 3.0)
                        {
                            if (started_tasks < _PARALLEL_START_REQUEST_COUNT)
                            {
                                _guid_list[i] = _dispatcher.AllocateNewTask(out _position[i]);
                                if (_guid_list[i] != Guid.Empty)
                                {
                                    _last_receive[i] = DateTime.Now;
                                    try
                                    {
                                        _request[i].HttpGetAsync(_urls[i % _urls.Length], _data_transfer_callback, new _temp_strcut { id = _guid_list[i], index = i }, range: (long)_position[i]);
                                        _io_position[i] = (long)_position[i];
                                    }
                                    catch { }

                                    started_tasks++;
                                }
                            }
                        }

                        //自动中断超时的请求
                        else if (_guid_list[i] != Guid.Empty && (DateTime.Now - _last_receive[i]).TotalSeconds > 35.0)
                        {
                            _request[i].Close();
                            _request[i] = new NetStream();
                            _dispatcher.ReleaseTask(_guid_list[i]);
                            _guid_list[i] = Guid.Empty;
                        }
                    }
                }

                _current_bytes = 0;

                //更新速度
                var cur_len = _dispatcher.CompletedLength;
                _last_5s_length.RemoveFirst();
                _last_5s_length.AddLast(cur_len);

                _average_speed_total = cur_len / (DateTime.Now - _start_time).TotalSeconds;
                _average_speed_5s = (_last_5s_length.Last.Value - _last_5s_length.First.Value) / 5.0;
                _downloaded_size = (long)cur_len;
                Tracer.GlobalTracer.TraceInfo("Downloading: " + cur_len + "/" + _data.Size + " [" + (_average_speed_5s / 1024).ToString("0.00") + "KB/s]");

                if (cur_len == _data.Size) break;
                //时钟控制（1s）
                var ts = (next_loop_time - DateTime.Now).TotalMilliseconds;
                next_loop_time = next_loop_time.AddSeconds(1);
                if (ts >= 1.0) Thread.Sleep((int)ts);
            }
            #endregion

            //下载完成
            _file_stream.Close();
            for (int i = 0; i < _request.Length; i++)
            {
                lock (_thread_data_lock[i])
                {
                    _request[i].Close();
                }
            }
            _request = null;
            _urls = null;
            _guid_list = null;
            _last_receive = null;
            _position = null;
            _thread_data_lock = null;
            _file_stream.Dispose();
            _file_stream = null;
            _monitor_thread = null;
            _end_time = DateTime.Now;

            //文件解密
            if (_data.Path.EndsWith(".bcsd"))
            {
                _download_thread_flag |= _DOWNLOAD_THREAD_FLAG_DECRYPTING;
                try { DecryptStarted?.Invoke(this, new EventArgs()); } catch { }
                _decrypt_file();
                _download_thread_flag = _download_thread_flag & ~_DOWNLOAD_THREAD_FLAG_DECRYPTING;
                try { DecryptFinished?.Invoke(this, new EventArgs()); } catch { }
            }
            _download_thread_flag = (_download_thread_flag | _DOWNLOAD_THREAD_FLAG_FINISHED) & ~_DOWNLOAD_THREAD_FLAG_STARTED;

            try { TaskFinished?.Invoke(this, new EventArgs()); }
            catch { }
        }

        //数据传输的回调函数
        private void _data_transfer_callback(NetStream ns, object state)
        {
            const int buffer_size = 2048;
            var buffer = new byte[buffer_size];
            int index = ((_temp_strcut)state).index;
            var id = ((_temp_strcut)state).id;
            //debug
            try
            {
                //http状态检查
                //空引用
                if (ns == null)
                    throw new InvalidDataException("NetStream closed and set to null");
                //guid不匹配（通常是该任务已经触发主动超时后更改了新的guid值）
                lock (_thread_data_lock[index])
                    if (id != _guid_list[index])
                        throw new InvalidDataException("Guid mismatched, ignored");
                //空http响应
                if (ns.HTTP_Response == null) return;
                //状态码不匹配
                if (ns.HTTP_Response.StatusCode != System.Net.HttpStatusCode.OK && ns.HTTP_Response.StatusCode != System.Net.HttpStatusCode.PartialContent)
                    throw new InvalidDataException("Status code check failed");
                //数据长度不匹配
                if (ns.HTTP_Response.ContentLength + (long)_position[index] != (long)_data.Size)
                    throw new InvalidDataException("Content-Length mismatched, ignored");
                var response_url = ns.HTTP_Response.ResponseUri.ToString();
                lock (_url_lock)
                    if (!string.IsNullOrEmpty(response_url)) _urls[index % _urls.Length] = response_url;
                var istream = ns.ResponseStream;
                var ostream = _buffer_stream[index];
                var bytes_read = 0;
                ulong total_read = 0;
                do
                {
                    //状态检查，非下载状态时退出接收循环
                    if (((_download_thread_flag & 0xffffff) & ~(_DOWNLOAD_THREAD_FLAG_STARTED | _DOWNLOAD_THREAD_FLAG_START_REQUESTED)) != 0)
                    {
                        break;
                    }

                    //循环读取数据
                    var cur_bytes = Interlocked.Read(ref _current_bytes);
                    if (_speed_limit == 0 || cur_bytes < _speed_limit)
                    {
                        bytes_read = istream.Read(buffer, 0, buffer_size);
                        Interlocked.Add(ref _current_bytes, bytes_read);
                    }
                    else
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    //写入数据到内存
                    lock (_thread_data_lock[index])
                    {
                        if (_guid_list[index] == Guid.Empty || _guid_list[index] != id)
                            throw new InvalidDataException("Guid mismatch!");
                        ostream.Write(buffer, 0, bytes_read);
                        _last_receive[index] = DateTime.Now;
                    }
                    total_read += (ulong)bytes_read;
                } while (_dispatcher.UpdateTaskSituation(_guid_list[index], _position[index] + total_read) && bytes_read > 0);
            }
            catch (InvalidDataException ex)
            {
                //Invalid GUID or StatusCode
                if (_enable_error_tracing)
                    Tracer.GlobalTracer.TraceError(ex);
            }
            catch (IOException)
            {
                //IO exception in SOCKET connection
            }
            catch (System.Net.WebException)
            {
                //HTTP Exception (timed out)
            }
            catch (Exception ex)
            {
                //general exception
                Tracer.GlobalTracer.TraceError(ex);
            }
            finally
            {
                lock (_thread_data_lock[index])
                {
                    _dispatcher.ReleaseTask(_guid_list[index]);
                    _guid_list[index] = Guid.Empty;
                    _request[index].Close();
                }
            }
        }
        private void _decrypt_file_status_callback(string inputFile, string outputFile, long current, long length)
        {
            _downloaded_size = current;
            _data.Size = (ulong)length;
        }
        private void _decrypt_file()
        {
            //未实例化密钥管理类
            if (_key_manager == null)
                return;
            try
            {
                if (File.Exists(_output_path + ".decrypting"))
                    File.Delete(_output_path + ".decrypting"); //删除已有的冲突文件

                //重命名
                if (_output_path.EndsWith(".bcsd"))
                {
                    //xxxxx.bcsd -> xxxxx.decrypting
                    _output_path = _output_path.Substring(0, _output_path.Length - 5);
                    File.Move(_output_path + ".bcsd", _output_path + ".decrypting");
                }
                else
                    File.Move(_output_path, _output_path + ".decrypting"); //xxxxx.xx -> xxxxx.xx.decrypting

                var fs = new FileStream(_output_path + ".decrypting", FileMode.Open, FileAccess.Read, FileShare.None);
                var identifier = fs.ReadByte(); //读取文件首字节判断加密类型
                fs.Close();

                if (identifier == FileEncrypt.FLG_DYNAMIC_KEY && _key_manager.HasRsaKey)
                {
                    try
                    {
                        //解密文件并删除加密文件
                        FileEncrypt.DecryptFile(_output_path + ".decrypting", _output_path, _key_manager.RSAPrivateKey);
                        File.Delete(_output_path + ".decrypting");
                    }
                    catch
                    {
                        //出错后取消解密，将文件重新命名
                        File.Move(_output_path + ".decrypting", _output_path);
                    }
                }
                else if (identifier == FileEncrypt.FLG_STATIC_KEY && _key_manager.HasAesKey)
                {
                    try
                    {
                        FileEncrypt.DecryptFile(_output_path + ".decrypting", _output_path, _key_manager.AESKey, _key_manager.AESIV);
                        File.Delete(_output_path + ".decrypting");
                    }
                    catch
                    {
                        File.Move(_output_path + ".decrypting", _output_path);
                    }
                }
                else
                {
                    //加密类型不匹配，重新命名
                    File.Move(_output_path + ".decrypting", _output_path);
                }
            }
            catch (Exception ex)
            {
                Tracer.GlobalTracer.TraceError(ex);
            }
        }
        #endregion

        public void Dispose()
        {
            if (_monitor_thread != null)
            {
                Cancel();
            }

            if (_file_stream != null)
            {
                _file_stream.Close();
                _file_stream.Dispose();
                _file_stream = null;
            }
        }
    }
}
