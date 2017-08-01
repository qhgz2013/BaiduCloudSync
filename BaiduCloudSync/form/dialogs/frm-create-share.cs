using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace BaiduCloudSync
{
    public partial class frmCreateShare : Form
    {
        public frmCreateShare(BaiduPCS api, IEnumerable<ulong> fs_ids)
        {
            InitializeComponent();
            _api = api;
            _fs_id = new List<ulong>();
            foreach (var item in fs_ids)
            {
                _fs_id.Add(item);
            }
        }
        public frmCreateShare(BaiduPCS api, ulong fs_id)
        {
            InitializeComponent();
            _api = api;
            _fs_id = new List<ulong>();
            _fs_id.Add(fs_id);
        }
        private BaiduPCS _api;
        private List<ulong> _fs_id;
        private void rbPrivate_CheckedChanged(object sender, EventArgs e)
        {
            label1.Visible = rbPrivate.Checked;
            label2.Visible = rbPrivate.Checked;
            tPassword.Visible = rbPrivate.Checked;
        }

        private void tPassword_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r')
            {
                e.Handled = true;
            }
        }
        private const string _rnd_str = "0123456789abcdefghijklmnopqrstuvwxyz";
        private void _generate_random_password()
        {
            var pwd = string.Empty;
            var rnd = new Random();
            for (int i = 0; i < 4; i++)
            {
                pwd += _rnd_str[rnd.Next(_rnd_str.Length)];
            }
            tPassword.Text = pwd;
        }
        private bool _validate_password()
        {
            if (tPassword.Text.Length != 4) return false;
            for (int i = 0; i < 4; i++)
            {
                if (!_rnd_str.Contains(tPassword.Text.ToLower()[i])) return false;
            }
            return true;
        }
        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private bool _generated = false;
        private void btnConfirm_Click(object sender, EventArgs e)
        {
            if (_generated)
            {
                Close();
                return;
            }

            if (rbPrivate.Checked && !_validate_password())
            {
                MessageBox.Show(this, "密码格式错误了", "Tips");
                return;
            }

            int expire = 0;
            if (rb1d.Checked) expire = 1;
            if (rb7d.Checked) expire = 7;

            btnConfirm.Enabled = false;
            btnConfirm.Text = "创建中...";

            var pwd = tPassword.Text;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    BaiduPCS.ShareData data;
                    if (rbPublic.Checked)
                        data = _api.CreatePublicShare(_fs_id, expire);
                    else
                        data = _api.CreatePrivateShare(_fs_id, pwd, expire);

                    if (string.IsNullOrEmpty(data.Link)) throw new ArgumentNullException("分享链接", "分享链接不可能为空");

                    Invoke(new ThreadStart(delegate
                    {
                        tPassword.Enabled = false;
                        rb1d.Enabled = false;
                        rb7d.Enabled = false;
                        rbEver.Enabled = false;
                        rbPublic.Enabled = false;
                        rbPrivate.Enabled = false;

                        label3.Visible = true;
                        tLongUrl.Visible = true;
                        tShortUrl.Visible = true;
                        linkLabel1.Visible = true;
                        linkLabel2.Visible = true;
                        tLongUrl.Text = data.Link;
                        tShortUrl.Text = data.ShortURL;
                        btnConfirm.Text = "关闭";
                        _generated = true;
                    }));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "天有不测风云，少侠，有错误发生啦！:\r\n" + ex.ToString(), "叕是Error Message!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Invoke(new ThreadStart(delegate { btnConfirm.Text = "确定"; }));
                }
                finally
                {
                    Invoke(new ThreadStart(delegate { btnConfirm.Enabled = true; }));
                }
            });
        }

        private void frmCreateShare_Load(object sender, EventArgs e)
        {
            _generate_random_password();
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Clipboard.SetDataObject(tLongUrl.Text);
        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Clipboard.SetDataObject(tShortUrl.Text);
        }
    }
}
