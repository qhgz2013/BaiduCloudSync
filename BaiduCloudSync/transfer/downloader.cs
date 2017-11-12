using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GlobalUtil;
using GlobalUtil.NetUtils;
using static BaiduCloudSync.BaiduPCS;
using System.IO;

namespace BaiduCloudSync
{
    public class Downloader : IDisposable
    {
        private const bool _enable_error_tracing = true;
        public const int DEFAULT_MAX_THREAD = 96;

        //private const int _buffer_pool_size = 0x80000; //8MB buffering size

        //necessary vars
        private RemoteFileCacher _api;
        private ObjectMetadata _data;
        private string _output_path;

        //线程标识
        private volatile int _download_thread_flag;
        private const int _DOWNLOAD_THREAD_FLAG_READY = 0;
        private const int _DOWNLOAD_THREAD_FLAG_PAUSE_REQUESTED = 1;
        private const int _DOWNLOAD_THREAD_FLAG_PAUSED = 2;
        private const int _DOWNLOAD_THREAD_FLAG_STOP_REQUESTED = 4;
        private const int _DOWNLOAD_THREAD_FLAG_STOPPED = 8;
        private const int _DOWNLOAD_THREAD_FLAG_START_REQUESTED = 16;
        private const int _DOWNLOAD_THREAD_FLAG_STARTED = 32;

        //单任务最大连接数
        private int _max_thread;

        private TaskDispatcher _dispatcher;
        private string[] _urls;

        private Guid[] _guid_list;
        private DateTime[] _last_receive;
        private MemoryStream[] _buffer_stream;
        private NetStream[] _request;
        private ulong[] _position;
        private FileStream _file_stream;
        //status
        private DateTime _start_time;
        private double _average_speed_total;
        private double _average_speed_5s;

        //locks
        private object _external_lock;
        private object[] _thread_data_lock;
        private object _io_stream_lock;
        private object _thread_flag_lock;
        public Downloader(RemoteFileCacher pcs, ObjectMetadata remote_file, string local_file, int max_thread = DEFAULT_MAX_THREAD)
        {
            if (pcs == null) throw new ArgumentNullException("pcs");
            if (string.IsNullOrEmpty(remote_file.Path)) throw new ArgumentNullException("remote_file.Path");
            if (remote_file.FS_ID == 0) throw new ArgumentOutOfRangeException("remote_file.FS_ID");
            if (remote_file.AccountID < 0) throw new ArgumentOutOfRangeException("remote_file.AccountID");
            if (max_thread <= 0) throw new ArgumentOutOfRangeException("max_thread");
            if (string.IsNullOrEmpty(local_file)) throw new ArgumentNullException("local_file");

            _external_lock = new object();
            _io_stream_lock = new object();
            _thread_flag_lock = new object();
            _api = pcs;
            _data = remote_file;
            _output_path = local_file;
            _max_thread = max_thread;
            _dispatcher = new TaskDispatcher(_data.Size);
            _download_thread_flag = _DOWNLOAD_THREAD_FLAG_READY;
            _file_stream = new FileStream(_output_path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);

        }

        ~Downloader()
        {
            Dispose();
        }

