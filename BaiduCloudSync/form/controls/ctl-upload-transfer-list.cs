using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using GlobalUtil;

namespace BaiduCloudSync
{

    //代码 -----   投影  ---------        ~~~~转移！！！
    // Code -------  Trace On ------------~~~~~  T R A N S F O R M ! ~~~
    public partial class UploadTransferList : UserControl
    {
        public UploadTransferList()
        {
            _upload_list = new List<Uploader>();
            _index_list = new List<int>();
            _index_counter = 0;
            _status = new List<_STAT>();
            _length_cache = new List<ulong>();
            InitializeComponent();
            MouseWheel += new MouseEventHandler((object _sender, MouseEventArgs _e) =>
            {
                var change = scroll.SmallChange * (_e.Delta > 0 ? -1 : 1);
                int new_val = scroll.Value + change;
                int old_val = scroll.Value;
                if (change > 0 && (new_val + Height - 30 > scroll.Maximum + 1))
                    new_val = scroll.Maximum - Height + 30 + 1;
                if (new_val < 0)
                    new_val = 0;
                if (old_val == new_val) return;
                scroll.Value = new_val;
                scroll_Scroll(_sender, new ScrollEventArgs(change > 0 ? ScrollEventType.SmallIncrement : ScrollEventType.SmallDecrement, old_val, new_val));
            });

            _background_thd = new Thread(_timer_thd_callback);
            _background_thd.Name = "background worker";
            _background_thd.IsBackground = true;
            scroll.LargeChange = Height;
        }

        private void UploadTransferList_Load(object sender, EventArgs e)
        {
            _background_thd.Start();
        }

        private Thread _background_thd;

        private List<Uploader> _upload_list;
        private List<int> _index_list;
        private List<_STAT> _status;
        private List<ulong> _length_cache;
        private enum _STAT
        {
            UNDEFINED, INIT, START, PAUSE, STOP
        }

        private int _index_counter;

        private ulong _uploaded_bytes;
        private ulong _uploading_bytes;
        ulong _total_bytes;

        private int _current_list_size;
        private object _list_lock = new object();

