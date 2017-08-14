using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.IO;

namespace BaiduCloudSync
{

    //代码 -----   投影  ---------        ~~~~转移！！！
    // Code -------  Trace On ------------~~~~~  T R A N S F O R M ! ~~~
    public partial class DownloadTransferList : UserControl
    {
        public DownloadTransferList()
        {
            _download_list = new List<frmDownload>();
            _index_list = new List<int>();
            _index_counter = 0;
            _status = new List<_STAT>();
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
            _background_thd.Start();
            scroll.LargeChange = Height;
        }

        private bool _form_created = false;
        private void DownloadTransferList_Load(object sender, EventArgs e)
        {
            _form_created = true;
        }

        private Thread _background_thd;

        private List<frmDownload> _download_list;
        private List<int> _index_list;
        private List<_STAT> _status;
        private enum _STAT
        {
            UNDEFINED, INIT, START, PAUSE, STOP
        }

        private int _index_counter;

        private ulong _downloaded_bytes;
        private ulong _downloading_bytes;
        private ulong _total_bytes;


        private int _current_list_size;
        private object _list_lock = new object();

        private void _auto_start_new_tasks()
        {
            for (int i = 0; i < _download_list.Count & i < StaticConfig.MAX_DOWNLOAD_PARALLEL_TASK_COUNT; i++)
            {
                if (_status[i] == _STAT.INIT)
                    _download_list[i].Start();
            }
        }
        private void _updating_tasks()
        {
            //updating old row
            for (int i = 0; i < _current_list_size; i++)
            {
                var explicit_form = _download_list[i];
                var name = explicit_form.FileName;
                var size = explicit_form.Content_Length;
                var process = explicit_form.Finish_Rate;
                var speed = explicit_form.Speed;
                var downloaded_size = explicit_form.Downloaded_Size;
                var col2_size = (Label)Controls["ctl-" + _index_list[i] + "-2"];
                var col3_progress = (ProgressBar)Controls["ctl-" + _index_list[i] + "-3"];
                var col4_percent = (Label)Controls["ctl-" + _index_list[i] + "-4"];
                var col5_speed = (Label)Controls["ctl-" + _index_list[i] + "-5"];
                col2_size.Text = util.FormatBytes(downloaded_size) + "/" + util.FormatBytes(size);
                col3_progress.Value = (int)(process * 10000);
                col4_percent.Text = (process * 100).ToString("0.000") + "%";
                col5_speed.Text = util.FormatBytes(speed) + "/s";
            }
        }
        private void _create_tasks()
        {
            var new_count = _download_list.Count > StaticConfig.MAX_LIST_SIZE ? StaticConfig.MAX_LIST_SIZE : _download_list.Count;
            //creating new row
            for (int i = _current_list_size; i < new_count; i++)
            {

                var explicit_form = _download_list[i];
                var name = explicit_form.FileName;
                var size = explicit_form.Content_Length;
                var process = explicit_form.Finish_Rate;
                var speed = explicit_form.Speed;
                var downloaded_size = explicit_form.Downloaded_Size;

                //Tracer.GlobalTracer.TraceInfo("Creating new row for download: #" + i + "(" + name + ")" + "\r\nHeight=" + (30 + 35 * i - scroll.Value) + " (origin=" + (30 + 35 * i) + ", offset=" + scroll.Value + ")");

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
                col2_size.Text = util.FormatBytes(downloaded_size) + "/" + util.FormatBytes(size);
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
                var col6_show = new Button();
                col6_show.Text = "显示";
                col6_show.Height = 20;
                col6_show.Width = 40;
                col6_show.Tag = index;
                col6_show.Click += (_sender, _e) => { _download_list[_index_list.FindIndex((d) => d == (int)((Button)_sender).Tag)].Show(); };
                col6_show.Location = new Point(400, 45 + 35 * i - scroll.Value);
                col6_show.Name = "ctl-" + index + "-6";
                Controls.Add(col6_show);
                //#7
                var col7_startpause = new Button();
                switch (_status[i])
                {
                    case _STAT.INIT:
                        col7_startpause.Text = "开始";
                        break;
                    case _STAT.PAUSE:
                        col7_startpause.Text = "继续";
                        break;
                    case _STAT.START:
                        col7_startpause.Text = "暂停";
                        break;
                    case _STAT.STOP:
                    case _STAT.UNDEFINED:
                    default:
                        col7_startpause.Text = "????";
                        break;
                }
                col7_startpause.Height = 20;
                col7_startpause.Width = 40;
                col7_startpause.Tag = index;
                col7_startpause.Click += (_sender, _e) =>
                {
                    var frm = _download_list[_index_list.FindIndex((d) => d == (int)((Button)_sender).Tag)];
                    if (frm.IsStarted) frm.Pause();
                    else if (frm.IsPaused || frm.IsInitialized) frm.Start();
                };
                col7_startpause.Location = new Point(445, 45 + 35 * i - scroll.Value);
                col7_startpause.Name = "ctl-" + index + "-7";
                Controls.Add(col7_startpause);
                //#8
                var col8_cancel = new Button();
                col8_cancel.Text = "取消";
                col8_cancel.Height = 20;
                col8_cancel.Width = 40;
                col8_cancel.Tag = index;
                col8_cancel.Click += (_sender, _e) => { _download_list[_index_list.FindIndex((d) => d == (int)((Button)_sender).Tag)].Cancel(); };
                col8_cancel.Location = new Point(490, 45 + 35 * i - scroll.Value);
                col8_cancel.Name = "ctl-" + index + "-8";
                Controls.Add(col8_cancel);

            }
            _current_list_size = new_count;
            DownloadTransferList_Resize(this, new EventArgs());
        }

