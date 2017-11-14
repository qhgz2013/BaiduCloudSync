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
    public class Downloader : IDisposable
    {
        //错误输出
        private const bool _enable_error_tracing = true;
        //默认下载线程
        public const int DEFAULT_MAX_THREAD = 96;
        //每个间隔的请求数
        private const int _PARALLEL_START_REQUEST_COUNT = 2;
        //每个缓存段进行IO写入时需要达到的数据量
        private const int _MIN_IO_FLUSH_DATA_LENGTH = 32768; //32KB

        //private const int _buffer_pool_size = 0x80000; //8MB buffering size

        //necessary vars
        //api请求，用于获得下载链接
        private RemoteFileCacher _api;
        //下载文件的数据
        private ObjectMetadata _data;
        //保存地址
        private string _output_path;
        private string _cookie_key;

        //线程标识
        private volatile int _download_thread_flag;
        private const int _DOWNLOAD_THREAD_FLAG_READY = int.MinValue;
        private const int _DOWNLOAD_THREAD_FLAG_PAUSE_REQUESTED = 1;
        private const int _DOWNLOAD_THREAD_FLAG_PAUSED = 2;
        private const int _DOWNLOAD_THREAD_FLAG_STOP_REQUESTED = 4;
        private const int _DOWNLOAD_THREAD_FLAG_STOPPED = 8;
        private const int _DOWNLOAD_THREAD_FLAG_START_REQUESTED = 16;
        private const int _DOWNLOAD_THREAD_FLAG_STARTED = 32;
        private const int _DOWNLOAD_THREAD_FLAG_FINISHED = 64;

        //单任务最大连接数
        private int _max_thread;

        private TaskDispatcher _dispatcher; //分段分配
        private string[] _urls; //url

        private Guid[] _guid_list; //分段的id
        private DateTime[] _last_receive; //分段最后接收数据的时间，用于主动模式下的timeout
        private QueueStream[] _buffer_stream; //缓存数据流
        private NetStream[] _request; //分段的http请求
        private ManualResetEventSlim[] _request_cancel; //每个分段的线程是否已经退出
        private ulong[] _position; //分段的位置
        private long[] _io_position; //分段的写入位置 (seek _io_position->write data)
        //private long _test_io_write;
        private FileStream _file_stream; //本地文件数据流
        private Thread _monitor_thread; //后台监控线程
        private ManualResetEventSlim _monitor_thread_created; //是否已创建监控线程
        //status
        private DateTime _start_time; //任务开始时间
        private double _average_speed_total; //全局平均速度
        private double _average_speed_5s; //5秒内的平均速度
        private LinkedList<ulong> _last_5s_length;
        private long _downloaded_size; //已下载的字节数
        //private byte[] _test;
        private long[] _remote_io_size;
        private long[] _local_io_size;
        //private Tracer[] _test_tracer;
        private DateTime _url_expire_time;

        //locks
        private object _external_lock;
        private object[] _thread_data_lock;
        private object _url_lock;
        private object _thread_flag_lock;
        public Downloader(RemoteFileCacher pcs, ObjectMetadata remote_file, string local_file, int max_thread = DEFAULT_MAX_THREAD)
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
            _dispatcher = new TaskDispatcher(_data.Size);
            _download_thread_flag = _DOWNLOAD_THREAD_FLAG_READY;
            _file_stream = new FileStream(_output_path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            _monitor_thread_created = new ManualResetEventSlim();

            _last_5s_length = new LinkedList<ulong>();
            for (int i = 0; i < 5; i++)
                _last_5s_length.AddFirst(0);
            _cookie_key = _api.GetAccount(_data.AccountID).Auth.CookieIdentifier;
        }

        ~Downloader()
        {
            Dispose();
        }

        #region public func
        public void Start()
        {
            lock (_external_lock)
            {
                lock (_thread_flag_lock)
                {
                    if ((_download_thread_flag & (_DOWNLOAD_THREAD_FLAG_READY | _DOWNLOAD_THREAD_FLAG_PAUSED)) != 0)
                    {
                        Tracer.GlobalTracer.TraceInfo("---STARTED---");
                        _download_thread_flag = (_download_thread_flag | _DOWNLOAD_THREAD_FLAG_START_REQUESTED) & ~_DOWNLOAD_THREAD_FLAG_READY;
                        _api.GetAccount(_data.AccountID).GetLocateDownloadLinkAsync(_data.Path, _main_url_request_callback);
                    }
                }
            }
        }

        public void Pause()
        {
            lock (_external_lock)
            {
                lock (_thread_flag_lock)
                {
                    if ((_download_thread_flag & (_DOWNLOAD_THREAD_FLAG_START_REQUESTED | _DOWNLOAD_THREAD_FLAG_STARTED)) != 0)
                    {
                        Tracer.GlobalTracer.TraceInfo("---PAUSED---");
                        _download_thread_flag |= _DOWNLOAD_THREAD_FLAG_PAUSE_REQUESTED;
                        _monitor_thread_created.Wait();
                        _monitor_thread.Join();
                        _monitor_thread_created.Reset();
                    }
                }
            }
        }

        public void Cancel()
        {
            lock (_external_lock)
            {
                lock (_thread_flag_lock)
                {
                    Tracer.GlobalTracer.TraceInfo("---CANCELLED---");
                    if (_download_thread_flag == _DOWNLOAD_THREAD_FLAG_READY)
                    {
                        _download_thread_flag |= _DOWNLOAD_THREAD_FLAG_STOPPED;
                    }
                    else if ((_download_thread_flag & (_DOWNLOAD_THREAD_FLAG_PAUSED | _DOWNLOAD_THREAD_FLAG_PAUSE_REQUESTED | _DOWNLOAD_THREAD_FLAG_STARTED | _DOWNLOAD_THREAD_FLAG_START_REQUESTED)) != 0)
                    {
                        _download_thread_flag |= _DOWNLOAD_THREAD_FLAG_STOP_REQUESTED;
                        _monitor_thread_created.Wait();
                        _monitor_thread.Join();
                        _monitor_thread_created.Reset();
                    }
                }
            }
        }
        #endregion

        #region public properties
        public ObjectMetadata RemoteFile { get { return _data; } }
        public DateTime StartTime { get { return _start_time; } }
        public TimeSpan EllapsedTime { get { return DateTime.Now - _start_time; } }
        public double AverageSpeedTotal { get { return _average_speed_total; } }
        public double AverageSpeed5s { get { return _average_speed_5s; } }
        public long DownloadedSize { get { return _downloaded_size; } }
        public int MaxThread { get { return _max_thread; } set { _max_thread = value; } }
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
            FINISHED = _DOWNLOAD_THREAD_FLAG_FINISHED
        }

        public State TaskState { get { return (State)_download_thread_flag; } }
        #endregion
        private struct _temp_strcut
        {
            public Guid id;
            public int index;
        }

        #region callbacks
        //PCS API -> Locate url
        private void _main_url_request_callback(bool suc, string[] urls, object state)
        {
            _main_url_refresh_callback(suc, urls, state);
            _monitor_thread = new Thread(_monitor_thread_callback);
            _monitor_thread.Name = "Download Monitor";
            _monitor_thread.IsBackground = false;
            _monitor_thread.Start();

        }

        private void _main_url_refresh_callback(bool suc, string[] urls, object state)
        {
            if (suc && urls != null && urls.Length > 0)
                lock (_url_lock)
                {
                    _urls = urls;
                    _url_expire_time = DateTime.Now.AddHours(0.5);
                }
            else
            {
                Thread.Sleep(1000);
                _api.GetAccount(_data.AccountID).GetLocateDownloadLinkAsync(_data.Path, _main_url_refresh_callback);
            }
        }
        //RAM stream -> File stream
        private void _monitor_thread_callback()
        {
            _monitor_thread_created.Set();
            bool pause_to_start = false, stop_to_start = false;
            lock (_thread_flag_lock)
            {
                pause_to_start = (_download_thread_flag & (_DOWNLOAD_THREAD_FLAG_PAUSED | _DOWNLOAD_THREAD_FLAG_PAUSE_REQUESTED)) != 0;
                stop_to_start = (_download_thread_flag & (_DOWNLOAD_THREAD_FLAG_STOPPED | _DOWNLOAD_THREAD_FLAG_STOP_REQUESTED)) != 0;
                if (stop_to_start) return; //could not start from stopped state
                _download_thread_flag = (_download_thread_flag | _DOWNLOAD_THREAD_FLAG_STARTED) & ~_DOWNLOAD_THREAD_FLAG_START_REQUESTED;
                if (pause_to_start)
                    _download_thread_flag = (_download_thread_flag & ~_DOWNLOAD_THREAD_FLAG_PAUSED);
            }
            _start_time = DateTime.Now;
            var max_thread = _max_thread;

            //pre allocating file
            if (_file_stream.Length != (long)_data.Size)
            {
                if (_file_stream.Length > (long)_data.Size)
                    _file_stream.SetLength((long)_data.Size);
                else
                {
                    _file_stream.Seek(0, SeekOrigin.End);
                    var blank_buffer = new byte[_MIN_IO_FLUSH_DATA_LENGTH];
                    long write_length = 0;
                    do
                    {
                        write_length = (long)_data.Size - _file_stream.Length;
                        int len = (int)Math.Min(_MIN_IO_FLUSH_DATA_LENGTH, write_length);
                        _file_stream.Write(blank_buffer, 0, len);
                    } while (write_length > 0 && (_download_thread_flag & (_DOWNLOAD_THREAD_FLAG_PAUSE_REQUESTED | _DOWNLOAD_THREAD_FLAG_STOP_REQUESTED)) == 0);
                }
                _file_stream.Seek(0, SeekOrigin.Begin);
            }
            //var test_data = new MemoryStream((int)_data.Size);
            var test_data = new FileStream("D:\\allocated_block", FileMode.OpenOrCreate, FileAccess.ReadWrite);
            test_data.SetLength((long)_data.Size);
            test_data.Seek(-1, SeekOrigin.End);
            test_data.WriteByte(0);
            test_data.Seek(0, SeekOrigin.Begin);

            //allocating memory
            _guid_list = new Guid[max_thread];
            _last_receive = new DateTime[max_thread];
            _buffer_stream = new QueueStream[max_thread];
            _thread_data_lock = new object[max_thread];
            _request = new NetStream[max_thread];
            _position = new ulong[max_thread];
            _request_cancel = new ManualResetEventSlim[max_thread];
            _io_position = new long[max_thread];
            _remote_io_size = new long[max_thread];
            _local_io_size = new long[max_thread];
            for (int i = 0; i < max_thread; i++)
            {
                _buffer_stream[i] = new QueueStream();
                _request_cancel[i] = new ManualResetEventSlim();
                _thread_data_lock[i] = new object();
                _request[i] = new NetStream();
                _request[i].CookieKey = _cookie_key;
                //_test_tracer[i] = new Tracer(".cache/" + i + ".log", false);
            }

            var buffer = new byte[_MIN_IO_FLUSH_DATA_LENGTH];
            var fill_buffer = new byte[max_thread][];
            for (int i = 0; i < max_thread; i++)
            {
                fill_buffer[i] = new byte[_MIN_IO_FLUSH_DATA_LENGTH];
                for (int j = 0; j < _MIN_IO_FLUSH_DATA_LENGTH; j++)
                {
                    fill_buffer[i][j] = (byte)(i + 1);
                }
            }
            var next_loop_time = DateTime.Now.AddSeconds(1);
            //monitor loop
            #region monitor loop
            while (true)
            {

                //url update
                if (_url_expire_time < DateTime.Now)
                {
                    _url_expire_time = _url_expire_time.AddSeconds(30);
                    _api.GetAccount(_data.AccountID).GetLocateDownloadLinkAsync(_data.Path, _main_url_refresh_callback);
                }

                #region thread flag handling
                bool ispause = false, iscancel = false;
                //lock (_thread_flag_lock)
                //{

                if ((_download_thread_flag & _DOWNLOAD_THREAD_FLAG_PAUSE_REQUESTED) != 0)
                    ispause = true;
                if ((_download_thread_flag & _DOWNLOAD_THREAD_FLAG_STOP_REQUESTED) != 0)
                    iscancel = true;
                //}
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
                            //_test_tracer[i].TraceInfo("Local: guid check: " + ((_guid_list[i] == Guid.Empty) ? "false" : "true"));
                            if ((_guid_list[i] == Guid.Empty && _buffer_stream[i].Length > 0) || (_buffer_stream[i].Length > _MIN_IO_FLUSH_DATA_LENGTH))
                            {
                                _file_stream.Seek(_io_position[i], SeekOrigin.Begin);
                                test_data.Seek(_io_position[i], SeekOrigin.Begin);
                                while ((_guid_list[i] == Guid.Empty && _buffer_stream[i].Length > 0) || (_buffer_stream[i].Length > _MIN_IO_FLUSH_DATA_LENGTH))
                                {
                                    int rc = _buffer_stream[i].Read(buffer, 0, _MIN_IO_FLUSH_DATA_LENGTH);
                                    _file_stream.Write(buffer, 0, rc);
                                    test_data.Write(fill_buffer[i], 0, rc);
                                    _io_position[i] += rc;
                                    _local_io_size[i] += rc;
                                }
                            }
                        }

                        _file_stream.Flush();
                        test_data.Flush();
                        //_request_cancel[i].Wait(); //Sync mode
                    }
                    _request = null;
                    _request_cancel = null;
                    _urls = null;
                    _guid_list = null;
                    _last_receive = null;
                    _position = null;
                    _thread_data_lock = null;
                    _monitor_thread = null;
                    _download_thread_flag = (_download_thread_flag | _DOWNLOAD_THREAD_FLAG_PAUSED) & ~_DOWNLOAD_THREAD_FLAG_PAUSE_REQUESTED;
                    test_data.Close();
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

                        //_request_cancel[i].Wait(); //Sync mode
                    }
                    _request = null;
                    _request_cancel = null;
                    _urls = null;
                    _guid_list = null;
                    _last_receive = null;
                    _position = null;
                    _thread_data_lock = null;
                    _file_stream.Dispose();
                    _file_stream = null;
                    _monitor_thread = null;
                    test_data.Close();
                    _download_thread_flag = (_download_thread_flag | _DOWNLOAD_THREAD_FLAG_STOPPED) & ~_DOWNLOAD_THREAD_FLAG_STOP_REQUESTED;
                    return;
                }
                #endregion


                int started_tasks = 0;
                for (int i = 0; i < _request.Length; i++)
                {
                    lock (_thread_data_lock[i])
                    {
                        //flushing buffer stream
                        //_test_tracer[i].TraceInfo("Local: guid check: " + ((_guid_list[i] == Guid.Empty) ? "false" : "true"));
                        if ((_guid_list[i] == Guid.Empty && _buffer_stream[i].Length > 0) || (_buffer_stream[i].Length > _MIN_IO_FLUSH_DATA_LENGTH))
                        {
                            _file_stream.Seek(_io_position[i], SeekOrigin.Begin);
                            test_data.Seek(_io_position[i], SeekOrigin.Begin);
                            while ((_guid_list[i] == Guid.Empty && _buffer_stream[i].Length > 0) || (_buffer_stream[i].Length > _MIN_IO_FLUSH_DATA_LENGTH))
                            {
                                int rc = _buffer_stream[i].Read(buffer, 0, _MIN_IO_FLUSH_DATA_LENGTH);
                                _file_stream.Write(buffer, 0, rc);
                                test_data.Write(fill_buffer[i], 0, rc);
                                _io_position[i] += rc;
                                _local_io_size[i] += rc;
                            }
                        }

                        //auto starting new task
                        if (_guid_list[i] == Guid.Empty)
                        {
                            if (_remote_io_size[i] != _local_io_size[i])
                            {

                            }
                        }

                        if (_guid_list[i] == Guid.Empty)
                        {
                            if (started_tasks < _PARALLEL_START_REQUEST_COUNT)
                            {
                                _guid_list[i] = _dispatcher.AllocateNewTask(out _position[i]);
                                if (_guid_list[i] != Guid.Empty)
                                {
                                    //_test[i] = 1;
                                    _remote_io_size[i] = 0;
                                    //_test_tracer[i].TraceInfo("Remote: 0");
                                    _local_io_size[i] = 0;
                                    //_test_tracer[i].TraceInfo("Local: 0");
                                    _request_cancel[i].Reset();
                                    Tracer.GlobalTracer.TraceInfo("data request begin: #" + i);
                                    _last_receive[i] = DateTime.Now;
                                    try { _request[i].HttpGetAsync(_urls[i % _urls.Length], _data_transfer_callback, new _temp_strcut { id = _guid_list[i], index = i }, range: (long)_position[i]); }
                                    catch { }
                                    _io_position[i] = (long)_position[i];
                                    Tracer.GlobalTracer.TraceInfo("seeking io position #" + i + " to " + _io_position[i]);
                                    started_tasks++;
                                }
                            }
                        }
                        //auto dropping timeout requests
                        else if ((DateTime.Now - _last_receive[i]).TotalSeconds > 35.0)
                        {
                            _request[i].Close();
                            //_test[i] = 0;
                            Tracer.GlobalTracer.TraceInfo("data request cancelled (request timed out): #" + i);
                            _request[i] = new NetStream();
                            _dispatcher.ReleaseTask(_guid_list[i]);
                            _guid_list[i] = Guid.Empty;
                        }
                    }
                }



                //speed update
                var cur_len = _dispatcher.CompletedLength;
                _last_5s_length.RemoveFirst();
                _last_5s_length.AddLast(cur_len);

                _average_speed_total = cur_len / (DateTime.Now - _start_time).TotalSeconds;
                _average_speed_5s = (_last_5s_length.Last.Value - _last_5s_length.First.Value) / 5.0;
                _downloaded_size = (long)cur_len;
                Tracer.GlobalTracer.TraceInfo("Downloading: " + cur_len + "/" + _data.Size + " [" + (_average_speed_5s / 1024).ToString("0.00") + "KB/s]");

                if (_last_5s_length.Last.Value == _data.Size) break;
                //timer interval
                var ts = (next_loop_time - DateTime.Now).TotalMilliseconds;
                next_loop_time = next_loop_time.AddSeconds(1);
                if (ts >= 1.0) Thread.Sleep((int)ts);
            }
            #endregion

            //finished
            //test
            test_data.Position = 0;
            while (test_data.Position != test_data.Length)
            {
                var data = new byte[2048];
                var c = test_data.Read(data, 0, 2048);
                for (int i = 0; i < c; i++)
                    if (data[i] == 0)
                    {
                        //Tracer.GlobalTracer.TraceWarning("[W] Position " + test_data.Position);
                    }
            }
            _file_stream.Close();
            for (int i = 0; i < _request.Length; i++)
            {
                lock (_thread_data_lock[i])
                {
                    _request[i].Close();
                }

                //_request_cancel[i].Wait(); //Sync mode
            }
            _request = null;
            _request_cancel = null;
            _urls = null;
            _guid_list = null;
            _last_receive = null;
            _position = null;
            _thread_data_lock = null;
            _file_stream.Dispose();
            _file_stream = null;
            _monitor_thread = null;
            test_data.Close();
            _download_thread_flag = (_download_thread_flag | _DOWNLOAD_THREAD_FLAG_FINISHED) & ~_DOWNLOAD_THREAD_FLAG_STARTED;
            Tracer.GlobalTracer.TraceInfo("Download finished");
        }

        //TCP stream -> RAM stream
        private void _data_transfer_callback(NetStream ns, object state)
        {
            const int buffer_size = 2048;
            var buffer = new byte[buffer_size];
            int index = ((_temp_strcut)state).index;
            var id = ((_temp_strcut)state).id;
            //debug
            //Tracer.GlobalTracer.TraceInfo("data response begin: #" + index);
            //_test[index] = 2;
            try
            {
                lock (_thread_data_lock[index])
                    if (id != _guid_list[index])
                        throw new Exception("Guid mismatched, ignored");
                if (ns.HTTP_Response == null) return;
                if (ns.HTTP_Response.StatusCode != System.Net.HttpStatusCode.OK && ns.HTTP_Response.StatusCode != System.Net.HttpStatusCode.PartialContent)
                    throw new InvalidDataException("Status code check failed");

                var response_url = ns.HTTP_Response.ResponseUri.ToString();
                lock (_url_lock)
                    if (!string.IsNullOrEmpty(response_url)) _urls[index % _urls.Length] = response_url;
                var istream = ns.ResponseStream;
                var ostream = _buffer_stream[index];
                var bytes_read = 0;
                ulong total_read = 0;
                do
                {
                    //state check
                    if ((_download_thread_flag & ~(_DOWNLOAD_THREAD_FLAG_STARTED | _DOWNLOAD_THREAD_FLAG_START_REQUESTED)) != 0)
                    {
                        //exit
                        break;
                    }

                    //loop for reading data
                    bytes_read = istream.Read(buffer, 0, buffer_size);

                    //overwriting data, no exception allowed here!
                    lock (_thread_data_lock[index])
                    {
                        if (_guid_list[index] == Guid.Empty || _guid_list[index] != id)
                            throw new Exception("Guid mismatch!");
                        _remote_io_size[index] += bytes_read;
                        //_test_tracer[index].TraceInfo("Remote: " + _remote_io_size[index]);
                        ostream.Write(buffer, 0, bytes_read);
                        _last_receive[index] = DateTime.Now;
                    }
                    total_read += (ulong)bytes_read;
                } while (_dispatcher.UpdateTaskSituation(_guid_list[index], _position[index] + total_read) && bytes_read > 0);
            }
            catch (Exception ex)
            {

                Tracer.GlobalTracer.TraceError("ERROR #" + index + "\r\n" + ex);
            }
            finally
            {
                lock (_thread_data_lock[index])
                {
                    _dispatcher.ReleaseTask(_guid_list[index]);
                    _guid_list[index] = Guid.Empty;
                    _request[index].Close();
                    //_test[index] = 0;
                }
                _request_cancel[index].Set();
                //Tracer.GlobalTracer.TraceInfo("data response end: #" + index);
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
