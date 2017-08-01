using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;

namespace BaiduCloudSync
{
    public partial class frmLogin : Form
    {
        public frmLogin()
        {
            InitializeComponent();
        }
        private Thread _bgThread;
        private void button1_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(textBox1.Text))
            {
                MessageBox.Show(this, "请输入账号名称", "纳……纳尼？", MessageBoxButtons.OK);
                return;
            }
            if (string.IsNullOrEmpty(textBox2.Text))
            {
                MessageBox.Show(this, "请输入密码", "纳……纳尼？", MessageBoxButtons.OK);
                return;
            }
            button1.Enabled = false;
            _bgThread = new Thread(
                (object arg) =>
                {
                    suc = BaiduOAuth.Login(textBox1.Text, textBox2.Text);
                    _bgThread = null;
                });
            _bgThread.Start();
        }
        private void frmLogin_Load(object sender, EventArgs e)
        {
            BaiduOAuth.LoginFailed += on_login_failed;
            BaiduOAuth.LoginSucceeded += on_login_succeeded;
            BaiduOAuth.LoginCaptchaRequired += on_captcha_required;
            BaiduOAuth.LoginExceptionRaised += on_login_exception_raised;
        }

        private void on_login_exception_raised()
        {
            this.Invoke(new ThreadStart(delegate
            {
                button1.Enabled = true;
                MessageBox.Show(this, "哇的一下，错误就来了，请稍后再来试试吧:\r\n" + BaiduOAuth.GetLastFailedReason, "Emmmmmm....", MessageBoxButtons.OK);
            }));
        }
        private void on_login_failed()
        {
            this.Invoke(new ThreadStart(delegate
            {
                button1.Enabled = true;
                if (BaiduOAuth.GetLastFailedCode != 257)
                    MessageBox.Show(this, "登陆失败啦！！:\n" + BaiduOAuth.GetLastFailedReason, "纳……纳尼？", MessageBoxButtons.OK);
            }));
        }
        private bool suc = false;
        public bool LoginSucceeded { get { _bgThread?.Join(); return suc; } }
        private void on_login_succeeded()
        {
            this.Invoke(new ThreadStart(delegate { this.Close(); }));

        }
        Guid captcha_id;
        private void on_captcha_required()
        {
            this.Invoke(new ThreadStart(delegate
            {
                label3.Visible = true;
                pictureBox1.Visible = true;
                textBox3.Visible = true;
                label4.Visible = true;
                linkLabel1.Visible = true;
            }));

            var async_id = Guid.NewGuid();
            captcha_id = async_id;

            ThreadPool.QueueUserWorkItem(
                (object arg) =>
                {
                    Image img = (pictureBox1.Image == null) ? BaiduOAuth.GetCaptcha() : BaiduOAuth.RefreshCaptcha();
                    if (captcha_id == async_id)
                    {
                        this.Invoke(new ThreadStart(
                            delegate { if (pictureBox1.Image != null) pictureBox1.Image.Dispose(); pictureBox1.Image = img; textBox3.Focus(); }
                            ));
                    }
                });
        }
        private void button2_Click(object sender, EventArgs e)
        {
            suc = false;
            this.Close();
        }

        Guid captcha_check_id;
        private void textBox3_Leave(object sender, EventArgs e)
        {

            var async_id = Guid.NewGuid();
            captcha_check_id = async_id;
            label4.Text = "验证中";
            label4.ForeColor = Color.DarkOrange;

            ThreadPool.QueueUserWorkItem(
                (object arg) =>
                {
                    bool valid = BaiduOAuth.CheckVCode(textBox3.Text);
                    if (captcha_check_id == async_id)
                    {
                        this.Invoke(new ThreadStart(
                            delegate
                            {
                                if (valid)
                                {
                                    label4.Text = "正确";
                                    label4.ForeColor = Color.Green;
                                }
                                else
                                {
                                    label4.Text = "错误";
                                    label4.ForeColor = Color.Red;
                                }
                            }
                            ));
                    }
                });
        }

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r')
            {
                textBox2.Focus();
                e.Handled = true;
            }
        }

        private void textBox2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r')
            {
                if (textBox3.Visible)
                {
                    textBox3.Focus();
                }
                else
                {
                    button1.PerformClick();
                }
                e.Handled = true;
            }
        }

        private void textBox3_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r')
            {
                button1.Focus();
                e.Handled = true;
            }
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            on_captcha_required();
        }

        private void frmLogin_FormClosing(object sender, FormClosingEventArgs e)
        {
            BaiduOAuth.LoginCaptchaRequired -= on_captcha_required;
            BaiduOAuth.LoginExceptionRaised -= on_login_exception_raised;
            BaiduOAuth.LoginFailed -= on_login_failed;
            BaiduOAuth.LoginSucceeded -= on_login_succeeded;
        }
    }
}
