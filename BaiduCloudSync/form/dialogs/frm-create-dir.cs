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
    public partial class frmCreateDir : Form
    {
        public frmCreateDir(string default_name = null)
        {
            InitializeComponent();

            if (!string.IsNullOrEmpty(default_name))
                tPath.Text = default_name;
        }

        private void frm_create_dir_Load(object sender, EventArgs e)
        {

        }
        private bool _is_valid;
        public string FileName { get { return _is_valid ? tPath.Text : string.Empty; } set { tPath.Text = value; } }

        private void bConfirm_Click(object sender, EventArgs e)
        {
            if (!_is_valid)
            {
                MessageBox.Show(this, "输入的文件夹名字不合法", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                tPath.Focus();
            }
            else
            {
                Close();
            }
        }
        private bool _cancelled;
        public bool Cancelled { get { return _cancelled; } }
        private void bCancel_Click(object sender, EventArgs e)
        {
            _cancelled = true;
            Close();
        }

        private void tPath_TextChanged(object sender, EventArgs e)
        {
            _is_valid = FileListCacher.PathValidating("/" + tPath.Text);
        }

        private void tPath_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r')
            {
                bConfirm.PerformClick();
                e.Handled = true;
            }
        }
    }
}
