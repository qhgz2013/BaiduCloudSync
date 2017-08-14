using BaiduCloudSync.NetUtils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;
namespace BaiduCloudSync
{
    //todo: bug fixed for mismatch MD5
    //and buffering the connection stream -> memory stream (buffering) -> hard disk
    public partial class frmDownload : Form
    {
        private const bool _enable_error_tracing = false;
        public frmDownload(BaiduPCS pcs, BaiduPCS.ObjectMetadata data, string save_path, bool start_task_now = false)
        {
            InitializeComponent();
            if (pcs == null || data.FS_ID == 0) throw new ArgumentNullException();
            if (data.IsDir) throw new InvalidOperationException();
            if (StaticConfig.MAX_DOWNLOAD_THREAD > thdCount.Maximum) thdCount.Maximum = _max_thread;
            thdCount.Value = StaticConfig.MAX_DOWNLOAD_THREAD;
            _max_thread = StaticConfig.MAX_DOWNLOAD_THREAD;

            _pcsAPI = pcs;
            _path = data.Path;
            _fs_id = data.FS_ID;
            _save_path = save_path;
            _status = 4;
            _content_length = data.Size;
            _dispatcher = new TaskDispatcher(_content_length);

            lPath.Text = _path;
            var fileInfo = new FileInfo(_save_path);
            var parent_directory = fileInfo.Directory;
            if (!parent_directory.Exists) parent_directory.Create();
            lContentLength.Text = formatBytes(data.Size);
            lDownloadSize.Text = "0B";
            lSpeed.Text = "0B/s";
            lETA.Text = "?";

            Name = "download_form";
            if (start_task_now) Start();
        }

        #region VARS
        //API
        private BaiduPCS _pcsAPI;
        //文件的网盘路径
        private string _path;
        //文件id
        private ulong _fs_id;
        //保存到本地的id
        private string _save_path;
        //保存到本地的数据流
        private Stream _save_stream;
        private object _stream_lck = new object();
        //文件长度
        private ulong _content_length;
        //最大线程数
        private int _max_thread;
        //每个线程的缓冲区大小
        private const int _BUFFER_SIZE = 16384;
        //分配任务
        private TaskDispatcher _dispatcher;
        private object _data_lck = new object();
        private object _external_lck = new object();
        private bool _form_initialized = false;

        //detail info for each thread
        private string[] _urls;

        //standalone data for each request thread
        private ulong[] _position, _downloaded_bytes;
        //flat status: 0000 00 X(data recving) X(request sent)
        private byte[] _thread_situation;
        private NetStream[] _requests;
        private Guid[] _guid_list;
        private DateTime[] _last_receive;

        private DateTime _start_time;
        private ulong _total_download_size;
        private ulong _last_total_download_size;
        private ulong _speed;
        //flag status : 0000 0X (inited) X (paused) X(calcelled) (0 for running)
        private byte _status;
        private TimeSpan _eta;

        private delegate void NoArgSTA();
        private Thread _background_thread;

        private struct _t_struct
        {
            public Guid id;
            public int index;
        }
        #endregion

