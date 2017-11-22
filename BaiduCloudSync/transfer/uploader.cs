using GlobalUtil.NetUtils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace BaiduCloudSync
{
    //todo: implement file encryption
    public class Uploader : IDisposable
    {
        //默认并行4线程上传
        public const int DEFAULT_THREAD_SIZE = 4;
        //是否允许分段上传
        private bool _enable_slice_upload = true;

        //api数据
        private LocalFileCacher _local_cacher;
        private RemoteFileCacher _remote_cacher;
        private int _selected_account_id;

        //上传的网盘文件路径
        private string _remote_path;
        //本地文件路径
        private string _local_path;
        //文件大小
        private long _file_size;

        //并行请求
        private NetStream[] _request;
        //本地文件数据流
        private FileStream _local_stream;
        //并行线程数
        private int _max_thread;
        private FileSystemWatcher _file_watcher;

        //本地文件信息
        private LocalFileData _local_data;
        //网盘文件信息（上传成果后修改）
        private ObjectMetadata _remote_data;

        //开始时间
        private DateTime _start_time;

        //平均时间
        private double _average_speed_total;
        private double _average_speed_5s;
        private LinkedList<long> _upload_size_5s;
        //已上传字节
        private double _uploaded_size;

        //线程标识
        private volatile int _upload_thread_flag;
        private const int _UPLOAD_THREAD_FLAG_READY = 1;
        private const int _UPLOAD_THREAD_FLAG_START_REQUESTED = 2;
        private const int _UPLOAD_THREAD_FLAG_STARTED = 4;
        private const int _UPLOAD_THREAD_FLAG_PAUSE_REQUESTED = 8;
        private const int _UPLOAD_THREAD_FLAG_PAUSED = 16;
        private const int _UPLOAD_THREAD_FLAG_CANCEL_REQUESTED = 32;
        private const int _UPLOAD_THREAD_FLAG_CANCELLED = 64;
        private const int _UPLOAD_THREAD_FLAG_FINISHED = 128;
        //status flag
        private const int _UPLOAD_THREAD_FLAG_ERROR = int.MinValue;
        private const int _UPLOAD_THREAD_FLAG_DIGEST_REQUESTED = 0x40000000;
        private const int _UPLOAD_THREAD_FLAG_DIGEST_CALCULATING = 0x20000000;
        private const int _UPLOAD_THREAD_FLAG_FILE_MODIFIED = 0x10000000;
        private const int _UPLOAD_THREAD_FLAG_FILE_ENCRYPTING = 0x08000000;


        //监控线程
        private Thread _monitor_thread;
        private ManualResetEventSlim _monitor_thread_created;

        private object _external_lock;
        private object _thread_flag_lock;
        private object _local_io_lock;

        //upload data
        private int _slice_count;
        private int[] _slice_queue;
        private string _upload_id;

        private bool _overwrite;
        public Uploader(LocalFileCacher local_cacher, RemoteFileCacher remote_cacher, string local_path, string remote_path, int account_id, bool overwriting_exist_file = false, int max_thread = DEFAULT_THREAD_SIZE)
        {
            if (local_cacher == null) throw new ArgumentNullException("local_cacher");
            if (remote_cacher == null) throw new ArgumentNullException("remote_cacher");
            if (string.IsNullOrEmpty(local_path)) throw new ArgumentNullException("local_path");
            if (string.IsNullOrEmpty(remote_path)) throw new ArgumentNullException("remote_path");
            if (remote_path.EndsWith("/")) throw new ArgumentException("remote_path非法：/不能在路径结束");
            _overwrite = overwriting_exist_file;
            _local_cacher = local_cacher;
            _remote_cacher = remote_cacher;
            _local_path = local_path;
            _remote_path = remote_path;

            var file_info = new FileInfo(_local_path);
            //file monitor
            _file_watcher = new FileSystemWatcher(file_info.Directory.FullName, file_info.Name);
            _file_watcher.Changed += _on_file_changed;
            _file_watcher.Deleted += _on_file_deleted;
            _file_watcher.EnableRaisingEvents = true;

            _local_stream = new FileStream(_local_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            _external_lock = new object();
            _thread_flag_lock = new object();
            _local_io_lock = new object();

            _file_size = file_info.Length;
            _max_thread = max_thread;
            _slice_count = (int)Math.Ceiling(_file_size * 1.0 / BaiduPCS.UPLOAD_SLICE_SIZE);
            _upload_thread_flag = _UPLOAD_THREAD_FLAG_READY;
            _selected_account_id = account_id;
            _upload_size_5s = new LinkedList<long>();
            for (int i = 0; i < 6; i++)
                _upload_size_5s.AddLast(0);
            _monitor_thread_created = new ManualResetEventSlim();

            _local_cacher.LocalFileIOFinish += _on_file_io_completed;
            _local_cacher.LocalFileIOUpdate += _on_file_io_updated;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void Start()
        {
            lock (_external_lock)
            {
                //error status
                if ((_upload_thread_flag & _UPLOAD_THREAD_FLAG_ERROR) != 0)
                    return;
                //start requested or cancelled
                if (((_upload_thread_flag & 0xffffff) & (_UPLOAD_THREAD_FLAG_CANCELLED | _UPLOAD_THREAD_FLAG_CANCEL_REQUESTED | _UPLOAD_THREAD_FLAG_START_REQUESTED)) != 0)
                    return;

                if (_enable_slice_upload)
                {
                    lock (_thread_flag_lock)
                        _upload_thread_flag = _upload_thread_flag | _UPLOAD_THREAD_FLAG_START_REQUESTED;

                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }
        public void Pause()
        {

        }
        public void Cancel()
        {

        }


        private void _on_file_deleted(object sender, FileSystemEventArgs e)
        {
            lock (_thread_flag_lock)
                _upload_thread_flag = _upload_thread_flag | _UPLOAD_THREAD_FLAG_ERROR | _UPLOAD_THREAD_FLAG_FILE_MODIFIED;
        }
        private void _on_file_changed(object sender, FileSystemEventArgs e)
        {
            lock (_thread_flag_lock)
                _upload_thread_flag = _upload_thread_flag | _UPLOAD_THREAD_FLAG_ERROR | _UPLOAD_THREAD_FLAG_FILE_MODIFIED;
        }

        private void _on_file_io_updated(string path, long current, long total)
        {

        }
        private void _on_file_io_completed(LocalFileData data)
        {
            if (data.Path == _local_path)
            {
                ThreadPool.QueueUserWorkItem(delegate
                {
                    _local_data = data;

                });
            }
        }
        private void _on_slice_upload_request_callback(bool suc, BaiduPCS.PreCreateResult result, object state)
        {

        }

        private void _monitor_thread_callback()
        {
            _monitor_thread_created.Set();
            try
            {
                if ((_upload_thread_flag & (_UPLOAD_THREAD_FLAG_ERROR | _UPLOAD_THREAD_FLAG_CANCELLED)) != 0)
                    return;
                lock (_thread_flag_lock)
                {
                    _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_STARTED) & ~_UPLOAD_THREAD_FLAG_START_REQUESTED;
                }
            }
            catch (Exception)
            {

            }
            finally
            {
                _monitor_thread = null;
            }
        }
    }
}
