using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace BaiduCloudSync
{
    public partial class frmOverwrite : Form
    {
        public frmOverwrite(string path = null)
        {
            InitializeComponent();
            _path = path;
        }
        private string _path;
        private void frmOverwrite_Load(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_path))
                lText.Text = "文件已存在，是否覆盖原文件";
            else
                lText.Text = "文件 " + _path + " 已存在，是否覆盖原文件";
            if (lText.PreferredWidth + 50 > Width)
                Width = lText.PreferredWidth + 50;
        }

        private bool _cancelled;
        private bool _confirmed;
        private bool _always;
        private bool _save_as_new;
        /// <summary>
        /// 是否取消
        /// </summary>
        public bool Cancelled { get { return _cancelled; } }
        /// <summary>
        /// 是否确认覆盖原文件
        /// </summary>
        public bool Confirmed { get { return _confirmed; } }
        /// <summary>
        /// 是否勾选“总是”，跳过询问
        /// </summary>
        public bool Always { get { return _always; } }
        /// <summary>
        /// 是否保存为新的文件
        /// </summary>
        public bool SaveAsNew { get { return _save_as_new; } }

        private void cAlways_CheckedChanged(object sender, EventArgs e)
        {
            _always = cAlways.Checked;
        }

        private void bYes_Click(object sender, EventArgs e)
        {
            _confirmed = true;
            _cancelled = false;
            _save_as_new = false;
            Close();
        }

        private void bNo_Click(object sender, EventArgs e)
        {
            _confirmed = false;
            _cancelled = false;
            _save_as_new = false;
            Close();
        }

        private void bCancel_Click(object sender, EventArgs e)
        {
            _confirmed = false;
            _cancelled = false;
            _save_as_new = false;
            Close();
        }

        private void bNewFile_Click(object sender, EventArgs e)
        {
            _confirmed = false;
            _cancelled = false;
            _save_as_new = true;
            Close();
        }
    }
}
