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

namespace BaiduCloudSync
{
    public class Uploader
    {
        //private const int _MAX_SEGMENT_DATA = 0x400000; //每个分段为65MB
        private string _path;
        private string _local_path;

        private Stream _open_stream;

        private Thread _background_thread;
        private Thread _speed_timer; //controlled by background thread
        private object _thd_lock = new object();
        // 0000 x(inited) x(cancelled) x(data posting) x(md5 calculating)
        private byte _state;

        private object _external_lock = new object();

        private ulong _content_length;
        private string _content_crc32;
        private string _content_md5;
        private string _slice_md5;

        private ulong _last_upload_size;
        private ulong _upload_size;

        private BaiduPCS _api;
        private BaiduPCS.ondup _ondup;
        public Uploader(BaiduPCS api, string path, string local_path, BaiduPCS.ondup ondup = BaiduPCS.ondup.overwrite)
        {
            _path = path;
            _local_path = local_path;

            _api = api;
            _ondup = ondup;
            _state = 8;
            _content_length = (ulong)(new FileInfo(local_path)).Length;
            _content_crc32 = string.Empty;
            _content_md5 = string.Empty;
        }
        private void onStatusUpdated(string path, string local_path, long current, long length)
        {
            _upload_size = (ulong)current;
            _content_length = (ulong)length;
        }
        private void _speed_timer_callback()
        {
            try
            {
                do
                {
                    _last_upload_size = _upload_size;
                    Thread.Sleep(1000);
                } while (true);
            }
            catch
            {

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
                _speed_timer = new Thread(_speed_timer_callback);
                _speed_timer.IsBackground = true;
                _speed_timer.Name = "速度计算线程";
                _speed_timer.Start();

                //calculating local file
                var rapid_upload_data = _api.GetRapidUploadArguments(_local_path, onStatusUpdated);
                _content_length = rapid_upload_data.content_length;
                _content_crc32 = rapid_upload_data.content_crc32;
                _content_md5 = rapid_upload_data.content_md5;
                _slice_md5 = rapid_upload_data.slice_md5;

                BaiduPCS.ObjectMetadata data = new BaiduPCS.ObjectMetadata();
                //posting rapid upload info
                if (!string.IsNullOrEmpty(_slice_md5))
                {
                    data = _api.RapidUploadRaw(_path, _content_length, _content_md5, _content_crc32, _slice_md5, _ondup);
                }

                if (data.FS_ID != 0)
                {
                    //rapid upload succeeded, thread exited
                    _background_thread = null;
                    _state = 0;
                    TaskFinished?.Invoke(this, new UploadResultEventArgs(_path, _local_path, true));
                }
                else
                {
                    //upload begins
                    _state = 2;

                    _open_stream = new FileStream(_local_path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    data = _api.UploadRaw(_open_stream, _content_length, _path, _ondup, onStatusUpdated);

                    if (data.FS_ID == 0)
                    {
                        //upload failed
                        Tracer.GlobalTracer.TraceWarning("Upload failed, response returned FS_ID = 0 (possibly a bug)");
                        TaskFinished?.Invoke(this, new UploadResultEventArgs(_path, _local_path, false));
                    }
                    else
                    {
                        //checking info
                        if (data.MD5 != _content_md5)
                        {
                            Tracer.GlobalTracer.TraceWarning("Upload file MD5 mismatch! response returned " + data.MD5 + " (expected: " + _content_md5 + ")");
                            TaskFinished?.Invoke(this, new UploadResultEventArgs(_path, _local_path, false));
                        }
                        else if (data.Size != _content_length)
                        {
                            Tracer.GlobalTracer.TraceWarning("Upload file Length mismatch! response returned " + data.Size + " (expected: " + _content_length + ")");
                            TaskFinished?.Invoke(this, new UploadResultEventArgs(_path, _local_path, false));
                        }

                        TaskFinished?.Invoke(this, new UploadResultEventArgs(_path, _local_path, true));
                    }
                    _state = 0;
                    _background_thread = null;
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                if (_speed_timer != null)
                    _speed_timer.Abort();
                if (_open_stream != null)
                    _open_stream.Close();
            }

        }
        public void Start()
        {
            if (_state != 8) return;
            lock (_external_lock)
            {
                _state = 1;
                _background_thread = new Thread(_background_thread_callback);
                _background_thread.IsBackground = true;
                _background_thread.Name = "上传线程";
                _background_thread.Start();
                TaskStarted?.Invoke(this, new EventArgs());
            }
        }
        public void Pause()
        {
            Tracer.GlobalTracer.TraceWarning("Pausing while uploading is not support yet!");
        }

        public void Cancel()
        {
            if (_state == 4) return;
            lock (_external_lock)
            {
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
                        _state = 4;
                    }
                }
                TaskCancelled?.Invoke(this, new EventArgs());
            }
        }

        public bool IsStarted { get { return _state == 1; } }
        public bool IsPaused { get { return false; } }
        public bool IsCancelled { get { return _state == 4; } }
        public bool IsInitialized { get { return _state == 8; } }
        public ulong Uploaded_Size { get { return _upload_size; } }
        public ulong Content_Length { get { return _content_length; } }
        public string FileMD5 { get { return _content_md5; } }
        public string FileName { get { return _path.Split('/').Last(); } }
        public double Finish_Rate { get { return (_content_length == 0) ? 0 : (1.0 * _upload_size / _content_length); } }

        public ulong Speed { get { return (_upload_size >= _last_upload_size) ? (_upload_size - _last_upload_size) : 0; } }
        public event EventHandler TaskStarted, TaskPaused, TaskCancelled;

        public event EventHandler<UploadResultEventArgs> TaskFinished;
    }
    public class UploadResultEventArgs : EventArgs
    {
        public readonly string RemotePath;
        public readonly string LocalPath;
        public readonly bool Succeeded;
        public UploadResultEventArgs(string _remote_path, string _local_path, bool _succeeded)
        {
            RemotePath = _remote_path;
            LocalPath = _local_path;
            Succeeded = _succeeded;
        }
    }

}
