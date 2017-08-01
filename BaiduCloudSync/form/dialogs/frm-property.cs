using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using static BaiduCloudSync.BaiduPCS;

namespace BaiduCloudSync
{
    //todo: using FileListCacher to reuse cached file data
    public partial class frmProperty : Form
    {
        public frmProperty(IEnumerable<ObjectMetadata> data, FileListCacher list = null)
        {
            InitializeComponent();
            _data = new List<ObjectMetadata>();
            foreach (var item in data)
            {
                _data.Add(item);
            }
            _list = list;
            if (_data.Count == 0) throw new ArgumentNullException("data");
        }
        List<ObjectMetadata> _data;
        FileListCacher _list;
        private void frmProperty_Load(object sender, EventArgs e)
        {
            if (_data.Count == 1)
            {
                var data = _data[0];
                lCategory.Text = data.Category.ToString();
                switch (data.Category)
                {
                    case 1:
                        lCategory.Text += " {Video}";
                        break;
                    case 2:
                        lCategory.Text += " {Music}";
                        break;
                    case 3:
                        lCategory.Text += " {Image}";
                        break;
                    case 4:
                        lCategory.Text += " {Document}";
                        break;
                    case 5:
                        lCategory.Text += " {Application}";
                        break;
                    case 6:
                        lCategory.Text += " {Others}";
                        break;
                    case 7:
                        lCategory.Text += " {Torrent}";
                        break;
                    default:
                        break;
                }
                lFS_ID.Text = data.FS_ID.ToString();
                lIsDir.Text = data.IsDir.ToString();
                lLocalCTime.Text = data.LocalCTime.ToString();
                if (data.LocalCTime != 0)
                    lLocalCTime.Text += " {" + util.FromUnixTimestamp(data.LocalCTime).ToString("yyyy-MM-dd HH:mm:ss") + "}";
                lLocalMTime.Text = data.LocalMTime.ToString();
                if (data.LocalMTime != 0)
                    lLocalMTime.Text += " {" + util.FromUnixTimestamp(data.LocalMTime).ToString("yyyy-MM-dd HH:mm:ss") + "}";
                lMD5.Text = data.MD5;
                lOperID.Text = data.OperID.ToString();
                lPath.Text = data.Path;
                lServerCTime.Text = data.ServerCTime.ToString();
                if (data.ServerCTime != 0)
                    lServerCTime.Text += " {" + util.FromUnixTimestamp(data.ServerCTime).ToString("yyyy-MM-dd HH:mm:ss") + "}";
                lServerFileName.Text = data.ServerFileName;
                lServerMTime.Text = data.ServerMTime.ToString();
                if (data.ServerMTime != 0)
                    lServerMTime.Text += " {" + util.FromUnixTimestamp(data.ServerMTime).ToString("yyyy-MM-dd HH:mm:ss") + "}";
                lSize.Text = data.Size.ToString();
                if (data.IsDir)
                    lSize.Text += " {获取中}";
                else
                {
                    if (data.Size < 0x400) lSize.Text += " {" + data.Size + "B}";
                    else if (data.Size < 0x100000) lSize.Text += " {" + (data.Size / (double)0x400).ToString("0.000") + "KB}";
                    else if (data.Size < 0x40000000) lSize.Text += " {" + (data.Size / (double)0x100000).ToString("0.000") + "MB}";
                    else lSize.Text += " {" + (data.Size / (double)0x40000000).ToString("0.000") + "GB}";
                }
                lUnlist.Text = data.Unlist.ToString();

                int xwidth = lPath.Left + lPath.PreferredWidth + 50;
                if (xwidth > Width) Width = xwidth;
                if (Width > 1000) Width = 1000;

            }
            else
            {
                lCategory.Text = "n/a {multi files}";
                lFS_ID.Text = "n/a";
                lIsDir.Text = "-";
                lLocalCTime.Text = "n/a {multi files}";
                lLocalMTime.Text = "n/a {multi files}";
                lMD5.Text = "-";
                lOperID.Text = "n/a";
                lPath.Text = "-";
                lServerCTime.Text = "n/a {multi files}";
                lServerFileName.Text = "{已选择 " + _data.Count + " 个文件或目录}";
                lServerMTime.Text = "n/a {multi files}";
                lSize.Text = "0 {获取中}";
                lUnlist.Text = "n/a";
            }
            if (_list != null)
            {
                _bgThd = new Thread(
                    () =>
                    {
                        try
                        {
                            ulong size = 0, dirs = 0, files = 0;
                            foreach (var item in _data)
                            {
                                if (item.IsDir)
                                    _calculate_size(item.Path + "/", ref size, ref dirs, ref files);
                                else
                                {
                                    files++;
                                    size += item.Size;
                                }
                            }
                            Invoke(new NoArgSTA(delegate
                            {
                                lSize.Text = size.ToString();

                                if (size < 0x400) lSize.Text += " {" + size + "B}";
                                else if (size < 0x100000) lSize.Text += " {" + (size / (double)0x400).ToString("0.000") + "KB}";
                                else if (size < 0x40000000) lSize.Text += " {" + (size / (double)0x100000).ToString("0.000") + "MB}";
                                else lSize.Text += " {" + (size / (double)0x40000000).ToString("0.000") + "GB}";

                                lSize.Text += " [文件总数: " + files.ToString("#,##0") + " | 文件夹总数: " + dirs.ToString("#,##0") + "]";
                            }
                            ));
                        }
                        catch (ThreadAbortException) { }
                        catch (Exception)
                        {
                            Invoke(new NoArgSTA(delegate
                            {
                                lSize.Text = "0 {获取大小失败}";
                            }
                            ));
                        }
                    }
                    );

                _bgThd.Start();
            }
        }
        private Thread _bgThd;
        private void _calculate_size(string path, ref ulong size, ref ulong dir_count, ref ulong file_count)
        {
            var ls = _list.GetFileList(path);
            foreach (var item in ls)
            {
                if (item.IsDir)
                {
                    dir_count++;
                    _calculate_size(item.Path + "/", ref size, ref dir_count, ref file_count);
                }
                else
                {
                    size += item.Size;
                    file_count++;
                }
            }
            var _tsize = size;
            var _tdir = dir_count;
            var _tfile = file_count;
            Invoke(new NoArgSTA(delegate
            {
                lSize.Text = _tsize.ToString();

                if (_tsize < 0x400) lSize.Text += " {" + _tsize + "B}";
                else if (_tsize < 0x100000) lSize.Text += " {" + (_tsize / (double)0x400).ToString("0.000") + "KB}";
                else if (_tsize < 0x40000000) lSize.Text += " {" + (_tsize / (double)0x100000).ToString("0.000") + "MB}";
                else lSize.Text += " {" + (_tsize / (double)0x40000000).ToString("0.000") + "GB}";

                lSize.Text += " [文件总数: " + _tfile.ToString("#,##0") + " | 文件夹总数: " + _tdir.ToString("#,##0") + "]";
            }
            ));
        }
        private delegate void NoArgSTA();

        private void frmProperty_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\u001b')
            {
                Close();
            }
        }

        private void frmProperty_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_bgThd != null)
                _bgThd.Abort();
        }
    }
}