        private void _auto_start_new_tasks()
        {
            for (int i = 0; i < _upload_list.Count & i < StaticConfig.MAX_UPLOAD_PARALLEL_TASK_COUNT; i++)
            {
                if (_status[i] == _STAT.INIT)
                    _upload_list[i].Start();
            }
        }
        private void _updating_tasks()
        {
            //updating old row
            for (int i = 0; i < _current_list_size; i++)
            {
                var explicit_form = _upload_list[i];
                var name = explicit_form.FileName;
                var size = explicit_form.Content_Length;
                var process = explicit_form.Finish_Rate;
                var speed = explicit_form.Speed;
                var uploaded_size = explicit_form.Uploaded_Size;
                var col2_size = (Label)Controls["ctl-" + _index_list[i] + "-2"];
                var col3_progress = (ProgressBar)Controls["ctl-" + _index_list[i] + "-3"];
                var col4_percent = (Label)Controls["ctl-" + _index_list[i] + "-4"];
                var col5_speed = (Label)Controls["ctl-" + _index_list[i] + "-5"];
                col2_size.Text = util.FormatBytes(uploaded_size) + "/" + util.FormatBytes(size);
                col3_progress.Value = (int)(process * 10000);
                col4_percent.Text = (process * 100).ToString("0.000") + "%";
                col5_speed.Text = util.FormatBytes(speed) + "/s";
                if (_length_cache[i] > size)
                {
                    _total_bytes -= _length_cache[i] - size;
                    _length_cache[i] = size;
                }
                else if (_length_cache[i] < size)
                {
                    _total_bytes += size - _length_cache[i];
                    _length_cache[i] = size;
                }
            }
        }
        private void _create_tasks()
        {
            var new_count = _upload_list.Count > StaticConfig.MAX_LIST_SIZE ? StaticConfig.MAX_LIST_SIZE : _upload_list.Count;
            //creating new row
            for (int i = _current_list_size; i < new_count; i++)
            {
                var explicit_form = _upload_list[i];
                var name = explicit_form.FileName;
                var size = explicit_form.Content_Length;
                var process = explicit_form.Finish_Rate;
                var speed = explicit_form.Speed;
                var uploaded_size = explicit_form.Uploaded_Size;

                var index = _index_list[i];
                scroll.Maximum += 35;

                //#1
                var col1_type = new Label();
                col1_type.AutoSize = true;
                col1_type.Width = 385;
                col1_type.Height = 12;
                col1_type.Text = name;
                col1_type.Location = new Point(10, 30 + 35 * i - scroll.Value);
                col1_type.Name = "ctl-" + index + "-1";
                Controls.Add(col1_type);
                //#2
                var col2_size = new Label();
                col2_size.AutoSize = true;
                col2_size.Width = 195;
                col2_size.Height = 12;
                col2_size.Name = "ctl-" + index + "-2";
                col2_size.Location = new Point(400, 30 + 35 * i - scroll.Value);
                col2_size.Text = util.FormatBytes(uploaded_size) + "/" + util.FormatBytes(size);
                Controls.Add(col2_size);
                //#3
                var col3_progress = new ProgressBar();
                col3_progress.Maximum = 10000;
                col3_progress.Value = (int)(process * 10000);
                col3_progress.Height = 10;
                col3_progress.Width = 195;
                col3_progress.Location = new Point(10, 45 + 35 * i - scroll.Value);
                col3_progress.Name = "ctl-" + index + "-3";
                Controls.Add(col3_progress);
                //#4
                var col4_percent = new Label();
                col4_percent.AutoSize = true;
                col4_percent.Text = (process * 100).ToString("0.000") + "%";
                col4_percent.Width = 85;
                col4_percent.Height = 12;
                col4_percent.Location = new Point(210, 45 + 35 * i - scroll.Value);
                col4_percent.Name = "ctl-" + index + "-4";
                Controls.Add(col4_percent);
                //#5
                var col5_speed = new Label();
                col5_speed.AutoSize = true;
                col5_speed.Text = util.FormatBytes(speed) + "/s";
                col5_speed.Width = 95;
                col5_speed.Height = 12;
                col5_speed.Location = new Point(300, 45 + 35 * i - scroll.Value);
                col5_speed.Name = "ctl-" + index + "-5";
                Controls.Add(col5_speed);
                //#6
                var col6_startpause = new Button();
                switch (_status[i])
                {
                    case _STAT.INIT:
                        col6_startpause.Text = "开始";
                        break;
                    case _STAT.PAUSE:
                        col6_startpause.Text = "继续";
                        break;
                    case _STAT.START:
                        col6_startpause.Text = "暂停";
                        break;
                    case _STAT.STOP:
                    case _STAT.UNDEFINED:
                    default:
                        col6_startpause.Text = "????";
                        break;
                }
                col6_startpause.Height = 20;
                col6_startpause.Width = 40;
                col6_startpause.Tag = index;
                col6_startpause.Click += (_sender, _e) =>
                {
                    var frm = _upload_list[_index_list.FindIndex((d) => d == (int)((Button)_sender).Tag)];
                    if (frm.IsStarted) frm.Pause();
                    else if (frm.IsPaused || frm.IsInitialized) frm.Start();
                };
                col6_startpause.Location = new Point(400, 45 + 35 * i - scroll.Value);
                col6_startpause.Name = "ctl-" + index + "-6";
                Controls.Add(col6_startpause);
                //#7
                var col7_cancel = new Button();
                col7_cancel.Text = "取消";
                col7_cancel.Height = 20;
                col7_cancel.Width = 40;
                col7_cancel.Tag = index;
                col7_cancel.Click += (_sender, _e) => { _upload_list[_index_list.FindIndex((d) => d == (int)((Button)_sender).Tag)].Cancel(); };
                col7_cancel.Location = new Point(445, 45 + 35 * i - scroll.Value);
                col7_cancel.Name = "ctl-" + index + "-7";
                Controls.Add(col7_cancel);

            }
            _current_list_size = new_count;
            UploadTransferList_Resize(this, new EventArgs());
        }