        private void _updating_statistics()
        {
            _downloading_bytes = 0;
            ulong downspeed = 0;
            for (int i = 0; i < _download_list.Count; i++)
            {
                _downloading_bytes += (_download_list[i]).Downloaded_Size;
                downspeed += (_download_list[i]).Speed;
            }

            //reset
            if (_download_list.Count == 0)
            {
                _downloaded_bytes = 0;
                _downloading_bytes = 0;
                _total_bytes = 0;
            }


            lTaskCount.Text = _download_list.Count.ToString();

            int scale_size;
            pFinishRate.Maximum = _convert_ulong_to_fit_int(_total_bytes, out scale_size);
            pFinishRate.Value = (int)((_downloaded_bytes + _downloading_bytes) >> scale_size);

            lDownloadSize.Text = util.FormatBytes(_downloaded_bytes + _downloading_bytes)+ "/" + util.FormatBytes(_total_bytes);

            lSpeed.Text = util.FormatBytes(downspeed) + "/s";
        }
        private void _process_tick()
        {
            lock (_list_lock)
            {
                _auto_start_new_tasks();
                if (_form_created)
                {
                    Invoke(new ThreadStart(delegate
                    {
                        _updating_tasks();
                        _create_tasks();
                        _updating_statistics();
                    }));
                }
            }
        }
        private void _on_download_completed(object sender, EventArgs e)
        {
            lock (_list_lock)
            {
                var base_form = (frmDownload)sender;
                var index = _download_list.FindIndex(o => o == base_form);
                if (_form_created)
                {
                    Invoke(new ThreadStart(delegate
                    {
                        _current_list_size--;

                        scroll.Maximum -= 35;
                        if (index != -1)
                        {
                            var ctl_index = _index_list[index];
                            //移除当前行
                            for (int i = 0; i < 8; i++)
                                Controls.RemoveByKey("ctl-" + ctl_index + "-" + (i + 1));
                            _downloaded_bytes += base_form.Content_Length;
                            _download_list.RemoveAt(index);
                            _index_list.RemoveAt(index);
                            _status.RemoveAt(index);
                            //上移下面的每一行
                            for (int i = index; i < _current_list_size; i++)
                            {
                                ctl_index = _index_list[i];
                                for (int j = 0; j < 8; j++)
                                {
                                    var control = Controls["ctl-" + ctl_index + "-" + (j + 1)];
                                    if (control == null) continue;
                                    control.Top -= 35;
                                }
                            }
                        }
                        _create_tasks();
                        DownloadTransferList_Resize(this, new EventArgs());
                    }));
                }
                else
                {
                    _downloaded_bytes += base_form.Content_Length;
                    if (index != -1)
                    {
                        _download_list.RemoveAt(index);
                        _index_list.RemoveAt(index);
                        _status.RemoveAt(index);
                    }
                }
            }
        }
        private void _on_download_cancelled(object sender, EventArgs e)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                _on_download_completed(sender, e);
                _total_bytes -= ((frmDownload)sender).Content_Length;
                _downloaded_bytes -= ((frmDownload)sender).Content_Length;
            });
        }
        private void _on_download_started(object sender, EventArgs e)
        {
            var frm = (frmDownload)sender;
            var index = _download_list.FindIndex(d => d == sender);
            if (index == -1) return;
            _status[index] = _STAT.START;
            var ctl_index = _index_list[index];
            if (_form_created)
            {
                Invoke(new ThreadStart(delegate
                {
                    var ctl = Controls["ctl-" + ctl_index + "-7"];
                    if (ctl != null) ctl.Text = "暂停";
                }));
            }
        }
        private void _on_download_paused(object sender, EventArgs e)
        {
            var frm = (frmDownload)sender;
            var index = _download_list.FindIndex(d => d == sender);
            if (index == -1) return;
            var ctl_index = _index_list[index];
            _status[index] = _STAT.PAUSE;
            if (_form_created)
            {
                Invoke(new ThreadStart(delegate
                {
                    var ctl = Controls["ctl-" + ctl_index + "-7"];
                    ctl.Text = "开始";
                }));
            }
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
        public void AddTask(frmDownload task)
        {
            if (task == null || task.IsCancelled) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                lock (_external_lock)
                {
                    lock (_list_lock)
                    {
                        task.TaskFinished += _on_download_completed;
                        task.TaskCancelled += _on_download_cancelled;
                        task.TaskPaused += _on_download_paused;
                        task.TaskStarted += _on_download_started;

                        _total_bytes += task.Content_Length;
                        if (task.IsInitialized) _status.Add(_STAT.INIT);
                        else if (task.IsPaused) _status.Add(_STAT.PAUSE);
                        //else if (task.IsCancelled) _status.Add(_STAT.STOP);
                        else if (task.IsStarted) _status.Add(_STAT.START);
                        else _status.Add(_STAT.UNDEFINED);

                        _index_list.Add(_index_counter++);
                        _download_list.Add(task);

                        if (_form_created)
                        {
                            Invoke(new ThreadStart(delegate
                            {
                                _create_tasks();
                            }));
                        }
                    }
                }
            });
        }
        public int TaskCount { get { return _download_list.Count; } }
        public void SaveTasks()
        {
            throw new NotImplementedException();
            lock (_external_lock)
            {
                lock (_list_lock)
                {
                    if (!Directory.Exists(".tasks")) Directory.CreateDirectory(".tasks");
                    for (int i = 0; i < _download_list.Count; i++)
                    {
                        _download_list[i].Pause();
                    }
                }
            }
        }
        private void DownloadTransferList_Resize(object sender, EventArgs e)
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
                    for (int i = 0; i < _download_list.Count; i++)
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
                    for (int i = 0; i < _download_list.Count && i < StaticConfig.MAX_DOWNLOAD_PARALLEL_TASK_COUNT; i++)
                    {
                        _download_list[i].Pause();
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
                    for (int i = 0; i < _download_list.Count && i < StaticConfig.MAX_DOWNLOAD_PARALLEL_TASK_COUNT; i++)
                    {
                        _download_list[i].TaskCancelled -= _on_download_cancelled;
                        _download_list[i].Cancel();
                    }
                    _download_list.Clear();
                    _status.Clear();
                    _index_list.Clear();
                    _current_list_size = 0;

                    for (int i = 0; i < Controls.Count; i++)
                    {
                        if (Controls[i].Name.StartsWith("ctl-"))
                        {
                            Controls.RemoveAt(i);
                            i--;
                        }
                    }
                }
            }
        }
    }
}
