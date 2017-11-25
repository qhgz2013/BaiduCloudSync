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
        private DateTime _start_time;

        //平均时间
        private double _average_speed_total;
        private double _average_speed_5s;
        private LinkedList<long> _upload_size_5s;
        //已上传字节
        private long _uploaded_size;

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
        private ManualResetEventSlim _file_io_response;

        private object _external_lock;
        private object _thread_flag_lock;
        private object _local_io_lock;

        //upload data
        private int _slice_count;
        private ConcurrentQueue<int> _slice_seq;
        private ConcurrentDictionary<int, string> _slice_result;
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

            _external_lock = new object();
            _thread_flag_lock = new object();
            _local_io_lock = new object();

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
            throw new NotImplementedException();
        }

        public void Start()
        {
            lock (_external_lock)
            {
                //clearing attributed status
                _upload_thread_flag = _upload_thread_flag & 0xffffff;
                //start, pause requested or cancelled
                if (((_upload_thread_flag & 0xffffff) & (_UPLOAD_THREAD_FLAG_CANCELLED | _UPLOAD_THREAD_FLAG_CANCEL_REQUESTED | _UPLOAD_THREAD_FLAG_PAUSE_REQUESTED | _UPLOAD_THREAD_FLAG_START_REQUESTED)) != 0)
                    return;

                if (_enable_slice_upload)
                {
                    lock (_thread_flag_lock)
                    {
                        _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_START_REQUESTED) & ~_UPLOAD_THREAD_FLAG_READY;

                        _monitor_thread = new Thread(_monitor_thread_callback);
                        _monitor_thread.IsBackground = true;
                        _monitor_thread.Name = "Upload monitor";
                        _monitor_thread_created.Reset();
                        _monitor_thread.Start();
                    }
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
            //todo: updating io status
            if ((_upload_thread_flag & _UPLOAD_THREAD_FLAG_DIGEST_REQUESTED) != 0 && path == _local_path)
            {
                lock (_thread_flag_lock)
                {
                    if ((_upload_thread_flag & _UPLOAD_THREAD_FLAG_DIGEST_REQUESTED) != 0)
                    {
                        _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_DIGEST_CALCULATING) & ~_UPLOAD_THREAD_FLAG_DIGEST_REQUESTED;
                    }
                }
            }
        }
        private void _on_file_io_completed(LocalFileData data)
        {
            if (data.Path == _local_path)
            {
                _local_data = data;
                lock (_thread_flag_lock)
                {
                    _upload_thread_flag = _upload_thread_flag & ~(_UPLOAD_THREAD_FLAG_DIGEST_REQUESTED | _UPLOAD_THREAD_FLAG_DIGEST_CALCULATING);
                }
                _file_io_response.Set();
            }
        }
        private void _on_file_io_aborted(string path)
        {
            if (path == _local_path)
            {
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
            int seq_id = _task_seq[index];
            long offset = BaiduPCS.UPLOAD_SLICE_SIZE * seq_id;
            FileStream fs = null;
            _task_id[index] = task_id;

            int data_offset = 0;
            try
            {
                fs = new FileStream(_local_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                fs.Seek(offset, SeekOrigin.Begin);
                var data = util.ReadBytes(fs, BaiduPCS.UPLOAD_SLICE_SIZE);

                const int transfer_size = 2048;
                while (data_offset < data.Length)
                {
                    int length = Math.Min(data.Length - data_offset, transfer_size);
                    connect_stream.Write(data, data_offset, length);
                    data_offset += length;
                    Interlocked.Add(ref _uploaded_size, length);
                    _last_sent[index] = DateTime.Now;
                    if (_upload_thread_flag != _UPLOAD_THREAD_FLAG_STARTED)
                        return;
                }

                _remote_cacher.UploadSliceEndAsync(task_id, (suc2, data2, e2) =>
                {
                    if (suc2)
                    {
                        Tracer.GlobalTracer.TraceWarning("Adding #" + seq_id + " to MD5 dictionary!");
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
                _task_id[index] = Guid.Empty;
                _last_sent[index] = DateTime.MinValue;
            }
        }

        private void _file_encrypt()
        {

        }
        private void _monitor_thread_callback()
        {
            _monitor_thread_created.Set();
            _start_time = DateTime.Now;
            try
            {
                lock (_thread_flag_lock)
                {
                    _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_STARTED) & ~_UPLOAD_THREAD_FLAG_START_REQUESTED;
                }

                //TODO: add file encryption module here
                _file_encrypt();

                //local io
                _local_cacher.FileIORequest(_local_path);
                lock (_thread_flag_lock)
                    _upload_thread_flag |= _UPLOAD_THREAD_FLAG_DIGEST_REQUESTED;
                _file_io_response.Wait();

                //status check
                #region status check
                lock (_thread_flag_lock)
                {
                    if ((_upload_thread_flag & _UPLOAD_THREAD_FLAG_CANCEL_REQUESTED) != 0)
                    {
                        _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_CANCELLED) & ~(_UPLOAD_THREAD_FLAG_CANCEL_REQUESTED | _UPLOAD_THREAD_FLAG_STARTED);
                        return;
                    }
                    if ((_upload_thread_flag & _UPLOAD_THREAD_FLAG_PAUSE_REQUESTED) != 0)
                    {
                        _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_PAUSED) & ~(_UPLOAD_THREAD_FLAG_PAUSE_REQUESTED | _UPLOAD_THREAD_FLAG_STARTED);
                        return;
                    }
                }
                #endregion

                //rapid upload test
                var sync_lock = new ManualResetEventSlim();
                var rapid_param = _local_data;
                bool rapid_upload_suc = false;
                //_remote_cacher.RapidUploadAsync(_remote_path, (ulong)rapid_param.Size, rapid_param.MD5, rapid_param.CRC32.ToString("X2").ToLower(), rapid_param.Slice_MD5, (suc, data, e) =>
                //{
                //    rapid_upload_suc = suc;
                //    _remote_data = data;
                //    sync_lock.Set();
                //}, _overwrite ? BaiduPCS.ondup.overwrite : BaiduPCS.ondup.newcopy, _selected_account_id);
                //sync_lock.Wait();
                //sync_lock.Reset();

                if (rapid_upload_suc == false)
                {
                    //deleting existed file if overwrite is true
                    if (_overwrite)
                    {
                        _remote_cacher.DeletePathAsync(_remote_path, (suc, data, e) => { }, _selected_account_id);
                    }
                    //pre create file request
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
                        lock (_thread_flag_lock)
                            _upload_thread_flag |= _UPLOAD_THREAD_FLAG_ERROR;
                        return;
                    }
                    #region status check
                    lock (_thread_flag_lock)
                    {
                        if ((_upload_thread_flag & _UPLOAD_THREAD_FLAG_CANCEL_REQUESTED) != 0)
                        {
                            _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_CANCELLED) & ~(_UPLOAD_THREAD_FLAG_CANCEL_REQUESTED | _UPLOAD_THREAD_FLAG_STARTED);
                            return;
                        }
                        if ((_upload_thread_flag & _UPLOAD_THREAD_FLAG_PAUSE_REQUESTED) != 0)
                        {
                            _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_PAUSED) & ~(_UPLOAD_THREAD_FLAG_PAUSE_REQUESTED | _UPLOAD_THREAD_FLAG_STARTED);
                            return;
                        }
                    }
                    #endregion

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
                        lock (_thread_flag_lock)
                        {
                            if ((_upload_thread_flag & _UPLOAD_THREAD_FLAG_CANCEL_REQUESTED) != 0)
                            {
                                _upload_thread_flag = (_upload_thread_flag | _UPLOAD_THREAD_FLAG_CANCELLED) & ~(_UPLOAD_THREAD_FLAG_CANCEL_REQUESTED | _UPLOAD_THREAD_FLAG_STARTED);
                                for (int i = 0; i < _task_id.Length; i++)
                                {
                                    if (_task_id[i] != Guid.Empty)
                                        _remote_cacher.UploadSliceCancelAsync(_task_id[i]);
                                }
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
                                return;
                            }
                            if ((_upload_thread_flag & (_UPLOAD_THREAD_FLAG_ERROR | _UPLOAD_THREAD_FLAG_FILE_MODIFIED)) != 0)
                                {
                                _upload_thread_flag = _upload_thread_flag & ~_UPLOAD_THREAD_FLAG_STARTED;
                                return;
                            }
                        }
                        #endregion

                        for (int i = 0; i < max_thread && _slice_seq.Count > 0; i++)
                        {
                            if (_task_id[i] == Guid.Empty && (DateTime.Now - _last_sent[i]).TotalSeconds > 120)
                            {
                                //error handling for dequeue failure
                                if (_slice_seq.TryDequeue(out _task_seq[i]) == false)
                                {
                                    lock (_thread_flag_lock)
                                        _upload_thread_flag |= _UPLOAD_THREAD_FLAG_ERROR;
                                    return;
                                }
                                _remote_cacher.UploadSliceBeginAsync((ulong)Math.Min(_file_size - BaiduPCS.UPLOAD_SLICE_SIZE * _task_seq[i], BaiduPCS.UPLOAD_SLICE_SIZE), _remote_path, _upload_id, _task_seq[i], _on_slice_upload_request_callback, _selected_account_id, i);
                                _last_sent[i] = DateTime.Now;
                            }
                        }

                        //speed calculation
                        long size = Interlocked.Read(ref _uploaded_size);
                        _upload_size_5s.RemoveFirst();
                        _upload_size_5s.AddLast(size);
                        _average_speed_5s = (_upload_size_5s.Last.Value - _upload_size_5s.First.Value) / 5.0;
                        _average_speed_total = size / (DateTime.Now - _start_time).TotalSeconds;

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