        private void _updating_statistics()
        {
            _uploading_bytes = 0;
            ulong downspeed = 0;
            for (int i = 0; i < _upload_list.Count; i++)
            {
                _uploading_bytes += (_upload_list[i]).Uploaded_Size;
                downspeed += (_upload_list[i]).Speed;
            }

            //reset
            if (_upload_list.Count == 0)
            {
                _uploaded_bytes = 0;
                _uploading_bytes = 0;
                _total_bytes = 0;
            }


            lTaskCount.Text = _upload_list.Count.ToString();
            
            int scale_size;
            pFinishRate.Maximum = _convert_ulong_to_fit_int(_total_bytes, out scale_size);
            int value = (int)((_uploaded_bytes + _uploading_bytes) >> scale_size);
            if (value > pFinishRate.Maximum) pFinishRate.Maximum = value;
            pFinishRate.Value = value;

            lDownloadSize.Text = util.FormatBytes(_uploaded_bytes + _uploading_bytes) + "/" + util.FormatBytes(_total_bytes);

            lSpeed.Text = util.FormatBytes(downspeed) + "/s";
        }
        private void _process_tick()
        {
            lock (_list_lock)
            {
                _auto_start_new_tasks();
                Invoke(new ThreadStart(delegate
                {
                    try
                    {
                        _updating_tasks();
                        _create_tasks();
                        _updating_statistics();
                    }
                    catch (Exception) { }
                }));
            }
        }
        private void _on_upload_completed(object sender, UploadResultEventArgs e)
        {
            lock (_list_lock)
            {
                var base_form = (Uploader)sender;
                var index = _upload_list.FindIndex(o => o == base_form);
                if (index == -1) return;
                Invoke(new ThreadStart(delegate
                {
                    _current_list_size--;

                    scroll.Maximum -= 35;
                    var ctl_index = _index_list[index];
                    //移除当前行
                    for (int i = 0; i < 7; i++)
                        Controls.RemoveByKey("ctl-" + ctl_index + "-" + (i + 1));
                    _uploaded_bytes += base_form.Content_Length;
                    _upload_list.RemoveAt(index);
                    _index_list.RemoveAt(index);
                    _status.RemoveAt(index);
                    _length_cache.RemoveAt(index);
                    //上移下面的每一行
                    for (int i = index; i < _current_list_size; i++)
                    {
                        ctl_index = _index_list[i];
                        for (int j = 0; j < 7; j++)
                        {
                            var control = Controls["ctl-" + ctl_index + "-" + (j + 1)];
                            if (control == null) continue;
                            control.Top -= 35;
                        }
                    }
                    _create_tasks();
                    UploadTransferList_Resize(this, new EventArgs());

                    if (base_form.IsFinished && !StaticConfig.IGNORE_UPLOAD_ERROR && e.Code != Uploader.FailCode.SUCCESS)
                    {
                        string reason;
                        switch (e.Code)
                        {
                            case Uploader.FailCode.MD5_CHECK_ERROR:
                                reason = "文件特征码（MD5）不匹配";
                                break;
                            case Uploader.FailCode.LENGTH_CHECK_ERROR:
                                reason = "文件长度不匹配";
                                break;
                            case Uploader.FailCode.FSID_CHECK_ERROR:
                                reason = "文件ID检验错误";
                                break;
                            default:
                                reason = "未知错误";
                                break;
                        }
                        reason += "，是否重新上传";
                        if (StaticConfig.AUTO_REUPLOAD_WHEN_MD5_ERROR || MessageBox.Show(this, reason, "文件上传错误", MessageBoxButtons.YesNo) == DialogResult.Yes)
                        {
                            base_form.Reupload();
                            base_form.Ondup = BaiduPCS.ondup.overwrite;
                            if (base_form.IsInitialized) _status.Add(_STAT.INIT);
                            else if (base_form.IsPaused) _status.Add(_STAT.PAUSE);
                            //else if (base_form.IsCancelled) _status.Add(_STAT.STOP);
                            else if (base_form.IsStarted) _status.Add(_STAT.START);
                            else _status.Add(_STAT.UNDEFINED);

                            _index_list.Add(_index_counter++);
                            _upload_list.Add(base_form);
                            _length_cache.Add(base_form.Content_Length);
                            _total_bytes += base_form.Content_Length;
                            _create_tasks();
                        }
                    }
                }));
            }
        }
        private void _on_upload_cancelled(object sender, EventArgs e)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                _on_upload_completed(sender, new UploadResultEventArgs(null, null, false, Uploader.FailCode.UNKNOWN));
                _uploaded_bytes -= ((Uploader)sender).Content_Length;
            });
        }
        private void _on_upload_started(object sender, EventArgs e)
        {
            var frm = (Uploader)sender;
            var index = _upload_list.FindIndex(d => d == sender);
            if (index == -1) return;
            _status[index] = _STAT.START;
            var ctl_index = _index_list[index];
            Invoke(new ThreadStart(delegate
            {
                var ctl = Controls["ctl-" + ctl_index + "-6"];
                if (ctl != null) { ctl.Text = "暂停"; }
            }));
        }
        private void _on_upload_paused(object sender, EventArgs e)
        {
            var frm = (Uploader)sender;
            var index = _upload_list.FindIndex(d => d == sender);
            if (index == -1) return;
            var ctl_index = _index_list[index];
            _status[index] = _STAT.PAUSE;
            Invoke(new ThreadStart(delegate
            {
                var ctl = Controls["ctl-" + ctl_index + "-6"];
                ctl.Text = "开始";
            }));
        }
        private void _timer_thd_callback()
        {
            try
            {
                var time = DateTime.Now.AddSeconds(1);
                do
                {
                    _process_tick();

                    var ts = (time - DateTime.Now);
                    time = time.AddSeconds(1);
                    if (ts.TotalMilliseconds > 1)
                        Thread.Sleep((int)ts.TotalMilliseconds);
                } while (true);
            }
            catch (Exception ex)
            {
                Tracer.GlobalTracer.TraceError(ex.ToString());
            }
        }

        private int _convert_ulong_to_fit_int(ulong _in, out int scale_size)
        {
            if (_in <= 0x7fffffff) { scale_size = 0; return (int)_in; }
            else if (_in <= 0x7fffffffff) { scale_size = 0x10; return (int)(_in >> 0x10); }
            else if (_in <= 0x7fffffffffff) { scale_size = 0x20; return (int)(_in >> 0x20); }
            else if (_in <= 0x7fffffffffffff) { scale_size = 0x30; return (int)(_in >> 0x30); }
            else { scale_size = 0x40; return (int)(_in >> 0x40); }
        }

        // ****** PUBLIC INTERFACES
        private object _external_lock = new object();
        public int TaskCount { get { return _upload_list.Count; } }
        public void AddTask(Uploader task)
        {
            if (task == null || task.IsCancelled) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                lock (_external_lock)
                {
                    lock (_list_lock)
                    {
                        task.TaskFinished += _on_upload_completed;
                        task.TaskCancelled += _on_upload_cancelled;
                        task.TaskPaused += _on_upload_paused;
                        task.TaskStarted += _on_upload_started;

                        if (task.IsInitialized) _status.Add(_STAT.INIT);
                        else if (task.IsPaused) _status.Add(_STAT.PAUSE);
                        //else if (task.IsCancelled) _status.Add(_STAT.STOP);
                        else if (task.IsStarted) _status.Add(_STAT.START);
                        else _status.Add(_STAT.UNDEFINED);

                        _index_list.Add(_index_counter++);
                        _upload_list.Add(task);
                        _length_cache.Add(task.Content_Length);
                        _total_bytes += task.Content_Length;

                        Invoke(new ThreadStart(delegate
                        {
                            _create_tasks();
                        }));
                    }
                }
            });
        }

        private void UploadTransferList_Resize(object sender, EventArgs e)
        {
            if (Height > 30)
                scroll.LargeChange = Height - 30;
            else
                scroll.LargeChange = 0;

            if (scroll.LargeChange > 30)
                scroll.SmallChange = 30;
            else
                scroll.SmallChange = scroll.LargeChange;

            if (scroll.Value + Height - 30 > scroll.Maximum + 1)
            {
                int val = scroll.Maximum - Height + 30 + 1;
                var old_val = scroll.Value;
                if (val > 0)
                {
                    scroll.Value = val;
                    scroll_Scroll(scroll, new ScrollEventArgs(ScrollEventType.LargeDecrement, old_val, val));
                }
                else
                {
                    scroll.Value = 0;
                    scroll_Scroll(scroll, new ScrollEventArgs(ScrollEventType.LargeDecrement, old_val, 0));
                }
            }
        }

        private void scroll_Scroll(object sender, ScrollEventArgs e)
        {
            foreach (Control item in Controls)
            {
                if (item.Name.Substring(0, 3) == "ctl")
                    item.Top += (e.OldValue - e.NewValue);
            }
        }

        private void bStart_Click(object sender, EventArgs e)
        {
            lock (_external_lock)
            {
                lock (_list_lock)
                {
                    for (int i = 0; i < _upload_list.Count; i++)
                    {
                        if (_status[i] == _STAT.PAUSE)
                            _status[i] = _STAT.INIT;
                    }
                }
            }
        }

        private void bPause_Click(object sender, EventArgs e)
        {
            lock (_external_lock)
            {
                lock (_list_lock)
                {
                    for (int i = 0; i < _upload_list.Count && i < StaticConfig.MAX_UPLOAD_PARALLEL_TASK_COUNT; i++)
                    {
                        _upload_list[i].Pause();
                    }
                }
            }
        }

        private void bCancel_Click(object sender, EventArgs e)
        {
            lock (_external_lock)
            {
                lock (_list_lock)
                {
                    for (int i = 0; i < _upload_list.Count && i < StaticConfig.MAX_UPLOAD_PARALLEL_TASK_COUNT; i++)
                    {
                        _upload_list[i].TaskCancelled -= _on_upload_cancelled;
                        _upload_list[i].Cancel();
                    }
                    _upload_list.Clear();
                    _status.Clear();
                    _index_list.Clear();
                    _current_list_size = 0;
                    _total_bytes = 0;
                    _uploaded_bytes = 0;
                    _uploading_bytes = 0;

                    for (int i = 0; i < Controls.Count; i++)
                    {
                        if (Controls[i].Name.StartsWith("ctl-"))
                        {
                            Controls.RemoveAt(i);
                            i--;
                        }
                    }

                    UploadTransferList_Resize(sender, e);
                }
            }
        }


    }
}
