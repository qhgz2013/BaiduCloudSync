// uploader.cs
//
// 用于上传文件的类
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using static BaiduCloudSync.BaiduPCS;

namespace BaiduCloudSync
{
    public class Uploader
    {
        public enum FailCode
        {
            SUCCESS, UNKNOWN,
            MD5_CHECK_ERROR, LENGTH_CHECK_ERROR, FSID_CHECK_ERROR
        }

        private string _path;
        private string _local_path;

        private Stream _open_stream;

        private Thread _background_thread;
        private Thread _speed_timer; //controlled by background thread
        private object _thd_lock = new object();
        // 000x(finished) x(md5 calculating) x(cancelled) x(paused) x(inited)
        private byte _state;

        private object _external_lock = new object();
        //秒传相关的变量和结果
        private ulong _content_length;
        private string _content_crc32;
        private string _content_md5;
        private string _slice_md5;
        private string _content_sha1;
        private bool _rapid_upload_requested;
        //用于申请上传的变量
        private string _uploadid;
        private int _slice_count;
        private List<string> _slice_upload_data;
        //用于计算速度的变量
        private ulong _last_upload_size;
        private ulong _upload_size;
        private ulong _speed;

        private BaiduPCS _api;
        private ondup _ondup;
        private LocalFileCacher _cache;
        private Form _parent_form;
        private bool _is_encrypt_upload;
        private bool _is_encrypted_data_created;
        public Uploader(Form parent, BaiduPCS api, string path, LocalFileCacher local_cache, string local_path, ondup ondup = ondup.overwrite, bool encrypt = false)
        {
            _path = path;
            _local_path = local_path;
            _parent_form = parent;

            _api = api;
            _ondup = ondup;
            _cache = local_cache;
            _state = 1;
            _content_length = (ulong)(new FileInfo(_local_path).Length);
            _content_crc32 = string.Empty;
            _content_md5 = string.Empty;

            _rapid_upload_requested = false;
            _slice_count = (int)Math.Ceiling(_content_length / 4194304.0);
            _slice_upload_data = new List<string>();

            _is_encrypt_upload = encrypt;
            if (_is_encrypt_upload && !_path.EndsWith(".bcsd"))
                _path += ".bcsd";

        }
        private void onStatusUpdated(string path, string local_path, long current, long length)
        {
            _upload_size = (ulong)(current + _slice_upload_data.Count * 4194304);
            //_content_length = (ulong)length;
            if (_upload_size > _content_length)
            {

            }
        }
        private void _speed_timer_callback()
        {
            try
            {
                do
                {
                    ulong a = _upload_size, b = _last_upload_size; ;
                    if (a > b) _speed = a - b; else _speed = 0;
                    _last_upload_size = _upload_size;
                    Thread.Sleep(1000);
                } while (true);
            }
            finally
            {
                _speed_timer = null;
            }
        }
        private void _background_thread_callback()
        {
            try
            {
                lock (_thd_lock)
                    if (_speed_timer == null)
                    {
                        _speed_timer = new Thread(_speed_timer_callback);
                        _speed_timer.IsBackground = true;
                        _speed_timer.Name = "速度计算线程";
                        _speed_timer.Start();
                    }
                if (_content_length == 0) return;

                //calculating origin file
                _state = 8;
                TrackedData data = new TrackedData();
                lock (_thd_lock)
                {
                    _cache.GetDataFromFile(_local_path);
                }
                if (!data.IsDir)
                {
                    _content_crc32 = data.CRC32;
                    _content_md5 = data.MD5;
                    _content_sha1 = data.SHA1;
                    _content_length = data.ContentSize;
                }

                _encrypt_file();

                //calculating encrypted file (skips the origin file)
                _calculate_md5();


                #region Rapid Upload Test
                if (!_rapid_upload_requested)
                {
                    ObjectMetadata datameta = new ObjectMetadata();
                    //posting rapid upload info
                    if (!string.IsNullOrEmpty(_slice_md5))
                    {
                        try
                        {
                            while (string.IsNullOrEmpty(datameta.MD5))
                            {
                                datameta = _api.RapidUploadRaw(_path, _content_length, _content_md5, _content_crc32, _slice_md5, _ondup);
                                if (string.IsNullOrEmpty(datameta.MD5))
                                {
                                    Tracer.GlobalTracer.TraceWarning("Rapid upload file failed: Network Error, retry in 1 second");
                                    Thread.Sleep(1000);
                                }
                            }
                            _rapid_upload_requested = true;
                        }
                        catch (ErrnoException ex)
                        {
                            Tracer.GlobalTracer.TraceError("Rapid upload file failed, response returned errno = " + ex.Errno);
                            _parent_form?.Invoke(new ThreadStart(delegate
                            {
                                MessageBox.Show(_parent_form, "秒传错误: 错误代码: " + ex.Errno, "出错了", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }));
                        }
                    }
                    if (datameta.FS_ID != 0)
                    {
                        //rapid upload succeeded, thread exited
                        _background_thread = null;
                        _state = 16;
                        TaskFinished?.Invoke(this, new UploadResultEventArgs(_path, _local_path, true, FailCode.SUCCESS));
                        return;
                    }
                }
                #endregion

                //upload begins
                _state = 0;

                //deleting exist file for overwrite: slice upload does not support the overwrite characteristic
                if (_ondup == ondup.overwrite)
                {
                    _api.DeletePath(_path);
                }

                //pre-creating file
                while (string.IsNullOrEmpty(_uploadid))
                {
                    try
                    {
                        _uploadid = _api.PreCreateFile(_path, _slice_count).UploadId;
                        if (string.IsNullOrEmpty(_uploadid))
                        {
                            Tracer.GlobalTracer.TraceWarning("Pre-creating upload file failed: Network Error, retry in 1 second");
                            Thread.Sleep(1000);
                        }
                    }
                    catch (ErrnoException ex)
                    {
                        Tracer.GlobalTracer.TraceError("Pre-create file failed, response returned errno = " + ex.Errno);
                        _state = 4;
                        TaskCancelled?.Invoke(this, new EventArgs());
                        return;
                    }
                }

                lock (_thd_lock)
                {
                    _open_stream = new FileStream(_local_path + (_is_encrypt_upload ? "_temp" : ""), FileMode.Open, FileAccess.Read, FileShare.Read);
                }
                //upload slice
                while (_open_stream != null && _slice_upload_data.Count < _slice_count)
                {
                    lock (_thd_lock)
                    {
                        if (_open_stream == null) break;
                        if (_open_stream.Position != 4194304 * _slice_upload_data.Count)
                            _open_stream.Seek(4194304 * _slice_upload_data.Count, SeekOrigin.Begin);
                    }
                    string data_md5 = null;
                    try { data_md5 = _api.UploadSliceRaw(_open_stream, _path, _uploadid, _slice_upload_data.Count, onStatusUpdated); }
                    catch (ErrnoException ex)
                    {
                        Tracer.GlobalTracer.TraceError("Upload failed, response returned errno = " + ex.Errno);
                        _state = 4;
                        TaskCancelled?.Invoke(this, new EventArgs());
                        return;
                    }
                    catch (Exception)
                    {
                        throw;
                    }

                    if (!string.IsNullOrEmpty(data_md5))
                        _slice_upload_data.Add(data_md5);
                    else
                    {
                        Tracer.GlobalTracer.TraceWarning("Upload failed: Network Error, retry in 1 second");
                        Thread.Sleep(1000);
                    }
                }

                ObjectMetadata dat = new ObjectMetadata();
                while (_slice_upload_data.Count == _slice_count && dat.FS_ID == 0)
                {
                    try
                    {
                        dat = _api.CreateSuperFile(_path, _uploadid, _slice_upload_data, _content_length);
                    }
                    catch (ErrnoException ex)
                    {
                        Tracer.GlobalTracer.TraceError("Create super file failed, response returned errno = " + ex.Errno);
                        _state = 4;
                        TaskCancelled?.Invoke(this, new EventArgs());
                        return;
                    }
                    if (dat.FS_ID == 0)
                    {
                        Tracer.GlobalTracer.TraceWarning("Create super file failed: Network Error, retry in 1 second");
                        Thread.Sleep(1000);
                    }
                }

                //upload finished, checking file meta
                _state = 16;
                if (dat.Size != _content_length)
                {
                    Tracer.GlobalTracer.TraceWarning("[LENGTH CHECK]: Upload file Length mismatch! response returned " + dat.Size + " (expected: " + _content_length + ")");
                    TaskFinished?.Invoke(this, new UploadResultEventArgs(_path, _local_path, false, FailCode.LENGTH_CHECK_ERROR));
                }
                else if (!string.IsNullOrEmpty(_content_md5) && dat.MD5 != _content_md5)
                {
                    Tracer.GlobalTracer.TraceWarning("[MD5 CHECK]: Upload file MD5 mismatch! response returned " + dat.MD5 + " (expected: " + _content_md5 + "), trying to resolve using RapidUpload Test");
                    //resolve md5 error by using RapidUpload
                    if (!string.IsNullOrEmpty(_slice_md5))
                    {
                        ObjectMetadata test_data = new ObjectMetadata();
                        Thread.Sleep(3000);
                        int retry_time = 0;
                        while (string.IsNullOrEmpty(test_data.MD5) || test_data.MD5 == "404")
                        {
                            test_data = _api.RapidUploadRaw(_path, _content_length, _content_md5, _content_crc32, _slice_md5, ondup.overwrite);
                            if (string.IsNullOrEmpty(test_data.MD5))
                            {
                                Tracer.GlobalTracer.TraceWarning("Rapid upload file failed: Network Error, retry in 1 second");
                                Thread.Sleep(1000);
                            }
                            else if (test_data.MD5 == "404")
                            {
                                if (retry_time++ < 5)
                                {
                                    Tracer.GlobalTracer.TraceWarning("Rapid upload file failed: Not Found, retry in 10 seconds (" + retry_time + "/5)");
                                    Thread.Sleep(10000);
                                }
                                else
                                    break;
                            }
                        }
                        if (!string.IsNullOrEmpty(test_data.MD5) && test_data.MD5 == "404")
                        {
                            Tracer.GlobalTracer.TraceWarning("[MD5 CHECK]: Upload file MD5 mismatch! response returned " + dat.MD5 + " (expected: " + _content_md5 + "), resolve failed: Origin file not found");
                            TaskFinished?.Invoke(this, new UploadResultEventArgs(_path, _local_path, false, FailCode.MD5_CHECK_ERROR));
                        }
                        else if (test_data.Size != _content_length)
                        {
                            Tracer.GlobalTracer.TraceWarning("[LENGTH CHECK]: Upload file Length mismatch! response returned " + test_data.Size + " (expected: " + _content_length + ")");
                            TaskFinished?.Invoke(this, new UploadResultEventArgs(_path, _local_path, false, FailCode.LENGTH_CHECK_ERROR));
                        }
                        else if (!string.IsNullOrEmpty(test_data.MD5) && test_data.MD5 != _content_md5)
                        {
                            Tracer.GlobalTracer.TraceWarning("[MD5 CHECK]: Upload file MD5 mismatch! response returned " + test_data.MD5 + " (expected: " + _content_md5 + ")");
                            TaskFinished?.Invoke(this, new UploadResultEventArgs(_path, _local_path, false, FailCode.MD5_CHECK_ERROR));
                        }
                        else
                        {
                            TaskFinished?.Invoke(this, new UploadResultEventArgs(_path, _local_path, true, FailCode.SUCCESS));
                        }
                    }
                }
                else
                {
                    TaskFinished?.Invoke(this, new UploadResultEventArgs(_path, _local_path, true, FailCode.SUCCESS));
                }

                _background_thread = null;
            }
            catch (Exception ex)
            {
                Tracer.GlobalTracer.TraceError(ex.ToString());
            }
            finally
            {
                lock (_thd_lock)
                {
                    if (_background_thread == null || _background_thread == Thread.CurrentThread)
                    {
                        if (_speed_timer != null)
                        {
                            _speed_timer.Abort();
                            _speed_timer = null;
                        }
                        if (_open_stream != null)
                        {
                            _open_stream.Close();
                            _open_stream = null;
                        }
                        if (_is_encrypt_upload && (_state == 4 || _state == 16))
                        {
                            //这里只是在上传完成和取消时删除
                            if (File.Exists(_local_path + "_temp"))
                                File.Delete(_local_path + "_temp");
                        }
                    }
                }

            }

        }
        private void _encrypt_file()
        {
            try
            {

                //encryption
                if (_is_encrypt_upload && (!_is_encrypted_data_created || !File.Exists(_local_path + "_temp")))
                {
                    if (frmKeyCreate.HasRsaPublicKey)
                    {
                        try
                        {
                            var key = frmKeyCreate.LoadRsaKey(false);
                            lock (_thd_lock)
                            {
                                FileEncrypt.EncryptFile(_local_path, _local_path + "_temp", key, _content_sha1);
                            }
                            _is_encrypted_data_created = true;
                        }
                        catch (Exception ex)
                        {
                            throw;
                        }
                    }
                    else if (frmKeyCreate.HasAesKey)
                    {
                        try
                        {
                            byte[] key, iv;
                            frmKeyCreate.LoadAesKey(out key, out iv);
                            if (key == null) throw new ArgumentNullException("key", "加载AES key失败：无效的密钥文件");
                            lock (_thd_lock)
                            {
                                FileEncrypt.EncryptFile(_local_path, _local_path + "_temp", key, iv, _content_sha1);
                            }
                        }
                        catch (Exception ex)
                        {
                            throw;
                        }
                    }
                    else
                    {
                        Tracer.GlobalTracer.TraceWarning("RSA and AES key not found, upload cancelled");
                        _state = 4;
                        TaskCancelled?.Invoke(this, new EventArgs());
                        return;
                    }
                }
                if (_is_encrypt_upload)
                {
                    _content_md5 = string.Empty;
                    _content_length = (ulong)new FileInfo(_local_path + "_temp").Length;
                    _slice_count = (int)Math.Ceiling(_content_length / 4194304.0);
                    _slice_md5 = string.Empty;
                    _content_crc32 = string.Empty;
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        private void _calculate_md5()
        {
            try
            {
                TrackedData data;
                if (string.IsNullOrEmpty(_content_md5) || string.IsNullOrEmpty(_content_crc32))
                {
                    lock (_thd_lock)
                    {
                        data = _cache.GetDataFromFile(_local_path + (_is_encrypt_upload ? "_temp" : ""));
                    }
                    _content_length = data.ContentSize;
                    _content_crc32 = data.CRC32;
                    _content_md5 = data.MD5;
                    _content_sha1 = data.SHA1;
                    _slice_count = (int)Math.Ceiling(_content_length / 4194304.0);
                }
                if (string.IsNullOrEmpty(_slice_md5) && _content_length >= 262144)
                {
                    lock (_thd_lock)
                    {
                        var stream = new FileStream(_local_path + (_is_encrypt_upload ? "_temp" : ""), FileMode.Open, FileAccess.Read);
                        var md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
                        int buffer_size = 8192;
                        var buffer = new byte[buffer_size];
                        int rb = 0, tb = 0;
                        do
                        {
                            rb = stream.Read(buffer, 0, buffer_size);
                            if (rb + tb > 262144) rb = 262144 - tb;
                            md5.TransformBlock(buffer, 0, rb, buffer, 0);
                            tb += rb;
                        } while (rb != 0 && tb < 262144);
                        stream.Close();

                        md5.TransformFinalBlock(buffer, 0, 0);
                        var slice_md5 = md5.Hash;
                        _slice_md5 = util.Hex(slice_md5);
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        public void Start()
        {
            if (_state != 2 && _state != 1) return;
            lock (_external_lock)
            {
                _state = 0;
                _background_thread = new Thread(_background_thread_callback);
                _background_thread.IsBackground = true;
                _background_thread.Name = "上传线程";
                _background_thread.Start();
                TaskStarted?.Invoke(this, new EventArgs());
            }
        }
        public void Pause()
        {
            if (_state != 0 && _state != 8) return;
            lock (_external_lock)
            {
                _state = 2;
                if (_background_thread != null)
                    _background_thread.Abort();
                _background_thread = null;
                _upload_size = (ulong)(4194304 * _slice_upload_data.Count);
                _last_upload_size = _upload_size;
                lock (_thd_lock)
                {
                    if (_open_stream != null)
                    {
                        _open_stream.Close();
                        _open_stream = null;
                    }
                }
                TaskPaused?.Invoke(this, new EventArgs());
            }
        }

        public void Cancel()
        {
            if (_state == 4 || _state == 16) return;
            lock (_external_lock)
            {
                _state = 4;
                if (_background_thread != null)
                {
                    try
                    {
                        _background_thread.Abort();
                    }
                    catch (Exception)
                    {
                    }
                    finally
                    {
                        _background_thread = null;
                    }
                }
                lock (_thd_lock)
                {
                    if (_open_stream != null)
                    {
                        _open_stream.Close();
                        _open_stream = null;
                    }
                    //if (File.Exists(_local_path + "_temp"))
                    //    File.Delete(_local_path + "_temp");
                }
                TaskCancelled?.Invoke(this, new EventArgs());
            }
        }
        public void Reupload()
        {
            if (_state != 4 && _state != 16)
            {
                Cancel();
            }
            lock (_external_lock)
            {
                _state = 1;
                _content_crc32 = string.Empty;
                _content_md5 = string.Empty;
                _slice_md5 = string.Empty;
                _content_length = (ulong)(new FileInfo(_local_path).Length);
                _rapid_upload_requested = false;
                _slice_count = (int)Math.Ceiling(_content_length / 4194304.0);
                _slice_upload_data = new List<string>();
                _uploadid = string.Empty;
                _upload_size = 0;
            }
        }
        public bool IsStarted { get { return _state == 0 || _state == 8; } }
        public bool IsPaused { get { return _state == 2; } }
        public bool IsCancelled { get { return _state == 4; } }
        public bool IsCalculatingMD5 { get { return _state == 8; } }
        public bool IsFinished { get { return _state == 16; } }
        public bool IsInitialized { get { return _state == 1; } }
        public ulong Uploaded_Size { get { return _upload_size; } }
        public ulong Content_Length { get { return _content_length; } }
        public string FileMD5 { get { return _content_md5; } }
        public string FileName { get { return _path.Split('/').Last(); } }
        public double Finish_Rate { get { return (_content_length == 0) ? 0 : (1.0 * _upload_size / _content_length); } }

        public ulong Speed { get { return _speed; } }
        public ondup Ondup { get { return _ondup; } set { _ondup = value; } }
        public string Path { get { return _path; } set { _path = value; } }
        public event EventHandler TaskStarted, TaskPaused, TaskCancelled;

        public event EventHandler<UploadResultEventArgs> TaskFinished;
    }
    public class UploadResultEventArgs : EventArgs
    {
        public readonly string RemotePath;
        public readonly string LocalPath;
        public readonly bool Succeeded;
        public readonly Uploader.FailCode Code;
        public UploadResultEventArgs(string _remote_path, string _local_path, bool _succeeded, Uploader.FailCode code)
        {
            RemotePath = _remote_path;
            LocalPath = _local_path;
            Succeeded = _succeeded;
            Code = code;
        }
    }

}
