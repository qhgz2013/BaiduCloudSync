using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using GlobalUtil;

namespace BaiduCloudSync
{
    public partial class frmKeyCreate : Form
    {
        public static string AES_KEY_FILENAME = "aes_key.keydata";
        public static string RSA_KEY_FILENAME = "rsa_key.pem";
        public static string RSA_PUBLIC_KEY_CACHE = ".cache/rsa_key_cache";
        public frmKeyCreate()
        {
            InitializeComponent();
        }
        private void radioButton4_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton4.Checked)
            {
                //固定密码
                groupBox1.Visible = false;
                label1.Visible = true;
                textBox1.Visible = true;
                pictureBox1.Visible = true;
                progressBar1.Visible = true;
                button3.Visible = true;
                button4.Visible = true;
            }
            else
            {
                groupBox1.Visible = true;
                label1.Visible = false;
                textBox1.Visible = false;
                pictureBox1.Visible = false;
                progressBar1.Visible = false;
                button3.Visible = false;
                button4.Visible = false;
            }
        }

        private void frmKeyCreate_Load(object sender, EventArgs e)
        {
            _cancelled = false;
            _track = new List<byte>();
            pictureBox1.Image = Crypt.GenerateRandomBitmap(pictureBox1.Width, pictureBox1.Height);
            _get_bmp_data();
        }
        private bool _cancelled;
        public bool Cancelled { get { return _cancelled; } }
        private void button2_Click(object sender, EventArgs e)
        {
            _cancelled = true;
            Close();
        }
        private List<byte> _track;
        private byte[,] _bmp_data;
        private void _get_bmp_data()
        {
            _bmp_data = new byte[pictureBox1.Width, pictureBox1.Height];
            var bmp = new Bitmap(pictureBox1.Image);
            var lck = bmp.LockBits(new Rectangle(0, 0, pictureBox1.Width, pictureBox1.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            var data = new byte[lck.Stride * pictureBox1.Height];
            Marshal.Copy(lck.Scan0, data, 0, data.Length);
            for (int x = 0; x < pictureBox1.Width; x++)
            {
                for (int y = 0; y < pictureBox1.Height; y++)
                {
                    int offset = y * lck.Stride + x * 3;
                    if (data[offset] > 128)
                        _bmp_data[x, y] = 1;
                    else
                        _bmp_data[x, y] = 0;
                }
            }
            bmp.UnlockBits(lck);
        }
        private void button3_Click(object sender, EventArgs e)
        {
            pictureBox1.Image = Crypt.GenerateRandomBitmap(pictureBox1.Width, pictureBox1.Height);
            _get_bmp_data();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            _track.Clear();
            progressBar1.Value = 0;
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) != 0 && _track.Count < 384)
            {
                _track.Add(_bmp_data[e.X, e.Y]);
                progressBar1.Value = _track.Count;
                if (_track.Count == 384)
                {
                    MessageBox.Show(this, "轨迹数据输入完成", "喵喵喵");
                }
            }
        }
        private void button1_Click(object sender, EventArgs e)
        {
            if (radioButton4.Checked)
            {
                //固定密码
                string str_data;
                if (_track.Count == 384)
                {
                    //鼠标轨迹模式
                    var data = new byte[48];
                    for (int i = 0; i < data.Length; i++)
                    {
                        for (int j = 0; j < 8; j++)
                        {
                            data[i] <<= 1;
                            data[i] |= _track[i * 8 + j];
                        }
                    }
                    str_data = util.Hex(data);
                }
                else
                {
                    //文本模式
                    var str_text = textBox1.Text;
                    //salt added
                    str_text += util.ToUnixTimestamp(DateTime.Now).ToString();
                    str_text += Guid.NewGuid();
                    //hash
                    var sha512 = new System.Security.Cryptography.SHA512CryptoServiceProvider();
                    var buffer = Encoding.UTF8.GetBytes(str_text);
                    sha512.TransformFinalBlock(buffer, 0, buffer.Length);
                    var sha_result = sha512.Hash;
                    var data = new byte[48];
                    var rnd = new Random();
                    for (int i = 0; i < 48; i++)
                        data[i] = sha_result[rnd.Next(sha_result.Length)];
                    str_data = util.Hex(data);
                }
                File.WriteAllText(AES_KEY_FILENAME, str_data);
                MessageBox.Show(this, "数据已保存到 " + AES_KEY_FILENAME + " (按下确认后会自动定位到该文件中)\r\n注意：该文件不要分发给任何不信任的人，以及上传到云盘等等\r\n请妥善保管该密钥文件，最好多备份到U盘等其他媒介中\r\n\r\n！丢失该文件会造成所有加密文件无法解密，后果由自己承担！", "很严肃的事情");
                Process.Start("explorer.exe", "/select,\"" + AppDomain.CurrentDomain.BaseDirectory + AES_KEY_FILENAME + "\"");

            }
            else
            {
                //动态密码
                byte[] pubkey, prvkey;
                int bit;
                if (radioButton3.Checked) bit = 4096;
                else if (radioButton2.Checked) bit = 2048;
                else bit = 1024;

                Crypt.RSA_CreateKey(out pubkey, out prvkey, bit);
                var str_pem = Crypt.RSA_ExportPEMPrivateKey(prvkey);
                var str_pem2 = Crypt.RSA_ExportPEMPublicKey(pubkey);
                File.WriteAllText(RSA_PUBLIC_KEY_CACHE, str_pem2);
                File.WriteAllText(RSA_KEY_FILENAME, str_pem);
                MessageBox.Show(this, "数据已保存到 " + RSA_KEY_FILENAME + " (按下确认后会自动定位到该文件中)\r\n注意：该文件不要分发给任何不信任的人，以及上传到云盘等等\r\n请妥善保管该密钥文件，最好多备份到U盘等其他媒介中\r\n\r\n！丢失该文件会造成所有加密文件无法解密，后果由自己承担！", "很严肃的事情");
                Process.Start("explorer.exe", "/select,\"" + AppDomain.CurrentDomain.BaseDirectory + RSA_KEY_FILENAME + "\"");
            }
            Close();
        }

        #region static functions
        public static bool HasAesKey { get { return File.Exists(AES_KEY_FILENAME); } }
        public static bool HasRsaPrivateKey { get { return File.Exists(RSA_KEY_FILENAME); } }
        public static bool HasRsaPublicKey { get { return File.Exists(RSA_PUBLIC_KEY_CACHE) || HasRsaPrivateKey; } }
        public static byte[] LoadRsaKey(bool private_key = false)
        {
            bool pubkey_exist = HasRsaPublicKey, prvkey_exist = HasRsaPrivateKey;
            if (private_key && !prvkey_exist) return null;
            if (!private_key && !pubkey_exist) return null;
            byte[] ret = null;
            try
            {
                if (prvkey_exist)
                {
                    if (private_key)
                        ret = Crypt.RSA_ImportPEMPrivateKey(File.ReadAllText(RSA_KEY_FILENAME));
                    else
                        ret = Crypt.RSA_ImportPEMPublicKey(File.ReadAllText(RSA_KEY_FILENAME));

                    if (!File.Exists(RSA_PUBLIC_KEY_CACHE))
                        File.WriteAllText(RSA_PUBLIC_KEY_CACHE, Crypt.RSA_ExportPEMPublicKey(ret));
                }
                else if (pubkey_exist)
                {
                    ret = Crypt.RSA_ImportPEMPublicKey(File.ReadAllText(RSA_PUBLIC_KEY_CACHE));
                }
            }
            catch (Exception)
            {

            }
            return ret;
        }
        public static void LoadAesKey(out byte[] key, out byte[] iv)
        {
            key = null;
            iv = null;
            if (!HasAesKey) return;
            try
            {
                var str_data = File.ReadAllText(AES_KEY_FILENAME);
                str_data = str_data.Replace("\r", "").Replace("\n", "");
                if (str_data.Length == 96)
                {
                    byte[] data = util.Hex(str_data);
                    key = new byte[32];
                    iv = new byte[16];
                    Array.Copy(data, 0, key, 0, 32);
                    Array.Copy(data, 32, iv, 0, 16);
                }
            }
            catch (Exception)
            {

            }
        }
        /// <summary>
        /// 通过文件对话框查询密钥文件，返回操作是否成功
        /// </summary>
        /// <returns></returns>
        public static bool QueryKeyFile()
        {
            using (var owner = new Form() { Width = 0, Height = 0 })
            {
                owner.Show();
                owner.BringToFront();
                var openFileDialog = new OpenFileDialog();
                openFileDialog.CheckFileExists = true;
                openFileDialog.CheckPathExists = true;
                openFileDialog.Title = "选择密钥文件";
                openFileDialog.Filter = "密钥文件|*.pem;*.keydata";
                bool result = false;
                owner.Invoke(new System.Threading.ThreadStart(delegate
                {

                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        if (openFileDialog.FileName.EndsWith(".pem"))
                        {
                            File.Copy(openFileDialog.FileName, RSA_KEY_FILENAME);
                            try
                            {
                                var rsa_test = new System.Security.Cryptography.RSACryptoServiceProvider();
                                rsa_test.ImportCspBlob(LoadRsaKey(true));
                                if (rsa_test.PublicOnly) throw new ArgumentException("The key file is not a private key file");
                                result = true;
                            }
                            catch (Exception)
                            {
                                result = false;
                            }
                        }
                        else if (openFileDialog.FileName.EndsWith(".keydata"))
                        {
                            byte[] key, iv;
                            File.Copy(openFileDialog.FileName, AES_KEY_FILENAME);
                            LoadAesKey(out key, out iv);
                            if (key == null || iv == null) result = false;
                            else result = true;
                        }
                    }
                }));
                return result;
            }
        }
        public static void ResetKey()
        {
            if (HasRsaPrivateKey) File.Delete(RSA_KEY_FILENAME);
            if (HasRsaPublicKey) File.Delete(RSA_PUBLIC_KEY_CACHE);
            if (HasAesKey) File.Delete(AES_KEY_FILENAME);
        }
        #endregion
    }
}