        public void Start()
        {
            lock (_external_lock)
            {
                lock (_thread_flag_lock)
                {
                    if ((_download_thread_flag & (_DOWNLOAD_THREAD_FLAG_READY | _DOWNLOAD_THREAD_FLAG_PAUSED)) != 0)
                    {
                        _download_thread_flag |= _DOWNLOAD_THREAD_FLAG_START_REQUESTED;
                        _api.GetAccount(_data.AccountID).GetDownloadLinkAsync(_data.FS_ID, _main_url_request_callback);
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
                        _download_thread_flag |= _DOWNLOAD_THREAD_FLAG_PAUSE_REQUESTED;
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
                    if (_download_thread_flag == _DOWNLOAD_THREAD_FLAG_READY)
                    {
                        _download_thread_flag |= _DOWNLOAD_THREAD_FLAG_STOPPED;
                    }
                    else if ((_download_thread_flag & (_DOWNLOAD_THREAD_FLAG_PAUSED | _DOWNLOAD_THREAD_FLAG_PAUSE_REQUESTED | _DOWNLOAD_THREAD_FLAG_STARTED | _DOWNLOAD_THREAD_FLAG_START_REQUESTED)) != 0)
                    {
                        _download_thread_flag |= _DOWNLOAD_THREAD_FLAG_STOP_REQUESTED;
                    }
                }
            }
        }

        //PCS API -> Locate url and RAM stream -> File stream
        private void _main_url_request_callback(bool suc, string[] urls, object state)
        {
            _download_thread_flag = (_download_thread_flag | _DOWNLOAD_THREAD_FLAG_STARTED) & ~_DOWNLOAD_THREAD_FLAG_START_REQUESTED;
            _urls = urls;

            bool ispause = false, iscancel = false;

            lock (_thread_flag_lock)
            {
                if ((_download_thread_flag & _DOWNLOAD_THREAD_FLAG_PAUSE_REQUESTED) != 0)
                {
                    _download_thread_flag = (_download_thread_flag | _DOWNLOAD_THREAD_FLAG_PAUSED) & ~_DOWNLOAD_THREAD_FLAG_PAUSE_REQUESTED;
                    ispause = true;
                }
                if ((_download_thread_flag & _DOWNLOAD_THREAD_FLAG_STOP_REQUESTED) != 0)
                {
                    _download_thread_flag = (_download_thread_flag | _DOWNLOAD_THREAD_FLAG_STOPPED) & ~_DOWNLOAD_THREAD_FLAG_STOP_REQUESTED;
                    iscancel = true;
                }
            }
            if (ispause || iscancel)
                return;

            _start_time = DateTime.Now;
            var max_thread = _max_thread;

            //allocating memory
            _guid_list = new Guid[max_thread];
            _last_receive = new DateTime[max_thread];
            _buffer_stream = new MemoryStream[max_thread];
            _thread_data_lock = new object[max_thread];
            _request = new NetStream[max_thread];
            _position = new ulong[max_thread];

            //starting requests
            //TODO: move this code to auto-start region (with time delay)
            for (int i = 0; i < max_thread; i++)
            {
                _last_receive[i] = DateTime.Now;
                _buffer_stream[i] = new MemoryStream();
                _thread_data_lock[i] = new object();
                _request[i] = new NetStream();

                //preloading data region
                _guid_list[i] = _dispatcher.AllocateNewTask(out _position[i]);
                if (_guid_list[i] != Guid.Empty)
                {
                    try
                    {
                        _request[i].HttpGetAsync(_urls[i % _urls.Length], _data_transfer_callback, i, range: (long)_position[i]);
                    }
                    catch (Exception ex)
                    {
                        Tracer.GlobalTracer.TraceError(ex);
                    }
                    finally
                    {
                        _dispatcher.ReleaseTask(_guid_list[i]);
                        _guid_list[i] = Guid.Empty;
                    }
                }
            }
        }

        //TCP stream -> RAM stream
        private void _data_transfer_callback(NetStream ns, object state)
        {
            const int buffer_size = 2048;
            var buffer = new byte[buffer_size];
            int index = (int)state;
            try
            {
                var istream = ns.ResponseStream;
                var ostream = _buffer_stream[index];
                var bytes_read = 0;
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
                        if (ostream.Position != ostream.Length)
                            ostream.Position = ostream.Length;

                        ostream.Write(buffer, 0, bytes_read);
                        _position[index] += (uint)bytes_read;
                        _last_receive[index] = DateTime.Now;
                    }
                } while (_dispatcher.UpdateTaskSituation(_guid_list[index], _position[index]) && bytes_read > 0);
            }
            catch (Exception ex)
            {

                throw;
            }
        }
        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
