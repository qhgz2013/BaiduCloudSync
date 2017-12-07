using GlobalUtil;
using GlobalUtil.NetUtils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace BaiduCloudSync
{
    //todo: implement file encryption
    public class Uploader : IDisposable, ITransfer
    {
        //默认并行4线程上传
        public const int DEFAULT_MAX_THREAD = 4;
        //是否允许分段上传
        private bool _enable_slice_upload = true;
        //是否开启文件加密
        private bool _enable_encryption;
        //是否开启秒传
        private bool _enable_rapid_upload = true;

        //api数据
        private LocalFileCacher _local_cacher;
        private RemoteFileCacher _remote_cacher;
        private int _selected_account_id; //对应RemoteFileCacher的账号id
        private KeyManager _key_manager;

        //上传的网盘文件路径
        private string _remote_path;
        //本地文件路径
        private string _local_path;
        //文件大小
        private long _file_size;

        //并行请求
        private Guid[] _task_id;
        private int[] _task_seq;
        private DateTime[] _last_sent;

        //并行线程数
        private int _max_thread;
        private FileSystemWatcher _file_watcher;

        //本地文件信息
        private LocalFileData _local_data;
        //网盘文件信息（上传成果后修改）
        private ObjectMetadata _remote_data;

        //开始时间
        private DateTime _start_time, _end_time;

        //平均时间
        private double _average_speed_total;
        private double _average_speed_5s;
        private LinkedList<long> _upload_size_5s;
        //已上传字节
        private long _uploaded_size;
        //速度限制
        private int _speed_limit;
        private long _current_bytes;

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
        private ManualResetEventSlim _monitor_thread_created; //线程是否已创建
        private ManualResetEventSlim _file_io_response; //文件IO是否已经进行回调

        //线程锁
        private object _external_lock;
        private object _thread_flag_lock;
        private object _thread_data_lock;

        //文件的分段数量
        private int _slice_count;
        //未完成的分段队列
        private ConcurrentQueue<int> _slice_seq;
        //已成功上传的文件的分段以及对应的MD5
        private ConcurrentDictionary<int, string> _slice_result;
        //分段上传用的上传id
        private string _upload_id;


        #region public properties
        [Flags]
        public enum State
        {
            READY = _UPLOAD_THREAD_FLAG_READY,
            STARTED = _UPLOAD_THREAD_FLAG_STARTED,
            START_REQUESTED = _UPLOAD_THREAD_FLAG_START_REQUESTED,
            PAUSED = _UPLOAD_THREAD_FLAG_PAUSED,
            PAUSE_REQUESTED = _UPLOAD_THREAD_FLAG_PAUSE_REQUESTED,
            CANCELLED = _UPLOAD_THREAD_FLAG_CANCELLED,
            CANCEL_REQUESTED = _UPLOAD_THREAD_FLAG_CANCEL_REQUESTED,
            DIGEST_REQUESTED = _UPLOAD_THREAD_FLAG_DIGEST_REQUESTED,
            DIGEST_CALCULATING = _UPLOAD_THREAD_FLAG_DIGEST_CALCULATING,
            ERROR = _UPLOAD_THREAD_FLAG_ERROR,
            FILE_MODIFIED = _UPLOAD_THREAD_FLAG_FILE_MODIFIED,
            FILE_ENCRYPTING = _UPLOAD_THREAD_FLAG_FILE_ENCRYPTING,
            FINISHED = _UPLOAD_THREAD_FLAG_FINISHED
        }
        /// <summary>
        /// 任务状态
        /// </summary>
        public State TaskState { get { return (State)_upload_thread_flag; } }
        /// <summary>
        /// 已上传字节数
        /// </summary>
        public long UploadedSize { get { return _uploaded_size; } }
        /// <summary>
        /// 任务的开始时间
        /// </summary>
        public DateTime StartTime { get { return _start_time; } }
        /// <summary>
        /// 任务的上传速度限制（单位：B/s）
        /// </summary>
        public int SpeedLimit { get { return _speed_limit; } set { if (value < 0) throw new ArgumentOutOfRangeException("SpeedLimit"); _speed_limit = value; } }
        /// <summary>
        /// 本地文件路径
        /// </summary>
        public string LocalFilePath { get { return _local_path; } }
        /// <summary>
        /// 网盘文件路径
        /// </summary>
        public string RemoteFilePath { get { return _remote_path; } }
        /// <summary>
        /// 5秒内的平均上传速度
        /// </summary>
        public double AverageSpeed5s { get { return _average_speed_5s; } }
        /// <summary>
        /// 总平均上传速度
        /// </summary>
        public double AverageSpeedTotal { get { return _average_speed_total; } }
        /// <summary>
        /// 上传的并行线程数
        /// </summary>
        public int MaxThread { get { return _max_thread; } set { if (value <= 0) throw new ArgumentOutOfRangeException("MaxThread"); _max_thread = value; } }
        public object Tag { get; set; }
        /// <summary>
        /// 任务的经历时间
        /// </summary>
        public TimeSpan EllapsedTime { get { return ((_upload_thread_flag & _UPLOAD_THREAD_FLAG_STARTED) != 0) ? (DateTime.Now - _start_time) : (_end_time - _start_time); } }
        /// <summary>
        /// 上传成功后的数据
        /// </summary>
        public ObjectMetadata UploadResult { get { return _remote_data; } }
        /// <summary>
        /// 文件大小
        /// </summary>
        public long Size { get { return _file_size; } }
        #endregion

        //是否覆盖已有文件
        private bool _overwrite;
        public Uploader(LocalFileCacher local_cacher, RemoteFileCacher remote_cacher, string local_path, string remote_path, int account_id, bool overwriting_exist_file = false, int max_thread = DEFAULT_MAX_THREAD, int speed_limit = 0, KeyManager key_manager = null, bool enable_encryption = false)
        {
            if (local_cacher == null) throw new ArgumentNullException("local_cacher");
            if (remote_cacher == null) throw new ArgumentNullException("remote_cacher");
            if (string.IsNullOrEmpty(local_path)) throw new ArgumentNullException("local_path");
            if (string.IsNullOrEmpty(remote_path)) throw new ArgumentNullException("remote_path");
            if (remote_path.EndsWith("/")) throw new ArgumentException("remote_path非法：/不能在路径结束");
            if (speed_limit < 0) throw new ArgumentOutOfRangeException("speed_limit");
            _overwrite = overwriting_exist_file;
            _local_cacher = local_cacher;
            _remote_cacher = remote_cacher;
            _local_path = local_path;
            _remote_path = remote_path;
            _speed_limit = speed_limit;
            _key_manager = key_manager;
            _enable_encryption = enable_encryption;

            var file_info = new FileInfo(_local_path);
            //file monitor
            _file_watcher = new FileSystemWatcher(file_info.Directory.FullName, file_info.Name);
            _file_watcher.Changed += _on_file_changed;
            _file_watcher.Deleted += _on_file_deleted;
            _file_watcher.EnableRaisingEvents = true;

            _external_lock = new object();
            _thread_flag_lock = new object();
            _thread_data_lock = new object();

            _file_size = file_info.Length;
            _max_thread = max_thread;
            _slice_count = (int)Math.Ceiling(_file_size * 1.0 / BaiduPCS.UPLOAD_SLICE_SIZE);
            _slice_seq = new ConcurrentQueue<int>();
            _slice_result = new ConcurrentDictionary<int, string>();

            _selected_account_id = account_id;
            _upload_size_5s = new LinkedList<long>();
            for (int i = 0; i < 6; i++)
                _upload_size_5s.AddLast(0);
            _monitor_thread_created = new ManualResetEventSlim();
            _file_io_response = new ManualResetEventSlim();

            _local_cacher.LocalFileIOFinish += _on_file_io_completed;
            _local_cacher.LocalFileIOUpdate += _on_file_io_updated;
            _local_cacher.LocalFileIOAbort += _on_file_io_aborted;

            _upload_thread_flag = _UPLOAD_THREAD_FLAG_READY;
        }

        public void Dispose()
        {
            if (_monitor_thread != null)
            {
                //todo: modify to join operation if necessary
                Cancel();
            }

            lock (_thread_data_lock)
            {
                if (_task_id != null)
                {
                    for (int i = 0; i < _task_id.Length; i++)
                    {
                        if (_task_id[i] != Guid.Empty)
                        {
                            _remote_cacher.UploadSliceCancelAsync(_task_id[i], _selected_account_id);
                        }
                    }
                    _task_id = null;
                    _task_seq = null;
                    _last_sent = null;
                    _upload_size_5s = null;
                }
            }

            if (_file_watcher != null)
            {
                _file_watcher.EnableRaisingEvents = false;
                _file_watcher.Dispose();
                _file_watcher = null;
            }

            _slice_result = null;
            _slice_seq = null;
            _local_cacher.LocalFileIOFinish -= _on_file_io_completed;
            _local_cacher.LocalFileIOUpdate -= _on_file_io_updated;
            _local_cacher.LocalFileIOAbort -= _on_file_io_aborted;
        }

        public void Start()
        {
            lock (_external_lock)
            {
                lock (_thread_flag_lock)
                {
                    //clearing attributed status
                    _upload_thread_flag = _upload_thread_flag & 0xffffff;
                    if ((_upload_thread_flag & (_UPLOAD_THREAD_FLAG_READY | _UPLOAD_THREAD_FLAG_PAUSED)) != 0)
                    {
                        if (_enable_slice_upload)
                        {
                            Tracer.GlobalTracer.TraceInfo("---STARTED---");
                            _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_START_REQUESTED) & ~_UPLOAD_THREAD_FLAG_READY;

                            _monitor_thread = new Thread(_monitor_thread_callback);
                            _monitor_thread.IsBackground = true;
                            _monitor_thread.Name = "Upload monitor";
                            _monitor_thread_created.Reset();
                            _monitor_thread.Start();
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                    }
                    else return;
                }
            }
            try { TaskStarted?.Invoke(this, new EventArgs()); } catch { }
        }
        public void Pause()
        {
            lock (_external_lock)
            {
                lock (_thread_flag_lock)
                {
                    //ready state
                    if ((_upload_thread_flag & _UPLOAD_THREAD_FLAG_READY) != 0)
                    {
                        _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_PAUSED) & ~_UPLOAD_THREAD_FLAG_READY;
                        return;
                    }
                    //status failure
                    if ((_upload_thread_flag & (_UPLOAD_THREAD_FLAG_STARTED | _UPLOAD_THREAD_FLAG_START_REQUESTED)) != 0)
                    {
                        Tracer.GlobalTracer.TraceInfo("---PAUSED---");
                        _upload_thread_flag |= _UPLOAD_THREAD_FLAG_PAUSE_REQUESTED;
                    }
                    else
                        return;

                    _monitor_thread_created.Wait();
                    _monitor_thread.Join();
                    _monitor_thread_created.Reset();
                }
            }
            try { TaskPaused?.Invoke(this, new EventArgs()); } catch { }
        }
        public void Cancel()
        {
            lock (_external_lock)
            {
                lock (_thread_flag_lock)
                {
                    //cancelled state
                    if ((_upload_thread_flag & (_UPLOAD_THREAD_FLAG_CANCELLED | _UPLOAD_THREAD_FLAG_FINISHED)) != 0)
                        return;
                    //ready state
                    if ((_upload_thread_flag & _UPLOAD_THREAD_FLAG_READY) != 0)
                    {
                        _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_CANCELLED) & ~_UPLOAD_THREAD_FLAG_READY;
                        return;
                    }
                    //paused state
                    if ((_upload_thread_flag & _UPLOAD_THREAD_FLAG_PAUSED) != 0)
                    {
                        _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_CANCELLED) & ~_UPLOAD_THREAD_FLAG_PAUSED;
                        return;
                    }
                    if ((_upload_thread_flag & (_UPLOAD_THREAD_FLAG_STARTED | _UPLOAD_THREAD_FLAG_START_REQUESTED)) != 0)
                    {
                        Tracer.GlobalTracer.TraceInfo("---CANCELLED---");
                        _upload_thread_flag |= _UPLOAD_THREAD_FLAG_CANCEL_REQUESTED;
                    }

                    _monitor_thread_created.Wait();
                    _monitor_thread.Join();
                    _monitor_thread_created.Reset();
                    Dispose();
                }
            }
            try { TaskCancelled?.Invoke(this, new EventArgs()); } catch { }
        }

        #region Event handler
        public event EventHandler TaskStarted, TaskFinished, TaskPaused, TaskCancelled, TaskError, EncryptStarted, EncryptFinished;
        public event EventHandler FileDigestStarted, FileDigestFinished, EncryptFileDigestStarted, EncryptFileDigestFinished;
        #endregion

        private void _on_file_deleted(object sender, FileSystemEventArgs e)
        {
            _upload_thread_flag = _upload_thread_flag | _UPLOAD_THREAD_FLAG_ERROR | _UPLOAD_THREAD_FLAG_FILE_MODIFIED;
        }
        private void _on_file_changed(object sender, FileSystemEventArgs e)
        {
            _upload_thread_flag = _upload_thread_flag | _UPLOAD_THREAD_FLAG_ERROR | _UPLOAD_THREAD_FLAG_FILE_MODIFIED;
        }

        private void _on_file_io_updated(string path, long current, long total)
        {
            //todo: updating io status
            if (path == _local_path)
            {
                if ((_upload_thread_flag & _UPLOAD_THREAD_FLAG_DIGEST_REQUESTED) != 0)
                {
                    _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_DIGEST_CALCULATING) & ~_UPLOAD_THREAD_FLAG_DIGEST_REQUESTED;
                }
                _uploaded_size = current;
            }
        }
        private void _on_file_io_completed(LocalFileData data)
        {
            if (data.Path == _local_path)
            {
                _local_data = data;
                _upload_thread_flag = _upload_thread_flag & ~(_UPLOAD_THREAD_FLAG_DIGEST_REQUESTED | _UPLOAD_THREAD_FLAG_DIGEST_CALCULATING);
                _file_io_response.Set();
            }
        }
        private void _on_file_io_aborted(string path)
        {
            if (path == _local_path)
            {
                _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_ERROR) & ~(_UPLOAD_THREAD_FLAG_DIGEST_REQUESTED | _UPLOAD_THREAD_FLAG_DIGEST_CALCULATING);
                _file_io_response.Set();
            }
        }

        private void _on_slice_upload_request_callback(bool suc, Guid task_id, Stream connect_stream, object state)
        {
            if (!suc)
            {
                return;
            }
            int index = (int)state;
            FileStream fs = null;
            int seq_id;
            lock (_thread_data_lock)
            {
                _task_id[index] = task_id;
                seq_id = _task_seq[index];
            }
            long offset = BaiduPCS.UPLOAD_SLICE_SIZE * seq_id;

            int data_offset = 0;
            try
            {
                fs = new FileStream(_local_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                fs.Seek(offset, SeekOrigin.Begin);
                var data = util.ReadBytes(fs, BaiduPCS.UPLOAD_SLICE_SIZE);

                const int transfer_size = 2048;
                while (data_offset < data.Length)
                {
                    if (_speed_limit > 0 && _current_bytes >= _speed_limit)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    int length = Math.Min(data.Length - data_offset, transfer_size);
                    connect_stream.Write(data, data_offset, length);
                    data_offset += length;
                    _current_bytes += length;
                    Interlocked.Add(ref _uploaded_size, length);

                    _last_sent[index] = DateTime.Now;
                }

                _remote_cacher.UploadSliceEndAsync(task_id, (suc2, data2, e2) =>
                {
                    if (suc2)
                    {
                        _slice_result.TryAdd(seq_id, data2);
                    }
                    else
                    {
                        _slice_seq.Enqueue(seq_id);
                        Interlocked.Add(ref _uploaded_size, -data_offset);
                    }
                }, _selected_account_id);
            }
            catch (Exception ex)
            {
                Tracer.GlobalTracer.TraceError(ex);
                Interlocked.Add(ref _uploaded_size, -data_offset);
                _slice_seq.Enqueue(seq_id);
            }
            finally
            {
                fs?.Close();
                lock (_thread_data_lock)
                {
                    _task_id[index] = Guid.Empty;
                    _last_sent[index] = DateTime.MinValue;
                }
            }
        }

        private void _file_encrypt_status_callback(string inputFile, string outputFile, long current, long total)
        {
            _uploaded_size = current;
            _file_size = total;
        }
        private void _file_encrypt()
        {
            if (_key_manager == null)
                return;
            try
            {
                if (_key_manager.IsDynamicEncryption)
                {
                    FileEncrypt.EncryptFile(_local_path, _local_path + ".encrypted", _key_manager.RSAPublicKey, _local_data.SHA1, _file_encrypt_status_callback);
                }
                else if (_key_manager.IsStaticEncryption)
                {
                    FileEncrypt.EncryptFile(_local_path, _local_path + ".encrypted", _key_manager.AESKey, _key_manager.AESIV, _local_data.SHA1, _file_encrypt_status_callback);
                }
                else
                {
                    throw new Exception("密钥缺失");
                }

                _local_path += ".encrypted";
                if (!_remote_path.EndsWith(".bcsd"))
                    _remote_path += ".bcsd";
            }
            catch (Exception ex)
            {
                Tracer.GlobalTracer.TraceError(ex);
            }
        }
        private void _monitor_thread_callback()
        {
            _monitor_thread_created.Set();
            _start_time = DateTime.Now;
            try
            {
                _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_STARTED) & ~(_UPLOAD_THREAD_FLAG_START_REQUESTED | _UPLOAD_THREAD_FLAG_PAUSED);

                //local io
                if (string.IsNullOrEmpty(_local_data.Path))
                {
                    _local_cacher.FileIORequest(_local_path);
                    _upload_thread_flag |= _UPLOAD_THREAD_FLAG_DIGEST_REQUESTED;
                    try { FileDigestStarted?.Invoke(this, new EventArgs()); } catch { }
                    _file_io_response.Wait();
                    _file_io_response.Reset();
                    try { FileDigestFinished?.Invoke(this, new EventArgs()); } catch { }
                }
                _uploaded_size = 0;

                #region status check
                if ((_upload_thread_flag & _UPLOAD_THREAD_FLAG_CANCEL_REQUESTED) != 0)
                {
                    _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_CANCELLED) & ~(_UPLOAD_THREAD_FLAG_CANCEL_REQUESTED | _UPLOAD_THREAD_FLAG_STARTED);
                    _end_time = DateTime.Now;
                    return;
                }
                if ((_upload_thread_flag & _UPLOAD_THREAD_FLAG_PAUSE_REQUESTED) != 0)
                {
                    _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_PAUSED) & ~(_UPLOAD_THREAD_FLAG_PAUSE_REQUESTED | _UPLOAD_THREAD_FLAG_STARTED);
                    _end_time = DateTime.Now;
                    return;
                }
                if ((_upload_thread_flag & _UPLOAD_THREAD_FLAG_ERROR) != 0)
                {
                    _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_PAUSED) & ~_UPLOAD_THREAD_FLAG_STARTED;
                    _end_time = DateTime.Now;
                    try { TaskError?.Invoke(this, new EventArgs()); } catch { }
                    return;
                }
                #endregion

                if (_enable_encryption)
                {
                    _upload_thread_flag |= _UPLOAD_THREAD_FLAG_FILE_ENCRYPTING;
                    try { EncryptStarted?.Invoke(this, new EventArgs()); } catch { }
                    _file_encrypt();
                    _upload_thread_flag = _upload_thread_flag & ~_UPLOAD_THREAD_FLAG_FILE_ENCRYPTING;
                    try { EncryptFinished?.Invoke(this, new EventArgs()); } catch { }
                    _uploaded_size = 0;
                    

                    //handling IO
                    _local_cacher.FileIORequest(_local_path);
                    _upload_thread_flag |= _UPLOAD_THREAD_FLAG_DIGEST_REQUESTED;
                    try { EncryptFileDigestStarted?.Invoke(this, new EventArgs()); } catch { }
                    _file_io_response.Wait();
                    _file_io_response.Reset();
                    try { EncryptFileDigestFinished?.Invoke(this, new EventArgs()); } catch { }

                    //handling file slice data
                    _uploaded_size = 0;
                    _file_size = _local_data.Size;
                    _slice_count = (int)Math.Ceiling(_file_size * 1.0 / BaiduPCS.UPLOAD_SLICE_SIZE);
                }

                //status check
                #region status check
                if ((_upload_thread_flag & _UPLOAD_THREAD_FLAG_CANCEL_REQUESTED) != 0)
                {
                    _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_CANCELLED) & ~(_UPLOAD_THREAD_FLAG_CANCEL_REQUESTED | _UPLOAD_THREAD_FLAG_STARTED);
                    _end_time = DateTime.Now;
                    return;
                }
                if ((_upload_thread_flag & _UPLOAD_THREAD_FLAG_PAUSE_REQUESTED) != 0)
                {
                    _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_PAUSED) & ~(_UPLOAD_THREAD_FLAG_PAUSE_REQUESTED | _UPLOAD_THREAD_FLAG_STARTED);
                    _end_time = DateTime.Now;
                    return;
                }
                if ((_upload_thread_flag & _UPLOAD_THREAD_FLAG_ERROR) != 0)
                {
                    _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_PAUSED) & ~_UPLOAD_THREAD_FLAG_STARTED;
                    _end_time = DateTime.Now;
                    try { TaskError?.Invoke(this, new EventArgs()); } catch { }
                    return;
                }
                #endregion

                //rapid upload test
                var sync_lock = new ManualResetEventSlim();
                var rapid_param = _local_data;
                bool rapid_upload_suc = false;
                if (_enable_rapid_upload)
                {
                    _remote_cacher.RapidUploadAsync(_remote_path, (ulong)rapid_param.Size, rapid_param.MD5, rapid_param.CRC32.ToString("X2").ToLower(), rapid_param.Slice_MD5, (suc, data, e) =>
                    {
                        rapid_upload_suc = suc;
                        _remote_data = data;
                        sync_lock.Set();
                    }, _overwrite ? BaiduPCS.ondup.overwrite : BaiduPCS.ondup.newcopy, _selected_account_id);
                    sync_lock.Wait();
                    sync_lock.Reset();
                }

                if (rapid_upload_suc == false)
                {
                    //deleting existed file if overwrite is true
                    if (_overwrite)
                    {
                        _remote_cacher.DeletePathAsync(_remote_path, (suc, data, e) => { }, _selected_account_id);
                    }
                    //pre create file request
                    if (string.IsNullOrEmpty(_upload_id))
                    {
                        bool precreate_suc = false;
                        _remote_cacher.PreCreateFileAsync(_remote_path, _slice_count, (suc, data, uploadid, e) =>
                        {
                            precreate_suc = suc;
                            _upload_id = uploadid;
                            sync_lock.Set();
                        }, _selected_account_id);
                        sync_lock.Wait();
                        sync_lock.Reset();
                        if (precreate_suc == false)
                        {
                            //precreate failed
                            _upload_thread_flag |= _UPLOAD_THREAD_FLAG_ERROR;
                            _end_time = DateTime.Now;
                            try { TaskError?.Invoke(this, new EventArgs()); } catch { }
                            return;
                        }
                        #region status check
                        if ((_upload_thread_flag & _UPLOAD_THREAD_FLAG_CANCEL_REQUESTED) != 0)
                        {
                            _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_CANCELLED) & ~(_UPLOAD_THREAD_FLAG_CANCEL_REQUESTED | _UPLOAD_THREAD_FLAG_STARTED);
                            _end_time = DateTime.Now;
                            return;
                        }
                        if ((_upload_thread_flag & _UPLOAD_THREAD_FLAG_PAUSE_REQUESTED) != 0)
                        {
                            _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_PAUSED) & ~(_UPLOAD_THREAD_FLAG_PAUSE_REQUESTED | _UPLOAD_THREAD_FLAG_STARTED);
                            _end_time = DateTime.Now;
                            return;
                        }
                        #endregion
                    }

                    //initializing multi thread data
                    var max_thread = _max_thread;
                    _task_id = new Guid[max_thread];
                    _task_seq = new int[max_thread];
                    _last_sent = new DateTime[max_thread];

                    //adding slice sequence
                    _slice_seq = new ConcurrentQueue<int>();
                    for (int i = 0; i < _slice_count; i++)
                        if (_slice_result.ContainsKey(i) == false)
                            _slice_seq.Enqueue(i);

                    //upload start, multi thread
                    var next_time = DateTime.Now.AddSeconds(1);
                    #region loop
                    while (true)
                    {
                        //handling finish state
                        if (_slice_result.Count == _slice_count)
                        {
                            break;
                        }
                        //handling other state
                        #region status check
                        if ((_upload_thread_flag & _UPLOAD_THREAD_FLAG_CANCEL_REQUESTED) != 0)
                        {
                            _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_CANCELLED) & ~(_UPLOAD_THREAD_FLAG_CANCEL_REQUESTED | _UPLOAD_THREAD_FLAG_STARTED);
                            for (int i = 0; i < _task_id.Length; i++)
                            {
                                if (_task_id[i] != Guid.Empty)
                                    _remote_cacher.UploadSliceCancelAsync(_task_id[i]);
                            }
                            _end_time = DateTime.Now;
                            return;
                        }
                        if ((_upload_thread_flag & _UPLOAD_THREAD_FLAG_PAUSE_REQUESTED) != 0)
                        {
                            _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_PAUSED) & ~(_UPLOAD_THREAD_FLAG_PAUSE_REQUESTED | _UPLOAD_THREAD_FLAG_STARTED);
                            for (int i = 0; i < _task_id.Length; i++)
                            {
                                if (_task_id[i] != Guid.Empty)
                                    _remote_cacher.UploadSliceCancelAsync(_task_id[i]);
                            }
                            _end_time = DateTime.Now;
                            return;
                        }
                        if ((_upload_thread_flag & (_UPLOAD_THREAD_FLAG_ERROR | _UPLOAD_THREAD_FLAG_FILE_MODIFIED)) != 0)
                        {
                            _upload_thread_flag = _upload_thread_flag & ~_UPLOAD_THREAD_FLAG_STARTED;
                            _end_time = DateTime.Now;
                            try { TaskError?.Invoke(this, new EventArgs()); } catch { }
                            return;
                        }
                        #endregion

                        lock (_thread_data_lock)
                        {
                            for (int i = 0; i < max_thread && _slice_seq.Count > 0; i++)
                            {
                                if (_task_id[i] == Guid.Empty || (DateTime.Now - _last_sent[i]).TotalSeconds > 120)
                                {
                                    if (_task_id[i] != Guid.Empty)
                                    {
                                        _remote_cacher.UploadSliceCancelAsync(_task_id[i], _selected_account_id);
                                        _task_id[i] = Guid.Empty;
                                    }

                                    //error handling for dequeue failure
                                    if (_slice_seq.TryDequeue(out _task_seq[i]) == false)
                                    {
                                        _upload_thread_flag |= _UPLOAD_THREAD_FLAG_ERROR;
                                        _end_time = DateTime.Now;
                                        try { TaskError?.Invoke(this, new EventArgs()); } catch { }
                                        return;
                                    }
                                    _remote_cacher.UploadSliceBeginAsync((ulong)Math.Min(_file_size - BaiduPCS.UPLOAD_SLICE_SIZE * _task_seq[i], BaiduPCS.UPLOAD_SLICE_SIZE), _remote_path, _upload_id, _task_seq[i], _on_slice_upload_request_callback, _selected_account_id, i);
                                    _last_sent[i] = DateTime.Now;
                                }
                            }
                        }

                        //speed calculation
                        long size = Interlocked.Read(ref _uploaded_size);
                        _upload_size_5s.RemoveFirst();
                        _upload_size_5s.AddLast(size);
                        _average_speed_5s = (_upload_size_5s.Last.Value - _upload_size_5s.First.Value) / 5.0;
                        _average_speed_total = size / (DateTime.Now - _start_time).TotalSeconds;

                        _current_bytes = 0;
                        Tracer.GlobalTracer.TraceInfo("Uploaded " + _uploaded_size + "/" + _file_size + " [" + (_average_speed_5s / 1024.0).ToString("0.00") + "KB/s]");

                        //monitor loop
                        var ts = next_time - DateTime.Now;
                        next_time = next_time.AddSeconds(1);
                        if (ts.TotalMilliseconds > 1)
                            Thread.Sleep((int)ts.TotalMilliseconds);
                    }
                    #endregion

                    //merging slice request
                    var create_superfile_suc = false;
                    _remote_cacher.CreteSuperFileAsync(_remote_path, _upload_id, from item in _slice_result orderby item.Key ascending select item.Value, (ulong)_file_size, (suc, data, e) =>
                    {
                        create_superfile_suc = suc;
                        _remote_data = data;
                        sync_lock.Set();
                    }, _selected_account_id);
                    sync_lock.Wait();
                    sync_lock.Reset();

                    if (create_superfile_suc)
                    {
                        _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_FINISHED) & ~_UPLOAD_THREAD_FLAG_STARTED;
                        try { TaskFinished?.Invoke(this, new EventArgs()); } catch { }
                    }
                    else
                    {
                        _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_ERROR | _UPLOAD_THREAD_FLAG_PAUSED) & ~_UPLOAD_THREAD_FLAG_STARTED;
                        try { TaskError?.Invoke(this, new EventArgs()); } catch { }
                    }
                    _end_time = DateTime.Now;
                }
                else
                {
                    //rapid upload succeeded
                    _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_FINISHED) & ~_UPLOAD_THREAD_FLAG_STARTED;
                    _end_time = DateTime.Now;
                    try { TaskFinished?.Invoke(this, new EventArgs()); } catch { }
                }

            }
            catch (Exception)
            {
                _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_ERROR | _UPLOAD_THREAD_FLAG_PAUSED) & ~_UPLOAD_THREAD_FLAG_STARTED;
                _end_time = DateTime.Now;
                try { TaskError?.Invoke(this, new EventArgs()); } catch { }

            }
            finally
            {
                _monitor_thread = null;
                //deleting temporary encrypted file
                if (_enable_encryption && File.Exists(_local_path) && _local_path.EndsWith(".encrypted"))
                    File.Delete(_local_path);
            }
        }
    }
}