        private void frmDownload_Load(object sender, EventArgs e)
        {
            _form_initialized = true; //created main thread for invoking
            if (_status == 0) bStartPause.Text = "暂停下载";
            else if (_status == 2 || _status == 4) bStartPause.Text = "开始下载";

        }
        private void startThdCallback()
        {
            //Tracer.GlobalTracer.TraceInfo("(debug message): startThdCallback...");

            if ((_status & 1) != 0) return;

            try
            {

                //Tracer.GlobalTracer.TraceInfo("(debug message): opening local filestream...");
                lock (_stream_lck)
                {
                    if (_save_stream != null)
                        _save_stream.Close();

                    if (File.Exists(_save_path))
                        _save_stream = new FileStream(_save_path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                    else
                        _save_stream = new FileStream(_save_path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);


                    if (_save_stream.Length > (long)_content_length)
                        _save_stream.SetLength((long)_content_length);

                    if (_save_stream.Length != (long)_content_length)
                    {
                        Tracer.GlobalTracer.TraceInfo("(debug message): setting filestream length to " + _content_length);
                        var sw = new Stopwatch();
                        sw.Start();

                        _save_stream.Seek(0, SeekOrigin.Begin);
                        //zero filling (instead of SetLength, a non-sync operation)
                        long filling_size = 0;
                        var buffer = new byte[65535];
                        do
                        {
                            long delta_size = ((long)_content_length - filling_size);
                            long block_size = delta_size > 65535 ? 65535 : delta_size;

                            _save_stream?.Write(buffer, 0, (int)block_size);
                            filling_size += block_size;
                        } while (filling_size < (long)_content_length);

                        sw.Stop();
                        var ellapsed_ms = sw.ElapsedMilliseconds;
                        var avg_speed = ellapsed_ms == 0 ? _content_length : (_content_length / (ulong)ellapsed_ms * 1000);
                        Tracer.GlobalTracer.TraceInfo("(debug message): completed ( " + ellapsed_ms.ToString("#,##0") + " ms ellapsed, avg speed = " + formatBytes(avg_speed) + "/s)");
                    }
                }
                _start_time = DateTime.Now;

                Tracer.GlobalTracer.TraceInfo("(debug message): fetching download urls");
                string[] urls = null;
                try { urls = _pcsAPI.GetLocateDownloadLink(_path); }
                catch (ErrnoException ex)
                {
                    Tracer.GlobalTracer.TraceError("GetLocateDownloadLink failed: unknown code " + ex.Errno);
                }
                if (urls.Length == 0) { Tracer.GlobalTracer.TraceWarning("GetLocateDownloadLink failed, ignored"); }
                string url0 = null;
                try { url0 = _pcsAPI.GetDownloadLink(_fs_id); }
                catch (ErrnoException ex)
                {
                    Tracer.GlobalTracer.TraceError("GetDownloadLink failed: unknown code " + ex.Errno);
                }
                if (string.IsNullOrEmpty(url0)) { Tracer.GlobalTracer.TraceWarning("GetDownloadLink failed, ignored"); }
                var url1 = _pcsAPI.GetDownloadLink_API(_path);

                int external_count = urls.Length + 2;
                if (string.IsNullOrEmpty(url0)) external_count--;
                if (string.IsNullOrEmpty(url1)) external_count--;
                _urls = new string[external_count];
                int offset = 0;
                if (!string.IsNullOrEmpty(url0)) _urls[offset++] = url0;
                if (!string.IsNullOrEmpty(url1)) _urls[offset++] = url1;
                Array.Copy(urls, 0, _urls, offset, urls.Length);

                if (_urls.Length == 0 && _form_initialized)
                {
                    Invoke(new NoArgSTA(delegate
                    {
                        MessageBox.Show("获取下载地址失败");
                        Close();
                    }));
                    return;
                }

                //Tracer.GlobalTracer.TraceInfo("(debug message): initializing variables array (_max_thread=" + _max_thread + ")");

                lock (_data_lck)
                {
                    _position = new ulong[_max_thread];
                    _downloaded_bytes = new ulong[_max_thread];
                    _thread_situation = new byte[_max_thread];
                    _requests = new NetStream[_max_thread];
                    _guid_list = new Guid[_max_thread];
                    _last_receive = new DateTime[_max_thread];
                    for (int i = 0; i < _max_thread; i++) { _last_receive[i] = _start_time; }
                }

                //开始刷新实时状态
                lock (_data_lck)
                {
                    if (_background_thread != null)
                    {
                        _background_thread.Abort();
                        _background_thread = null;
                    }
                    _background_thread = new Thread(background_thread_callback);
                    _background_thread.Name = "数据更新线程";
                    _background_thread.IsBackground = true;
                    _background_thread.Priority = ThreadPriority.AboveNormal;

                    //Tracer.GlobalTracer.TraceInfo("(debug message): calling data update thread");
                    _background_thread.Start();
                }
            }
            catch (Exception) { }
            finally
            {
                //Tracer.GlobalTracer.TraceInfo("(debug message): startThd exited");
                _startThd = null;
            }
        }

        private void _url_download_callback(NetStream ns, object state)
        {

            int index = ((_t_struct)state).index;
            Guid id = ((_t_struct)state).id;
            if (_guid_list[index] == Guid.Empty || _guid_list[index] != id)
            {
                ns.Close();
                lock (_data_lck)
                {
                    _dispatcher.ReleaseTask(_guid_list[index]);
                    _requests[index] = null;
                    _guid_list[index] = Guid.Empty;
                    _thread_situation[index] = 0;
                }
                return;
            }
            var istream = ns.Stream;
            int nread = 0;
            var buffer = new byte[_BUFFER_SIZE];
            try
            {
                if (ns.HTTP_Response != null && (int)ns.HTTP_Response.StatusCode >= 200 && (int)ns.HTTP_Response.StatusCode < 300)
                {
                    long length = ns.HTTP_Response.ContentLength;
                    var content_range = ns.HTTP_Response.Headers[HttpResponseHeader.ContentRange];
                    var reg = System.Text.RegularExpressions.Regex.Match(content_range, @"bytes\s+(\d+)-(\d+)?/\d+");
                    lock (_data_lck)
                    {
                        if (_thread_situation[index] != 1) throw new ArgumentException("Invalid Situation, expected 1(pending)");
                        if (_guid_list[index] != id) throw new ArgumentException("GUID mismatched");

                        _thread_situation[index] = 2;
                        if (long.Parse(reg.Result("$2")) - long.Parse(reg.Result("$1")) + 1 != length)
                        {
                            //Tracer.GlobalTracer.TraceWarning("[dbg message][" + index + "]" + " length=" + length + " position=" + _position[index] + " range=" + content_range);
                            throw new ArgumentException("数据流长度不匹配: ContentRange");
                        }
                        if (length != -1 && (ulong)length + _position[index] != _content_length)
                        {
                            //Tracer.GlobalTracer.TraceWarning("[dbg message][" + index + "]" + " length=" + length + " position=" + _position[index]);
                            throw new ArgumentException("数据流长度不匹配");
                        }
                    }
                    if (istream != null)
                    {
                        bool is_continue = true;
                        do
                        {
                            nread = istream.Read(buffer, 0, _BUFFER_SIZE);
                            if (nread < 0) throw new ArgumentException("size invalid");
                            //writing data (no Exception allowed)
                            lock (_data_lck)
                            {
                                _last_receive[index] = DateTime.Now;
                                if (_guid_list[index] != Guid.Empty && _guid_list[index] == id)
                                {
                                    lock (_stream_lck)
                                    {
                                        _save_stream.Seek((long)_position[index], SeekOrigin.Begin);
                                        _save_stream.Write(buffer, 0, nread);
                                    }
                                    _position[index] += (uint)nread;
                                    _downloaded_bytes[index] += (uint)nread;
                                    is_continue = _dispatcher.UpdateTaskSituation(_guid_list[index], _position[index]);
                                }
                                else
                                    throw new WebException();
                            }
                        } while (_status == 0 && nread > 0 && is_continue);
                    }
                }
            }
            catch (WebException ex)
            {
#pragma warning disable
                if (_enable_error_tracing)
                    Tracer.GlobalTracer.TraceError("[" + index + "]: " + ex.ToString());
#pragma warning restore
            }
            catch (IOException ex)
            {
                Tracer.GlobalTracer.TraceError("[" + index + "]: " + ex.ToString());
            }
            catch (Exception ex)
            {
                Tracer.GlobalTracer.TraceError("[" + index + "]: " + ex.ToString());
            }
            finally
            {
                ns.Close();
                lock (_data_lck)
                {
                    if (_guid_list[index] != Guid.Empty && _guid_list[index] == id)
                    {
                        _dispatcher.ReleaseTask(_guid_list[index]);
                        _requests[index] = null;
                        _guid_list[index] = Guid.Empty;
                        _thread_situation[index] = 0;
                    }
                }
            }
        }
        private void background_thread_callback(object arg)
        {
            //Tracer.GlobalTracer.TraceInfo("(debug message): background thread started");
            try
            {
                var default_timeout = new TimeSpan(0, 0, 40);
                var next_update = DateTime.Now.AddSeconds(1);
                do
                {
                    //wait for next call
                    var ts = (int)(next_update - DateTime.Now).TotalMilliseconds;
                    if (ts > 0) Thread.Sleep(ts);
                    next_update = next_update.AddSeconds(1);

                    //data parsing
                    var ls = _dispatcher.GetSegments();
                    _speed = _total_download_size - _last_total_download_size;
                    if (_speed < 0) _speed = 0;
                    _last_total_download_size = _total_download_size;
                    _total_download_size = 0;

                    bool has_thd = false;
                    for (int i = 0; i < _requests.Length; i++)
                    {
                        if (_thread_situation[i] != 0)
                        {
                            has_thd = true; break;
                        }
                    }
                    foreach (var item in ls)
                    {
                        _total_download_size += item.Value - item.Key;
                    }
                    var process = _total_download_size * 10000.0 / _content_length;

                    if (_speed == 0)
                        _eta = TimeSpan.MaxValue;
                    else
                        _eta = TimeSpan.FromSeconds(1.0 * (_content_length - _total_download_size) / _speed);

                    //requests auto disconnect (timed out)
                    lock (_data_lck)
                    {
                        for (int i = 0; i < _requests.Length; i++)
                        {
                            if (_thread_situation[i] > 0 && (DateTime.Now - _last_receive[i]) > default_timeout)
                            {
                                //Tracer.GlobalTracer.TraceInfo("Aborting task #" + i + " (recv timed out)");
                                if (_status != 0) break;
                                if (_requests[i] != null)
                                {
                                    try { _requests[i].Close(); } catch (Exception ex) { Tracer.GlobalTracer.TraceError(ex.ToString()); }
                                }
                                _dispatcher.ReleaseTask(_guid_list[i]);
                                //rst
                                _guid_list[i] = Guid.Empty;
                                _requests[i] = null;
                                _thread_situation[i] = 0;
                                _downloaded_bytes[i] = 0;
                                _position[i] = 0;
                                break;
                            }
                        }
                    }
                    //auto starting tasks
                    const int MAX_START_COUNT = 4; //starts 4 tasks in a working loop
                    int started_count = 0;
                    lock (_data_lck)
                    {
                        for (int i = 0; i < _requests.Length && started_count < MAX_START_COUNT; i++)
                        {
                            if (_thread_situation[i] == 0)
                            {
                                if (_status != 0) break;
                                _guid_list[i] = _dispatcher.AllocateNewTask(out _position[i]);
                                if (_guid_list[i] == Guid.Empty) { break; }
                                //Tracer.GlobalTracer.TraceInfo("Starting task #" + i);
                                
                                _requests[i] = new NetStream();
                                //test via local proxy
                                //cn_requests[i].Proxy = new WebProxy("http://localhost:8888/");
                                _thread_situation[i] = 1;
                                _last_receive[i] = DateTime.Now;
                                try
                                {
                                    //Tracer.GlobalTracer.TraceInfo("[dbg message][" + i + "]: fetching pos: " + _position[i]);
                                    _requests[i].HttpGetAsync(_urls[i % _urls.Length], _url_download_callback, new _t_struct { index = i, id = _guid_list[i] }, range: (long)_position[i]);
                                }
                                catch (Exception ex)
                                {
                                    Tracer.GlobalTracer.TraceError(ex.ToString());
                                    _dispatcher.ReleaseTask(_guid_list[i]);
                                    _guid_list[i] = Guid.Empty;
                                    _thread_situation[i] = 0;
                                    _requests[i] = null;
                                }
                                started_count++;
                            }
                        }
                    }

                    //auto disconnect for 1h (token refresh)
                    if (DateTime.Now - _start_time > new TimeSpan(1, 0, 0))
                    {
                        Restart();
                        break;
                    }
                    //ui update
                    #region UI update
                    if (_form_initialized)
                    {
                        Invoke(new NoArgSTA(delegate
                        {

                            int width = pSegments.Width, height = pSegments.Height;
                            if (width <= 0) width = 100;
                            if (height <= 0) height = 10;
                            var bmp = new Bitmap(width, height);
                            var gr = Graphics.FromImage(bmp);
                            //gr.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;


                            //downloaded segments
                            foreach (var item in ls)
                            {
                                float left = 1.0f * item.Key / _content_length * bmp.Width;
                                float right = 1.0f * item.Value / _content_length * bmp.Width;
                                gr.FillRectangle(Brushes.LightSkyBlue, left, 0, right - left, bmp.Height - 1);
                            }

                            for (int i = 0; i < _requests.Length; i++)
                            {
                                if (_thread_situation[i] != 0)
                                {
                                    float left = 1.0f * _position[i] / _content_length * bmp.Width;
                                    gr.DrawLine(Pens.Red, left, 0, left, bmp.Height - 1);
                                }
                                var lvi = new ListViewItem(new string[] { i.ToString(), _urls[i % _urls.Length], formatBytes(_downloaded_bytes[i]), formatBytes(_position[i]) });
                                if (_thread_situation[i] == 0) lvi.SubItems.Add("0 {cancelled}");
                                else if (_thread_situation[i] == 1) lvi.SubItems.Add("1 {pending}");
                                else if (_thread_situation[i] == 2) lvi.SubItems.Add("2 {fetching}");

                                if (lThreadInfo.Items.Count < _requests.Length)
                                    lThreadInfo.Items.Insert(i, lvi);
                                else
                                    lThreadInfo.Items[i] = lvi;
                            }

                            pSegments.Image = bmp;
                            pFinished.Maximum = 10000;
                            pFinished.Value = (int)process;
                            lDownloadSize.Text = formatBytes(_total_download_size) + " (" + (process / 100.0).ToString("0.000") + "%)";
                            lSpeed.Text = formatBytes(Speed) + "/s";
                            lContentLength.Text = formatBytes(_content_length);
                            lETA.Text = _eta == TimeSpan.MaxValue ? "--" : _eta.ToString("g");
                        }));

                    }
                    #endregion

                    //download completed
                    if (_content_length == _total_download_size)
                    {
                        _status = 1;
                        lock (_stream_lck)
                        {
                            _save_stream.Close();
                            _save_stream = null;
                        }
                        TaskFinished?.Invoke(this, new EventArgs());
                        if (_form_initialized)
                        {
                            Invoke(new NoArgSTA(delegate
                            {
                                Close();
                            }));
                        }
                        if (has_thd)
                        {
                            lock (_data_lck)
                                for (int i = 0; i < _requests.Length; i++)
                                {
                                    if (_thread_situation[i] != 0 && _requests[i] != null)
                                    {
                                        _requests[i].Close();
                                    }
                                    _thread_situation[i] = 0;
                                    _requests[i] = null;
                                }
                        }
                        _last_total_download_size = _total_download_size;

                        break;

                    }
                    //paused or cancelled
                    else if (_status != 0)
                    {
                        break;
                    }

                } while (true);
            }
            catch (Exception ex)
            {
                Tracer.GlobalTracer.TraceError(ex.ToString());
            }
            finally
            {
                _background_thread = null;
                //Tracer.GlobalTracer.TraceInfo("(debug message): background thread exited");
            }
        }

        private void bStartPause_Click(object sender, EventArgs e)
        {
            if (_status == 2 || _status == 4)
            {
                //恢复下载
                Start();
            }
            else
            {
                //暂停下载
                Pause();
            }
        }

        private void bCancel_Click(object sender, EventArgs e)
        {
            Cancel();
        }

        private void bHide_Click(object sender, EventArgs e)
        {
            Hide();
        }
        private string formatBytes(ulong _in)
        {
            if (_in < 0x400) return _in + "B";
            if (_in < 0x100000) return (_in / (double)0x400).ToString("0.000") + "KB";
            if (_in < 0x40000000) return (_in / (double)0x100000).ToString("0.000") + "MB";
            return (_in / (double)0x40000000).ToString("0.000") + "GB";
        }

        #region PUBLIC FUNCTIONS
        //external callback
        public event EventHandler TaskStarted, TaskPaused, TaskCancelled, TaskFinished;
        private Thread _startThd;
        /// <summary>
        /// 开始任务
        /// </summary>
        public void Start()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                lock (_external_lck)
                {
                    if (_status == 0 || _status == 1) return;
                    if (_startThd != null)
                    {
                        return;
                    }
                    _startThd = new Thread(new ThreadStart(startThdCallback));
                    _startThd.IsBackground = true;
                    _status = 0;
                    TaskStarted?.Invoke(this, new EventArgs());
                    _startThd.Start();
                    if (_form_initialized)
                    {
                        Invoke(new NoArgSTA(delegate
                        {
                            bStartPause.Text = "暂停下载";
                        }));
                    }
                }
            });
        }
        /// <summary>
        /// 暂停任务
        /// </summary>
        public void Pause()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                lock (_external_lck)
                {
                    if (_status == 2 || _status == 1) return;
                    _status = 2;
                    if (_startThd != null)
                    {
                        try { _startThd.Abort(); } catch (Exception) { }
                    }
                    //中断所有http请求
                    if (_requests != null)
                        lock (_data_lck)
                            for (int i = 0; i < _requests.Length; i++)
                            {
                                if (_requests[i] != null)
                                {
                                    _requests[i].Close();
                                    _requests[i] = null;
                                    _dispatcher.ReleaseTask(_guid_list[i]);
                                    _guid_list[i] = Guid.Empty;
                                    _position[i] = 0;
                                    _downloaded_bytes[i] = 0;
                                }
                            }
                    //关闭输出数据流
                    lock (_stream_lck)
                    {
                        if (_save_stream != null)
                        {
                            _save_stream.Close();
                            _save_stream = null;
                        }
                    }
                    TaskPaused?.Invoke(this, new EventArgs());
                }
                if (_form_initialized)
                {
                    Invoke(new NoArgSTA(delegate
                    {
                        //暂停下载
                        bStartPause.Text = "恢复下载";
                    }));
                }
            });
        }
        /// <summary>
        /// 取消任务
        /// </summary>
        public void Cancel()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                lock (_external_lck)
                {
                    if (_status == 1) return;
                    _status = 1;
                    if (_startThd != null)
                    {
                        try { _startThd.Abort(); } catch (Exception) { }
                    }
                    //中断所有http请求
                    if (_requests != null)
                        lock (_data_lck)
                            for (int i = 0; i < _requests.Length; i++)
                            {
                                if (_requests[i] != null)
                                {
                                    _requests[i].Close();
                                    _dispatcher.ReleaseTask(_guid_list[i]);
                                    _guid_list[i] = Guid.Empty;
                                    _position[i] = 0;
                                    _downloaded_bytes[i] = 0;
                                    _requests[i] = null;
                                }
                            }
                    //关闭输出数据流
                    lock (_stream_lck)
                    {
                        if (_save_stream != null)
                        {
                            _save_stream.Close();
                            _save_stream = null;
                        }
                    }
                    TaskCancelled?.Invoke(this, new EventArgs());
                    if (_form_initialized)
                    {
                        _form_initialized = false;
                        Invoke(new NoArgSTA(delegate
                        {
                            Close();
                        }));
                    }

                    if (File.Exists(_save_path))
                    {
                        try { File.Delete(_save_path); } catch (Exception) { }
                    }
                }
            });
        }
        /// <summary>
        /// 重新开始任务
        /// </summary>
        public void Restart()
        {
            if (_status != 0) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                lock (_external_lck)
                {
                    //pause
                    if (_startThd != null)
                    {
                        try { _startThd.Abort(); } catch (Exception) { }
                    }
                    lock (_data_lck)
                        if (_requests != null)
                            for (int i = 0; i < _requests.Length; i++)
                            {
                                if (_requests[i] != null)
                                {
                                    _requests[i].Close();
                                    _requests[i] = null;
                                }
                            }
                    if (_save_stream != null)
                    {
                        _save_stream.Close();
                        _save_stream = null;
                    }
                    TaskPaused?.Invoke(this, new EventArgs());
                    //start
                    _startThd = new Thread(new ThreadStart(startThdCallback));
                    _startThd.IsBackground = true;
                    _status = 0;
                    TaskStarted?.Invoke(this, new EventArgs());
                    _startThd.Start();
                }
            });
        }
        /// <summary>
        /// 已下载大小
        /// </summary>
        public ulong Downloaded_Size { get { return _total_download_size; } }
        /// <summary>
        /// 速度
        /// </summary>
        public ulong Speed { get { return _speed; } }
        /// <summary>
        /// 开始时间
        /// </summary>
        public DateTime StartTime { get { return _start_time; } }
        /// <summary>
        /// 经过时间
        /// </summary>
        public TimeSpan EllapsedTime { get { return DateTime.Now - _start_time; } }
        /// <summary>
        /// 网盘文件路径
        /// </summary>
        public string Path { get { return _path; } }
        /// <summary>
        /// 本地文件路径
        /// </summary>
        public string SavePath { get { return _save_path; } }
        /// <summary>
        /// 完成度，取值[0,1]。未获得文件大小时为0
        /// </summary>
        public double Finish_Rate
        {
            get
            {
                if (Content_Length == 0)
                    return 0.0;
                else
                    return 1.0 * Downloaded_Size / Content_Length;
            }
        }

        private void thdCount_ValueChanged(object sender, EventArgs e)
        {
            _max_thread = (int)thdCount.Value;
        }

        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName
        {
            get
            {
                var tr_str = _path.TrimEnd('/');
                var strs = tr_str.Split('/');
                return strs[strs.Length - 1];
            }
        }
        private void frmDownload_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_status != 1)
            {
                if (MessageBox.Show(this, "任务正在进行，确定要取消吗？", "注意", MessageBoxButtons.OKCancel) == DialogResult.OK)
                {
                    Cancel();
                    _form_initialized = false;
                }
                else
                {
                    e.Cancel = true;
                }
            }
        }
        /// <summary>
        /// 文件大小
        /// </summary>
        public ulong Content_Length { get { return _content_length; } }
        /// <summary>
        /// 获取/设置最大线程数，注意：最大线程的修改仅在Pause()后，Start()前起效
        /// </summary>
        public int Thread_Count { get { return _max_thread; } set { if (value < 1 || value > 200) return; _max_thread = value; thdCount.Value = _max_thread; } }
        /// <summary>
        /// 任务是否开始
        /// </summary>
        public bool IsStarted { get { return _status == 0; } }
        /// <summary>
        /// 任务是否暂停
        /// </summary>
        public bool IsPaused { get { return _status == 2; } }
        /// <summary>
        /// 任务是否结束
        /// </summary>
        public bool IsCancelled { get { return _status == 1; } }
        /// <summary>
        /// 任务是否已初始化
        /// </summary>
        public bool IsInitialized { get { return _status == 4; } }
        /// <summary>
        /// 剩余时间
        /// </summary>
        public TimeSpan ETA { get { return _eta; } }
        #endregion
    }
}
