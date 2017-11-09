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
    public partial class CtlDebugOutput : UserControl
    {
        public CtlDebugOutput()
        {
            InitializeComponent();
        }

        private void listView1_Resize(object sender, EventArgs e)
        {
            //if (listView1.Width > 4) listView1.Columns[0].Width = listView1.Width - 4;
            //else listView1.Columns[0].Width = 0;
        }
        private void ctl_debug_output_Load(object sender, EventArgs e)
        {
            Tracer.GlobalTracer.InfoTraced += _on_trace_info;
            Tracer.GlobalTracer.WarningTraced += _on_trace_warning;
            Tracer.GlobalTracer.ErrorTraced += _on_trace_error;
        }
        //private object _internal_lock = new object();
        private void _on_trace(string dbg_info, Color forecolor)
        {
            var lines = dbg_info.Replace("\r", "").Split(new char[] { '\n' });
            var lvis = new ListViewItem[lines.Length];
            for (int i = 0; i < lvis.Length; i++)
            {
                lvis[i] = new ListViewItem(lines[i]);
                lvis[i].ForeColor = forecolor;
            }
            try
            {
                Invoke(new ThreadStart(delegate
                {
                    listView1.Items.AddRange(lvis);
                    while (listView1.Items.Count > StaticConfig.MAX_DEBUG_OUTPUT_COUNT)
                    {
                        listView1.Items.RemoveAt(0);
                    }
                    if (listView1.Items.Count > 0) listView1.Items[listView1.Items.Count - 1].EnsureVisible();
                }));
            }
            catch (Exception) { }
        }
        private void _on_trace_info(string dbg_info)
        {
            _on_trace(dbg_info, Color.Black);
        }
        private void _on_trace_warning(string dbg_info)
        {
            _on_trace(dbg_info, Color.Orange);
        }
        private void _on_trace_error(string dbg_info)
        {
            _on_trace(dbg_info, Color.Red);
        }
    }
}
