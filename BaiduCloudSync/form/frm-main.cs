using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using static BaiduCloudSync.BaiduPCS;
using GlobalUtil;
//todo: 修改所有由主线程发起的异步线程调用方式 (+线程中止功能)
namespace BaiduCloudSync
{
    public partial class frmMain : Form
    {
        //注意: 网盘目录严格按照linux格式判断:
        //根目录: /
        //目录: /abc/def/dir/
        //文件: /abc/def/file
        //目录后面一定要加"/" !!!
        private BaiduOAuth _auth;
        public frmMain()
        {
            InitializeComponent();
            //_auth = new BaiduOAuth("default");
            if (GlobalUtil.NetUtils.NetStream.DefaultCookieContainer.Keys.Count > 0)
                _auth = new BaiduOAuth(GlobalUtil.NetUtils.NetStream.DefaultCookieContainer.Keys.First());
            else
            {
                _auth = new BaiduOAuth(util.GenerateFormDataBoundary());
                var frmlogin = new frmLogin(_auth);
                frmlogin.ShowDialog();
                if (!frmlogin.LoginSucceeded)
                    Environment.Exit(0);
            }
            _pcsAPI = new BaiduPCS(_auth);
            _remote_file_list = new RemoteFileCacher();
            if (_remote_file_list.GetAllAccounts().Length == 0)
                _remote_file_list.AddAccount(_pcsAPI);
            _local_file_list = new LocalFileCacher();
            StaticConfig.LoadStaticConfig();

        }
        private void Form1_Load(object sender, EventArgs e)
        {
            tabPage1.Show();
            tabPage2.Show();
            tabPage3.Show();
            tabPage4.Show();
            nDebugListCnt.ValueChanged -= nDebugListCnt_ValueChanged;
            nDebugListCnt.Value = StaticConfig.MAX_DEBUG_OUTPUT_COUNT;
            nDebugListCnt.ValueChanged += nDebugListCnt_ValueChanged;
            nDlThdCnt.ValueChanged -= nDlThdCnt_ValueChanged;
            nDlThdCnt.Value = StaticConfig.MAX_DOWNLOAD_THREAD;
            nDlThdCnt.ValueChanged += nDlThdCnt_ValueChanged;
            nListCount.ValueChanged -= nListCount_ValueChanged;
            nListCount.Value = StaticConfig.MAX_LIST_SIZE;
            nListCount.ValueChanged += nListCount_ValueChanged;
            nMaxDownload.ValueChanged -= nMaxDownload_ValueChanged;
            nMaxDownload.Value = StaticConfig.MAX_DOWNLOAD_PARALLEL_TASK_COUNT;
            nMaxDownload.ValueChanged += nMaxDownload_ValueChanged;
            nMaxUpload.ValueChanged -= nMaxUpload_ValueChanged;
            nMaxUpload.Value = StaticConfig.MAX_UPLOAD_PARALLEL_TASK_COUNT;
            nMaxUpload.ValueChanged += nMaxUpload_ValueChanged;
            cIgnoreUploadFail.CheckedChanged -= cIgnoreUploadFail_CheckedChanged;
            cIgnoreUploadFail.Checked = StaticConfig.IGNORE_UPLOAD_ERROR;
            cIgnoreUploadFail.CheckedChanged += cIgnoreUploadFail_CheckedChanged;
            cAutoReupload.CheckedChanged -= cAutoReupload_CheckedChanged;
            cAutoReupload.Checked = StaticConfig.AUTO_REUPLOAD_WHEN_MD5_ERROR;
            cAutoReupload.CheckedChanged += cAutoReupload_CheckedChanged;

            //login required
            if (string.IsNullOrEmpty(_auth.bduss))
            {
                frmLogin frm = new frmLogin(_auth);
                frm.ShowDialog(this);

                if (!frm.LoginSucceeded)
                {
                    Close();
                    return;
                }
            }
            _asyncCall(delegate
            {
                lock (__thd_lck)
                {
                    __update_treeview_data("/");
                    Invoke(new NoArgSTA(delegate { treeView_DirList.Nodes[0].Expand(); treeView_DirList.SelectedNode = treeView_DirList.Nodes[0]; }));
                }
                _update_quota();
            }, "获取文件信息...");
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            GlobalUtil.NetUtils.NetStream.SaveCookie();
            StaticConfig.SaveStaticConfig();

            _local_file_list.Dispose();
        }

        //变量命名规定 (非UI部分)
        //_x 私有数据，可以在其他不同功能的区域访问，也可以作UI的后台访问
        //__x 私有数据，仅能在该区域内的函数访问
        //_x() 私有函数，可以在其他不同功能的区域访问，也可以作UI的后台访问
        //__x() 私有函数，仅能在该区域内的函数访问

        #region main data
        private BaiduPCS _pcsAPI;
        private RemoteFileCacher _remote_file_list;
        private LocalFileCacher _local_file_list;
        #endregion

        #region quota update
        //external dependency: pQuota (ProgressBar), lQuota (Label)

        private object __quota_thread_lock = new object();
        private bool __quota_fetching = false;
        //刷新网盘配额: 允许非主线程调用
        private void _update_quota()
        {
            lock (__quota_thread_lock)
            {
                if (__quota_fetching) return;
                __quota_fetching = true;
                try
                {
                    _pcsAPI.GetQuotaAsync((suc, quota, s) =>
                    {
                        try
                        {
                            Invoke(new ThreadStart(delegate
                            {
                                if (suc)
                                {
                                    pQuota.Minimum = 0;
                                    pQuota.Maximum = 10000;
                                    pQuota.Value = quota.Total != 0 ? (int)(quota.InUsed * 10000.0 / quota.Total) : 0;
                                    lQuota.Text = "可用: " + util.FormatBytes(quota.Total - quota.InUsed) + ", 总: " + util.FormatBytes(quota.Total) + " (" + (100 - pQuota.Value / 100.0).ToString("0.00") + "%)";
                                }
                                else
                                    lQuota.Text = "获取失败，请刷新尝试";
                            }));
                        }
                        finally
                        {
                            lock (__quota_thread_lock)
                                __quota_fetching = false;
                        }
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("获取网盘大小出错:\r\n" + ex.ToString());
                }
            }
        }
        #endregion


        #region ICON
        //从指定拓展名中获取图标，若为文件夹则传入":/dir/" (nothrow)
        private Icon _getIconFromExt(string ext)
        {
            if (ext == ":/dir/")
            {
                return IconExtractor.GetIconDir();
            }
            else if (ext.Length == 0)
            {
                return IconExtractor.GetIconExt();
            }
            else
            {
                return IconExtractor.GetIconExt(ext);
            }
        }
        #endregion


        #region Async Thread
        private Thread __background_thread;
        private object __background_thd_lock = new object();
        private List<Thread> __async_call_thread_list = new List<Thread>();
        private object __async_call_thd_lock = new object();
        private const int __MAX_ASYNC_WAIT_STACK = 50;
        private void _cancelAsyncCall(object state = null)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                Invoke(new NoArgSTA(delegate
                {
                    lAsyncStatus.Visible = false;
                    bAsyncCancel.Visible = false;
                }));
                lock (__async_call_thd_lock)
                {
                    for (int i = 0; i < __async_call_thread_list.Count; i++)
                    {
                        try
                        {
                            __async_call_thread_list[i].Abort();
                        }
                        catch (Exception)
                        {
                        }
                    }
                    __async_call_thread_list.Clear();

                    var tmp_thd = __background_thread;
                    __background_thread = null;
                    if (tmp_thd != null)
                    {
                        lock (__background_thd_lock)
                        {
                            try
                            {
                                tmp_thd.Abort();
                            }
                            catch (Exception)
                            {
                            }
                        }
                    }
                }
            });
        }
        private void _asyncCall(NoArgSTA delegation, string status_name = null, bool abort_last_request = false)
        {
            var thd = new Thread(new ThreadStart(delegate
            {
                Debug.Print("async call: " + status_name);
                lock (__background_thd_lock)
                {
                    try
                    {
                        if (__async_call_thread_list.Count >= __MAX_ASYNC_WAIT_STACK) return;
                        //中止或者等待上一个线程，因为这里用主线程调用的话会有死锁，所以移到新的线程请求
                        if (__background_thread != null)
                        {
                            try
                            {
                                if (abort_last_request)
                                    __background_thread.Abort();
                                else
                                    __background_thread.Join();
                                __background_thread = null;
                            }
                            catch (Exception ex)
                            {
                                Tracer.GlobalTracer.TraceError(ex.ToString());
                            }
                        }
                        //开始新的线程
                        __background_thread = new Thread(new ThreadStart(delegate
                        {
                            try
                            {
                                delegation.Invoke();
                            }
                            catch (Exception ex)
                            {
                                Tracer.GlobalTracer.TraceError(ex.ToString());
                            }
                            finally
                            {
                                __background_thread = null;
                                Invoke(new NoArgSTA(delegate
                                {
                                    //reset async data
                                    lAsyncStatus.Text = string.Empty;
                                    bAsyncCancel.Visible = false;
                                    lAsyncStatus.Visible = false;
                                }));
                            }
                        }));
                        __background_thread.Name = "后台线程";
                        __background_thread.IsBackground = true;
                        __background_thread.SetApartmentState(ApartmentState.STA);
                        //output
                        Invoke(new NoArgSTA(delegate
                        {
                            lAsyncStatus.Text = status_name == null ? string.Empty : status_name;
                            lAsyncStatus.Visible = true;
                            bAsyncCancel.Visible = true;
                        }));

                        //remove from async call list
                        lock (__async_call_thd_lock)
                        {
                            __async_call_thread_list.Remove(Thread.CurrentThread);
                        }

                        //starting thread
                        __background_thread.Start();
                    }
                    catch (Exception)
                    {
                    }
                }
            }));
            lock (__async_call_thd_lock)
            {
                __async_call_thread_list.Add(thd);
            }
            thd.Start();
        }
        #endregion


        //STA module
        #region UI callback
        private object __thd_lck = new object();
        private delegate void NoArgSTA();

        private string __current_treeview_path = string.Empty;
        //更新左边的树状表的path路径(包括其父路径) (nothrow)
        private void __update_treeview_data(string path = null)
        {

            if (path == null) path = __current_treeview_path;
            //if (!FileListCacher.DirValidating(path)) return;

            //storing the selected node
            TreeNode selectednode = null;
            Invoke(new NoArgSTA(delegate { selectednode = treeView_DirList.SelectedNode; }));
            string selectedpath = string.Empty;
            if (selectednode != null) selectedpath = __get_dir_path_from_node(selectednode);

            var paths = path.Split('/');

            var cur_node = treeView_DirList.Nodes[0];

            var cur_path = string.Empty;
            for (int i = 0; i < paths.Length - 1; i++)
            {
                cur_path += paths[i] + "/";
                List<ObjectMetadata> files = null;
                var sync_thread = Thread.CurrentThread;
                _remote_file_list.GetFileListAsync(cur_path, (suc, result, state) =>
                {
                    files = result.ToList();
                    sync_thread.Interrupt();
                });
                try
                {
                    Thread.Sleep(Timeout.Infinite);
                }
                catch { }
                //var files = _remote_file_list.GetFileList(cur_path).ToList();
                files.Sort((a, b) => a.ServerFileName.CompareTo(b.ServerFileName));

                //Invoke(new NoArgSTA(delegate { cur_node.Nodes.Clear(); }));
                Invoke(new NoArgSTA(delegate
                {
                    for (int j = 0; j < cur_node.Nodes.Count; j++)
                    {
                        if (files.FindIndex(target => target.ServerFileName == cur_node.Nodes[j].Name) == -1)
                        {
                            cur_node.Nodes.RemoveAt(j);
                            j--;
                        }
                    }
                }));

                foreach (var item in files)
                {
                    if (item.IsDir)
                    {
                        var treenode = new TreeNode(item.ServerFileName);
                        treenode.Name = item.ServerFileName;
                        treenode.Nodes.Add("<temp_node>", "Fetching data...");
                        //Invoke(new NoArgSTA(delegate { cur_node.Nodes.Add(treenode); }));
                        Invoke(new NoArgSTA(delegate
                        {
                            if (!cur_node.Nodes.ContainsKey(item.ServerFileName))
                            {
                                cur_node.Nodes.Add(treenode);
                            }
                        }));
                    }
                }

                if (i < paths.Length - 2)
                {
                    if (!cur_node.Nodes.ContainsKey(paths[i + 1])) break;
                    cur_node = cur_node.Nodes[paths[i + 1]];
                }
            }
            __current_treeview_path = cur_path;
            //restore selected node
            cur_path = string.Empty;
            cur_node = treeView_DirList.Nodes[0];

            for (int i = 1; i < paths.Length - 1; i++)
            {
                if (cur_node.Nodes.ContainsKey(paths[i]))
                    cur_node = cur_node.Nodes[paths[i]];
            }
            Invoke(new NoArgSTA(delegate { treeView_DirList.SelectedNode = cur_node; }));
        }

        private string __current_listview_path = string.Empty;
        //排序依据
        private enum SortBase
        {
            FileName, Size, LastWriteTime, CreateTime
        }
        private SortBase __sortType = SortBase.FileName;
        private bool __asc = true;
        //更新右边的表为path路径的所有文件/文件夹 MTA (nothrow)
        private void __update_listview_data(string path = null, SortBase sort_type = SortBase.FileName, bool asc = true)
        {
            if (path == null) path = __current_listview_path;
            //if (!FileListCacher.DirValidating(path)) return;
            __current_listview_path = path;
            IEnumerable<ObjectMetadata> file_list = null;
            try
            {
                //file_list = _remote_file_list.GetFileList(path);
                var sync_thread = Thread.CurrentThread;
                _remote_file_list.GetFileListAsync(path, (suc, result, state) =>
                {
                    file_list = result;
                    sync_thread.Interrupt();
                });
                try
                {
                    Thread.Sleep(Timeout.Infinite);
                }
                catch { }
            }
            catch (Exception)
            {
                Invoke(new NoArgSTA(delegate
                {
                    MessageBox.Show(this, "获取文件列表失败，请稍后尝试", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }));
            }
            if (file_list == null) return;
            //splitting into dirs and files list by linq
            IEnumerable<ObjectMetadata> dirs;
            IEnumerable<ObjectMetadata> files;
            #region sorting
            switch (sort_type)
            {
                case SortBase.FileName:
                    if (asc)
                    {
                        dirs = from ObjectMetadata data in file_list where data.IsDir orderby data.ServerFileName ascending select data;
                        files = from ObjectMetadata data in file_list where !data.IsDir orderby data.ServerFileName ascending select data;
                    }
                    else
                    {
                        dirs = from ObjectMetadata data in file_list where data.IsDir orderby data.ServerFileName descending select data;
                        files = from ObjectMetadata data in file_list where !data.IsDir orderby data.ServerFileName descending select data;
                    }
                    break;
                case SortBase.Size:
                    if (asc)
                    {
                        dirs = from ObjectMetadata data in file_list where data.IsDir orderby data.Size ascending select data;
                        files = from ObjectMetadata data in file_list where !data.IsDir orderby data.Size ascending select data;
                    }
                    else
                    {
                        dirs = from ObjectMetadata data in file_list where data.IsDir orderby data.Size descending select data;
                        files = from ObjectMetadata data in file_list where !data.IsDir orderby data.Size descending select data;
                    }
                    break;
                case SortBase.LastWriteTime:
                    if (asc)
                    {
                        dirs = from ObjectMetadata data in file_list where data.IsDir orderby data.LocalMTime ascending select data;
                        files = from ObjectMetadata data in file_list where !data.IsDir orderby data.LocalMTime ascending select data;
                    }
                    else
                    {
                        dirs = from ObjectMetadata data in file_list where data.IsDir orderby data.LocalMTime descending select data;
                        files = from ObjectMetadata data in file_list where !data.IsDir orderby data.LocalMTime descending select data;
                    }
                    break;
                case SortBase.CreateTime:
                    if (asc)
                    {
                        dirs = from ObjectMetadata data in file_list where data.IsDir orderby data.LocalCTime ascending select data;
                        files = from ObjectMetadata data in file_list where !data.IsDir orderby data.LocalCTime ascending select data;
                    }
                    else
                    {
                        dirs = from ObjectMetadata data in file_list where data.IsDir orderby data.LocalCTime descending select data;
                        files = from ObjectMetadata data in file_list where !data.IsDir orderby data.LocalCTime descending select data;
                    }
                    break;
                default:
                    dirs = null;
                    files = null;
                    break;
            }
            #endregion
            if (dirs == null || files == null)
            {
                Invoke(new NoArgSTA(delegate
                {
                    MessageBox.Show(this, "无效的排序", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }));
            }
            Invoke(new NoArgSTA(delegate
            {
                listView_DirData.Items.Clear();
                var img_collection = new ImageList();
                img_collection.ImageSize = new Size(32, 32);
                img_collection.ColorDepth = ColorDepth.Depth32Bit;
                listView_DirData.LargeImageList = img_collection;
                foreach (var item in dirs)
                {
                    const string ext = ":/dir/";
                    if (!img_collection.Images.ContainsKey(ext))
                        img_collection.Images.Add(ext, _getIconFromExt(ext));
                    var lvi = new ListViewItem(item.ServerFileName, ext);
                    lvi.Name = item.ServerFileName;
                    listView_DirData.Items.Add(lvi);
                }
                foreach (var item in files)
                {
                    var names = item.ServerFileName.Split('.');
                    var ext = names[names.Length - 1];
                    var ext_key = ext;
                    if (names.Length == 1) { ext = ""; ext_key = ":/empty_ext/"; }
                    if (!img_collection.Images.ContainsKey(ext_key))
                        img_collection.Images.Add(ext_key, _getIconFromExt(ext));

                    var lvi = new ListViewItem(item.ServerFileName, ext_key);
                    lvi.Name = item.ServerFileName;
                    listView_DirData.Items.Add(lvi);
                }
                img_collection.TransparentColor = Color.Transparent;
            }));

        }

        //从当前node中返回完整目录路径
        private string __get_dir_path_from_node(TreeNode node)
        {
            return node.FullPath.Substring(treeView_DirList.Nodes[0].Text.Length) + "/";
        }
        private void __unhook_treeview()
        {
            Invoke(new NoArgSTA(delegate
            {
                treeView_DirList.AfterSelect -= treeView_DirList_AfterSelect;
                treeView_DirList.BeforeExpand -= treeView_DirList_BeforeExpand;
                treeView_DirList.KeyUp -= treeView_DirList_KeyUp;
                treeView_DirList.MouseClick -= treeView_DirList_MouseClick;
            }));
        }
        private void __hook_treeview()
        {
            Invoke(new NoArgSTA(delegate
            {
                treeView_DirList.AfterSelect += treeView_DirList_AfterSelect;
                treeView_DirList.BeforeExpand += treeView_DirList_BeforeExpand;
                treeView_DirList.KeyUp += treeView_DirList_KeyUp;
                treeView_DirList.MouseClick += treeView_DirList_MouseClick;
            }));
        }
        //UI: 展开目录结点
        private void treeView_DirList_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            _asyncCall(delegate
            {
                lock (__thd_lck)
                {
                    Debug.Print("BeforeExpand");
                    __unhook_treeview();
                    var node = e.Node;
                    if (node.Nodes.ContainsKey("<temp_node>"))
                    {
                        //__update_treeview_data(__get_dir_path_from_node(node));
                        ObjectMetadata[] new_files = null; //_remote_file_list.GetFileList(__get_dir_path_from_node(node));
                        var sync_thread = Thread.CurrentThread;
                        _remote_file_list.GetFileListAsync(__get_dir_path_from_node(node), (suc, result, state) =>
                        {
                            new_files = result;
                            sync_thread.Interrupt();
                        });
                        try
                        {
                            Thread.Sleep(Timeout.Infinite);
                        }
                        catch { }

                        Invoke(new NoArgSTA(delegate
                        {
                            node.Nodes.Clear();
                            foreach (var item in new_files)
                            {
                                if (item.IsDir)
                                {
                                    var treenode = new TreeNode(item.ServerFileName);
                                    treenode.Name = item.ServerFileName;
                                    treenode.Nodes.Add("<temp_node>", "Fetching data...");
                                    node.Nodes.Add(treenode);
                                }
                            }
                        }));

                    }
                    __hook_treeview();
                }
            }, "获取文件信息...");
        }
        //UI: 选择更变
        private void treeView_DirList_AfterSelect(object sender, TreeViewEventArgs e)
        {
            _asyncCall(delegate
            {
                lock (__thd_lck)
                {
                    Debug.Print("AfterSelect");
                    __unhook_treeview();
                    TreeNode selected_item = null;
                    Invoke(new NoArgSTA(delegate { selected_item = treeView_DirList.SelectedNode; }));
                    if (selected_item == null) return;
                    if (e.Node.Nodes.ContainsKey("<temp_node>"))
                    {
                        var node = e.Node;
                        ObjectMetadata[] new_files = null; // _remote_file_list.GetFileList(__get_dir_path_from_node(node));
                        var sync_thread = Thread.CurrentThread;
                        _remote_file_list.GetFileListAsync(__get_dir_path_from_node(node), (suc, result, state) =>
                        {
                            new_files = result;
                            sync_thread.Interrupt();
                        });
                        try
                        {
                            Thread.Sleep(Timeout.Infinite);
                        }
                        catch { }

                        Invoke(new NoArgSTA(delegate
                        {
                            node.Nodes.Clear();
                            foreach (var item in new_files)
                            {
                                if (item.IsDir)
                                {
                                    var treenode = new TreeNode(item.ServerFileName);
                                    treenode.Name = item.ServerFileName;
                                    treenode.Nodes.Add("<temp_node>", "Fetching data...");
                                    node.Nodes.Add(treenode);
                                }
                            }
                        }));
                    }
                    __update_listview_data(__get_dir_path_from_node(selected_item), __sortType, __asc);
                    __current_treeview_path = __get_dir_path_from_node(selected_item);

                    __hook_treeview();
                }
            }, "获取文件信息...");
        }
        //创建整个目录及子目录的文件链接 (throwable)
        private void create_symbollink(string path)
        {
            //var data = _remote_file_list.GetData(path);
            //if (data.IsDir)
            //{
            //    var files = _remote_file_list.GetFileList(path);
            //    foreach (var item in files)
            //    {
            //        create_symbollink(item.Path + (item.IsDir ? "/" : ""));
            //    }
            //}
            //else
            //{
            //    ObjectMetadata meta = new ObjectMetadata();
            //    try { meta = _pcsAPI.ConvertToSymbolLink(data.Path, data.FS_ID); }
            //    catch (ErrnoException ex)
            //    {
            //        Invoke(new NoArgSTA(delegate
            //        {
            //            MessageBox.Show(this, "转换失败: 错误代码: " + ex.Errno, "出错了", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //        }));
            //    }
            //    if (meta.FS_ID == 0)
            //    {
            //        Invoke(new NoArgSTA(delegate
            //        {
            //            MessageBox.Show(this, "转换失败", "出错了", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //        }));
            //    }
            //}
        }
        //UI: 创建文件链接
        private void 创建文件链接ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _asyncCall(delegate
            {
                lock (__thd_lck)
                {
                    //获取选定的文件
                    var ls = new List<ListViewItem>();
                    if (string.IsNullOrEmpty(__current_listview_path)) return;
                    ListView.SelectedListViewItemCollection selected_item = null;
                    Invoke(new NoArgSTA(delegate
                    {
                        selected_item = listView_DirData.SelectedItems;
                        if (selected_item == null) return;
                        foreach (ListViewItem item in selected_item) { ls.Add(item); }
                    }));
                    if (ls.Count == 0) return;
                    var dir_list = new List<string>();
                    //调用创建函数
                    try
                    {
                        foreach (ListViewItem item in ls)
                        {
                            if (item.ImageKey == ":/dir/")
                            {
                                var name = __current_listview_path + item.Text + "/";
                                dir_list.Add(name);
                                create_symbollink(name);
                            }
                            else
                                create_symbollink(__current_listview_path + item.Text);
                        }
                    }
                    catch (Exception)
                    {
                        Invoke(new NoArgSTA(delegate
                        {
                            MessageBox.Show(this, "创建失败", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }));
                    }
                    //更新内容
                    刷新EToolStripMenuItem_Click(sender, e);
                }
            }, "创建文件链接...");
        }
        private void delete_path(IEnumerable<string> paths)
        {
            //_pcsAPI.DeletePath(paths);
            //List<string> parent_data = new List<string>();
            //foreach (var item in paths)
            //{
            //    var parent = FileListCacher.GetParentDir(item);
            //    if (!parent_data.Contains(parent))
            //        parent_data.Add(parent);
            //}
            //foreach (var item in paths)
            //{
            //    _remote_file_list.RemoveFileCache(item);
            //}
        }
        //UI: 删除文件
        private void 删除ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _asyncCall(delegate
            {
                lock (__thd_lck)
                {
                    var ls = new List<ListViewItem>();
                    if (string.IsNullOrEmpty(__current_listview_path)) return;
                    ListView.SelectedListViewItemCollection selected_item = null;
                    bool confirmed = false;
                    Invoke(new NoArgSTA(delegate
                    {
                        selected_item = listView_DirData.SelectedItems;
                        if (selected_item == null) return;
                        foreach (ListViewItem item in selected_item) { ls.Add(item); }
                        if (ls.Count == 0) return;
                        if (MessageBox.Show(this, "确定要删除吗?", "确定", MessageBoxButtons.YesNo) == DialogResult.Yes) confirmed = true;
                    }));
                    if (selected_item == null || !confirmed) return;
                    var path_list = new List<string>(ls.Count);
                    foreach (ListViewItem item in ls)
                    {
                        //文件夹这里就不在结尾+"/"了
                        path_list.Add(__current_listview_path + item.Text);
                    }
                    if (ls.Count == 0) return;
                    try
                    {
                        delete_path(path_list);
                    }
                    catch (ErrnoException ex)
                    {
                        Invoke(new NoArgSTA(delegate
                        {
                            if (ex.Errno == 2) MessageBox.Show(this, "错误代号 2: 身份验证错误\r\n(这个问题应该会修复，手动打开浏览器进入网盘页面即可）", "出错了", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            else MessageBox.Show(this, "错误代号: " + ex.Errno + ": 未知错误", "出错了", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }));
                    }
                    刷新EToolStripMenuItem_Click(sender, e);
                }
            }, "删除文件...");
        }
        //UI: 鼠标点击
        private void treeView_DirList_MouseClick(object sender, MouseEventArgs e)
        {
            Debug.Print("MouseClick");
            treeView_DirList.SelectedNode = treeView_DirList.GetNodeAt(e.X, e.Y);
        }
        //UI: 属性
        private void 属性ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //if (string.IsNullOrEmpty(__current_listview_path)) return;
            //var selected_item = listView_DirData.SelectedItems;
            //Form property_form = null;
            //if (selected_item == null || selected_item.Count == 0)
            //{
            //    if (__current_listview_path == "/")
            //    {
            //        //root dir
            //        property_form = new frmProperty(_remote_file_list.GetFileList("/"), _remote_file_list);
            //    }
            //    else
            //    {
            //        //normal dir
            //        property_form = new frmProperty(new ObjectMetadata[] { _remote_file_list.GetData(__current_listview_path) }, _remote_file_list);
            //    }
            //}
            //else
            //{
            //    var ls = new List<ObjectMetadata>(selected_item.Count);
            //    foreach (ListViewItem item in selected_item)
            //    {
            //        if (item.ImageKey == ":/dir/")
            //            ls.Add(_remote_file_list.GetData(__current_listview_path + item.Text + "/"));
            //        else
            //            ls.Add(_remote_file_list.GetData(__current_listview_path + item.Text));

            //    }
            //    property_form = new frmProperty(ls, _remote_file_list);
            //}
            //property_form.Show(this);
        }
        //todo: 移除该代码
        private void treeView_DirList_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                //删除ToolStripMenuItem_Click(sender, e);
            }
        }
        private void recreate_symbollink(string path)
        {
            //var data = _remote_file_list.GetData(path);
            //if (data.IsDir)
            //{
            //    var files = _remote_file_list.GetFileList(path);
            //    foreach (var item in files)
            //    {
            //        recreate_symbollink(item.Path + (item.IsDir ? "/" : ""));
            //    }
            //}
            //else
            //{
            //    ObjectMetadata meta = new ObjectMetadata();
            //    try { meta = _pcsAPI.ConvertFromSymbolLink(data.Path, data.FS_ID); }
            //    catch (ErrnoException ex)
            //    {
            //        Invoke(new NoArgSTA(delegate
            //        {
            //            MessageBox.Show(this, "转换失败: 错误代码: " + ex.Errno, "出错了", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //        }));
            //    }
            //    if (meta.FS_ID == 0)
            //    {
            //        Invoke(new NoArgSTA(delegate
            //        {
            //            MessageBox.Show(this, "转换失败", "出错了", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //        }));
            //    }
            //}
        }
        //UI: 生成文件
        private void 由链接生成文件ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _asyncCall(delegate
            {
                lock (__thd_lck)
                {
                    //获取选定的文件
                    if (string.IsNullOrEmpty(__current_listview_path)) return;
                    var ls = new List<ListViewItem>();
                    Invoke(new NoArgSTA(delegate
                    {

                        var selected_item = listView_DirData.SelectedItems;
                        if (selected_item == null) return;
                        foreach (ListViewItem item in selected_item) { ls.Add(item); }
                    }));
                    if (ls.Count == 0) return;
                    var dir_list = new List<string>();
                    //调用创建函数
                    try
                    {
                        foreach (ListViewItem item in ls)
                        {
                            if (item.ImageKey == ":/dir/")
                            {
                                var name = __current_listview_path + item.Text + "/";
                                dir_list.Add(name);
                                recreate_symbollink(name);
                            }
                            else
                                recreate_symbollink(__current_listview_path + item.Text);
                        }
                    }
                    catch (Exception)
                    {
                        Invoke(new NoArgSTA(delegate
                        {
                            MessageBox.Show(this, "创建失败", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }));
                    }
                    //更新内容
                    foreach (var item in dir_list)
                    {
                        //remote_file_list.RemoveFileCache(item);
                    }
                    刷新EToolStripMenuItem_Click(sender, e);
                }
            }, "还原文件中...");
        }

        private void _download_files(string path, string save_path)
        {
            //getting data from local cache
            //var file_data = _remote_file_list.GetFileList(path);
            //foreach (var item in file_data)
            //{
            //    var dst_path = save_path;
            //    if (dst_path.Last() != '/' && dst_path.Last() != '\\') dst_path += "/";
            //    dst_path += item.ServerFileName;
            //    if (dst_path.EndsWith(".bcsd")) dst_path = dst_path.Substring(0, dst_path.Length - 5);
            //    if (item.IsDir)
            //    {
            //        _download_files(item.Path + "/", dst_path);
            //    }
            //    else
            //    {
            //        Invoke(new NoArgSTA(delegate
            //        {
            //            var frm = new frmDownload(_pcsAPI, item, dst_path);
            //            downloadTransferList1.AddTask(frm);
            //        }));
            //    }
            //}
        }
        //UI: 下载
        private void 下载ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(__current_listview_path)) return;
            var selected_item = listView_DirData.SelectedItems;
            if (selected_item.Count == 0) return;
            var data_list = new List<ObjectMetadata>();
            var node_path = __current_listview_path;

            foreach (ListViewItem item in selected_item)
            {
                //data_list.Add(_remote_file_list.GetData(node_path + item.Text + (item.ImageKey == ":/dir/" ? "/" : "")));
            }

            //single file
            if (data_list.Count == 1 && !data_list[0].IsDir)
            {
                downloadFilePath.FileName = data_list[0].ServerFileName;
                if (data_list[0].ServerFileName.EndsWith(".bcsd")) downloadFilePath.FileName = downloadFilePath.FileName.Substring(0, downloadFilePath.FileName.Length - 5);
                if (downloadFilePath.ShowDialog() != DialogResult.OK) return;
                var save_path = downloadFilePath.FileName;
                //var frm = new frmDownload(_pcsAPI, data_list[0], save_path);
                //downloadTransferList1.AddTask(frm);
            }
            //multi files / dirs
            else
            {
                if (downloadFileDir.ShowDialog() != DialogResult.OK) return;
                var save_path = downloadFileDir.SelectedPath;
                foreach (var item in data_list)
                {
                    var path = save_path;
                    if (path.Last() != '/' && path.Last() != '\\') path += "/";
                    path += item.ServerFileName;
                    if (path.EndsWith(".bcsd")) path = path.Substring(0, path.Length - 5);
                    if (item.IsDir)
                    {
                        lock (__thd_lck)
                        {
                            _asyncCall(delegate { _download_files(item.Path + "/", path); }, "获取文件信息...");
                        }
                    }
                    else
                    {
                        //var frm = new frmDownload(_pcsAPI, item, path);
                        //downloadTransferList1.AddTask(frm);
                    }
                }
            }
        }

        //UI: 双击
        private void listView_DirData_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (string.IsNullOrEmpty(__current_listview_path)) return;
            if (listView_DirData.SelectedItems.Count != 1) return; //single select only
            var selected_item = listView_DirData.SelectedItems[0];
            var filename = selected_item.Text;
            var isdir = (selected_item.ImageKey == ":/dir/");
            if (isdir)
                _asyncCall(delegate
                {
                    lock (__thd_lck)
                    {
                        //__update_listview_data(__current_listview_path + filename + "/", __sortType, __asc);
                        var path = __current_listview_path + filename + "/";
                        __update_treeview_data(path);
                        //bool mamual_update_required = false;
                        Invoke(new NoArgSTA(delegate
                        {
                            var node = treeView_DirList.Nodes[0];
                            var first_node = node;
                            var paths = path.Split('/');
                            for (int i = 1; i < paths.Length - 1 && node != null; i++)
                            {
                                node = node.Nodes[paths[i]];
                            }
                            if (node != null)
                            {
                                if (node != treeView_DirList.SelectedNode)
                                    treeView_DirList.SelectedNode = node;
                                //else
                                //    mamual_update_required = true;
                            }
                        }));
                        //if (mamual_update_required)
                        //    __update_listview_data(path);
                    }
                }, "获取文件信息...");
        }
        //UI: 按键
        private void listView_DirData_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r')
            {
                //回车
                if (string.IsNullOrEmpty(__current_listview_path)) return;
                if (listView_DirData.SelectedItems.Count != 1) return; //single select only
                var selected_item = listView_DirData.SelectedItems[0];
                var filename = selected_item.Text;
                var isdir = (selected_item.ImageKey == ":/dir/");
                if (isdir)
                    _asyncCall(delegate
                    {
                        lock (__thd_lck)
                        {
                            __update_listview_data(__current_listview_path + filename + "/", __sortType, __asc);
                        }
                    }, "获取文件信息...");
            }
            else if (e.KeyChar == '\b')
            {
                //退格
                if (string.IsNullOrEmpty(__current_listview_path)) return;
                //var parent_dir = FileListCacher.GetParentDir(__current_listview_path);
                //if (string.IsNullOrEmpty(parent_dir)) return;
                //_asyncCall(delegate
                //{
                //    lock (__thd_lck)
                //    {
                //        __update_listview_data(parent_dir, __sortType, __asc);
                //    }
                //}, "获取文件信息...");
            }
            else if (e.KeyChar == 1)
            {
                //ctrl+A
                foreach (ListViewItem item in listView_DirData.Items)
                {
                    item.Selected = true;
                }
            }
            else if (e.KeyChar == 24)
            {
                //ctrl+X
                剪切ToolStripMenuItem.PerformClick();
            }
            else if (e.KeyChar == 3)
            {
                //ctrl+C
                复制ToolStripMenuItem.PerformClick();
            }
            else if (e.KeyChar == 22)
            {
                //ctrl+V
                粘贴VToolStripMenuItem.PerformClick();
            }
        }
        //UI: 刷新
        private void 刷新EToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _asyncCall(delegate
            {
                if (string.IsNullOrEmpty(__current_listview_path)) __current_listview_path = "/";
                //_remote_file_list.RemoveFileCache(__current_listview_path);
                __update_listview_data(null, __sortType, __asc);
                __update_treeview_data(__current_listview_path);
                _update_quota();
            }, "获取文件信息...");
        }
        private void listView_DirData_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                删除ToolStripMenuItem_Click(sender, e);
            }
        }
        //UI: 上传文件
        //private void _on_upload_finished(object sender, UploadResultEventArgs e)
        //{
        //    _asyncCall(delegate
        //    {
        //        刷新EToolStripMenuItem_Click(sender, e);
        //    }, "更新文件信息...");
        //}
        private void 上传文件ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(__current_listview_path)) return;

            if (UploadFilePath.ShowDialog() != DialogResult.OK) return;
            var local_paths = UploadFilePath.FileNames;

            _asyncCall(delegate
            {
                lock (__thd_lck)
                {
                    bool cancelled = false;
                    bool always = false;
                    ondup o = ondup.newcopy;
                    bool skip = false;
                    foreach (var local_path in local_paths)
                    {
                        var temp_str = local_path.Replace(@"\", "/");
                        var file_name = temp_str.Split('/').Last();

                        var remote_path = __current_listview_path + file_name;

                        //var data = _remote_file_list.GetData(remote_path);
                        //if (data.FS_ID != 0 && !always)
                        //{
                        //    skip = false;
                        //    Invoke(new NoArgSTA(delegate
                        //    {
                        //        var frmQuery = new frmOverwrite(remote_path);
                        //        frmQuery.ShowDialog(this);
                        //        if (frmQuery.Cancelled) cancelled = true;
                        //        if (frmQuery.Always) always = true;
                        //        if (frmQuery.Confirmed) o = ondup.overwrite;
                        //        else if (frmQuery.SaveAsNew) o = ondup.newcopy;
                        //        else skip = true;
                        //    }));

                        //    if (cancelled) return;
                        //}
                        //if (skip && data.FS_ID != 0) continue;
                        //var new_class = new Uploader(this, _pcsAPI, remote_path, _local_file_list, local_path, o);
                        //new_class.TaskFinished += _on_upload_finished;
                        //uploadTransferList1.AddTask(new_class);
                    }
                }
            }, "获取文件信息...");

        }
        private void upload_files(string local_path, string remote_path, bool encrypt = false, bool always = false, bool skip = false)
        {
            var dir_info = new DirectoryInfo(local_path);
            //var data = _remote_file_list.GetFileList(remote_path).ToList();
            bool cancelled = false;
            ondup o = ondup.newcopy;
            foreach (var item in dir_info.GetFiles())
            {
                var cur_remote_path = remote_path + item.Name;
                if (encrypt && !cur_remote_path.EndsWith(".bcsd")) cur_remote_path += ".bcsd";
                var exist = false;// data.FindIndex(x => { return x.Path == cur_remote_path; }) != -1;
                if (!always && exist)
                {
                    skip = false;
                    //file exists
                    Invoke(new NoArgSTA(delegate
                    {
                        var frmQuery = new frmOverwrite(cur_remote_path);
                        frmQuery.ShowDialog(this);
                        if (frmQuery.Cancelled) cancelled = true;
                        if (frmQuery.Always) always = true;
                        if (frmQuery.Confirmed) o = ondup.overwrite;
                        else if (frmQuery.SaveAsNew) o = ondup.newcopy;
                        else skip = true;
                    }));

                    if (cancelled) return;
                }
                if (skip && exist) continue;
                //var new_class = new Uploader(this, _pcsAPI, cur_remote_path, _local_file_list, item.FullName, o, encrypt);
                //new_class.TaskFinished += _on_upload_finished;
                //uploadTransferList1.AddTask(new_class);
            }
            foreach (var item in dir_info.GetDirectories())
            {
                upload_files(local_path + item.Name + "/", remote_path + item.Name + "/", encrypt, always, skip);
            }
        }
        //UI: 上传文件夹
        private void 上传文件夹ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(__current_listview_path)) return;
            if (UploadFileDir.ShowDialog() != DialogResult.OK) return;
            _asyncCall(delegate
            {
                lock (__thd_lck)
                {
                    var path = UploadFileDir.SelectedPath.Replace(@"\", "/");
                    var pathname = path.Substring(path.LastIndexOf('/') + 1);
                    upload_files(path + "/", __current_listview_path + pathname + "/");
                }
            }, "获取文件信息...");
        }
        #endregion
        private void 新建文件夹WToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(__current_listview_path)) return;
            var frm = new frmCreateDir("新建文件夹");
            frm.ShowDialog(this);
            if (frm.Cancelled || string.IsNullOrEmpty(frm.FileName)) return;
            _asyncCall(delegate
            {
                lock (__thd_lck)
                {
                    var path = __current_listview_path + frm.FileName;
                    ObjectMetadata data = new ObjectMetadata();
                    try { data = _pcsAPI.CreateDirectory(path); }
                    catch (ErrnoException ex)
                    {
                        Invoke(new NoArgSTA(delegate { MessageBox.Show(this, "新建文件夹出错: 错误代码: " + ex.Errno, "出错了", MessageBoxButtons.OK, MessageBoxIcon.Error); }));
                    }
                    if (data.FS_ID == 0)
                    {
                        Invoke(new NoArgSTA(delegate { MessageBox.Show(this, "新建文件夹出错", "出错了", MessageBoxButtons.OK, MessageBoxIcon.Error); }));
                    }

                    刷新EToolStripMenuItem_Click(sender, e);
                }
            }, "新建文件夹...");
        }

        private void 重命名MToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(__current_listview_path)) return;
            var selected_data = listView_DirData.SelectedItems;
            if (selected_data == null || selected_data.Count != 1) return;

            var name = selected_data[0].Text;
            bool isdir = selected_data[0].ImageKey == ":/dir/";
            var frm = new frmCreateDir(name);
            frm.ShowDialog(this);

            if (frm.Cancelled || string.IsNullOrEmpty(frm.FileName) || frm.FileName == name) return;
            _asyncCall(delegate
            {
                lock (__thd_lck)
                {
                    var path = __current_listview_path + name;
                    //var srcData = _remote_file_list.GetData(path + (isdir ? "/" : ""));
                    //var dstData = _remote_file_list.GetData(FileListCacher.GetParentDir(path) + frm.FileName + (isdir ? "/" : ""));
                    //if (dstData.FS_ID != 0)
                    //{
                    //    Invoke(new NoArgSTA(delegate { MessageBox.Show(this, "重命名错误：目标文件已存在，请删除该文件或者刷新来解决", "犯错了", MessageBoxButtons.OK, MessageBoxIcon.Error); }));
                    //    return;
                    //}
                    //bool data = false;
                    //try { data = _pcsAPI.Rename(path, frm.FileName); }
                    //catch (ErrnoException ex)
                    //{
                    //    Invoke(new NoArgSTA(delegate { MessageBox.Show(this, "重命名出错: 错误代码: " + ex.Errno, "又是错误，崩溃了", MessageBoxButtons.OK, MessageBoxIcon.Error); }));
                    //}
                    //if (!data)
                    //{
                    //    Invoke(new NoArgSTA(delegate { MessageBox.Show(this, "重命名失败", "又是错误，崩溃了", MessageBoxButtons.OK, MessageBoxIcon.Error); }));
                    //}

                    //刷新EToolStripMenuItem_Click(sender, e);
                }
            }, "重命名...");
        }

        //取消后台线程
        private void bAsyncCancel_Click(object sender, EventArgs e)
        {
            _cancelAsyncCall();
        }


        #region tool strip - document operation
        private List<string> _clipboard_path = new List<string>();
        private bool _is_copy = false;

        private void 剪切ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(__current_listview_path)) return;
            var data = listView_DirData.SelectedItems;
            _clipboard_path = new List<string>();
            _is_copy = false;
            foreach (ListViewItem item in data)
            {
                if (item.ImageKey == ":/dir/")
                    _clipboard_path.Add(__current_listview_path + item.Text + "/");
                else
                    _clipboard_path.Add(__current_listview_path + item.Text);
            }

            if (_clipboard_path.Count > 0)
            {
                粘贴VToolStripMenuItem.Enabled = true;
            }
            else
            {
                粘贴VToolStripMenuItem.Enabled = false;
            }
        }

        private void 复制ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(__current_listview_path)) return;
            var data = listView_DirData.SelectedItems;
            _clipboard_path = new List<string>();
            _is_copy = true;
            foreach (ListViewItem item in data)
            {
                if (item.ImageKey == ":/dir/")
                    _clipboard_path.Add(__current_listview_path + item.Text + "/");
                else
                    _clipboard_path.Add(__current_listview_path + item.Text);
            }

            if (_clipboard_path.Count > 0)
            {
                粘贴VToolStripMenuItem.Enabled = true;
            }
            else
            {
                粘贴VToolStripMenuItem.Enabled = false;
            }
        }

        private void 粘贴VToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(__current_listview_path) || !粘贴VToolStripMenuItem.Enabled) return;
            var dest_list = new List<string>();
            var src_parent_list = new List<string>();
            for (int i = 0; i < _clipboard_path.Count; i++)
            {
                //var parent_path = FileListCacher.GetParentDir(_clipboard_path[i]);
                //if (!src_parent_list.Contains(parent_path)) src_parent_list.Add(parent_path);
                ////跳过相同文件夹的文件
                //if (parent_path == __current_listview_path)
                //{
                //    _clipboard_path.RemoveAt(i);
                //    i--;
                //    continue;
                //}
                //else
                //{
                //    var name = _clipboard_path[i].Substring(parent_path.Length);
                //    dest_list.Add(__current_listview_path + name);
                //}

                //if (_clipboard_path[i].EndsWith("/")) _clipboard_path[i] = _clipboard_path[i].Substring(0, _clipboard_path[i].Length - 1);
                //if (dest_list[i].EndsWith("/")) dest_list[i] = dest_list[i].Substring(0, dest_list[i].Length - 1);
            }

            var src_list = _clipboard_path;
            _clipboard_path = new List<string>();
            粘贴VToolStripMenuItem.Enabled = false;

            if (dest_list.Count == 0) return;

            _asyncCall(delegate
            {
                lock (__thd_lck)
                {
                    if (_is_copy)
                    {
                        bool suc = false;
                        try { suc = _pcsAPI.CopyPath(src_list, dest_list); }
                        catch (ErrnoException ex)
                        {
                            Invoke(new NoArgSTA(delegate { MessageBox.Show(this, "复制文件出错: 错误代码: " + ex.Errno, "错误了……", MessageBoxButtons.OK, MessageBoxIcon.Error); }));
                        }
                        if (!suc)
                        {
                            Invoke(new NoArgSTA(delegate { MessageBox.Show(this, "复制文件出错", "错误了……", MessageBoxButtons.OK, MessageBoxIcon.Error); }));
                        }
                    }
                    else
                    {
                        bool suc = false;
                        try { suc = _pcsAPI.MovePath(src_list, dest_list); }
                        catch (ErrnoException ex)
                        {
                            Invoke(new NoArgSTA(delegate { MessageBox.Show(this, "移动文件出错: 错误代码: " + ex.Errno, "错误了……", MessageBoxButtons.OK, MessageBoxIcon.Error); }));
                        }
                        if (!suc)
                        {
                            Invoke(new NoArgSTA(delegate { MessageBox.Show(this, "移动文件出错", "错误了……", MessageBoxButtons.OK, MessageBoxIcon.Error); }));
                        }
                    }
                    //foreach (var item in src_parent_list) { _remote_file_list.RemoveFileCache(item); }

                    刷新EToolStripMenuItem_Click(sender, e);
                }
            }, _is_copy ? "文件复制..." : "文件移动...");
        }
        #endregion

        private void 同步到云端ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //List<ObjectMetadata> delete_list = new List<ObjectMetadata>();
            //List<LocalFileData> upload_list = new List<TrackedData>();
            //if (UploadFileDir.ShowDialog() != DialogResult.OK) return;
            //var local_path = UploadFileDir.SelectedPath;
            //var temp_dirInfo = new DirectoryInfo(local_path);
            //var remote_path = __current_listview_path + temp_dirInfo.Name + "/";

            ////calling updating
            //_asyncCall(delegate
            //{
            //    bool suc = false;
            //    try { suc = _pcsAPI.GetSyncUpData(local_path, remote_path, _local_file_list, _file_list, out delete_list, out upload_list, true); }
            //    catch (ErrnoException ex)
            //    {
            //        Invoke(new NoArgSTA(delegate { MessageBox.Show(this, "同步出错: 未能获取文件信息（错误代码：" + ex.Errno + "），目录结构可能不一致", "Error message", MessageBoxButtons.OK, MessageBoxIcon.Error); }));
            //        return;
            //    }
            //    if (!suc)
            //    {
            //        Invoke(new NoArgSTA(delegate { MessageBox.Show(this, "同步出错: 未能获取文件信息，目录结构可能不一致", "Error message", MessageBoxButtons.OK, MessageBoxIcon.Error); }));
            //        return;
            //    }
            //    //delete path
            //    var delete_path = new string[delete_list.Count];
            //    for (int i = 0; i < delete_list.Count; i++)
            //    {
            //        delete_path[i] = delete_list[i].Path;
            //    }

            //    suc = false;
            //    try { suc = _pcsAPI.DeletePath(delete_path); }
            //    catch (ErrnoException ex)
            //    {
            //        Invoke(new NoArgSTA(delegate { MessageBox.Show(this, "同步出错: 未能删除文件/文件夹（错误代码：" + ex.Errno + "），目录结构可能不一致", "Error message", MessageBoxButtons.OK, MessageBoxIcon.Error); }));
            //        return;
            //    }
            //    if (!suc)
            //    {
            //        Invoke(new NoArgSTA(delegate { MessageBox.Show(this, "同步出错: 未能删除文件/文件夹，目录结构可能不一致", "Error message", MessageBoxButtons.OK, MessageBoxIcon.Error); }));
            //        return;
            //    }

            //    //create path
            //    foreach (var item in upload_list)
            //    {
            //        if (item.IsDir)
            //        {
            //            //converting relative path
            //            var relative_path = item.Path.Substring(local_path.Length + 1);
            //            var absolute_remote_path = remote_path + relative_path;
            //            ObjectMetadata data = new ObjectMetadata();
            //            try { data = _pcsAPI.CreateDirectory(absolute_remote_path); }
            //            catch (ErrnoException ex)
            //            {
            //                Invoke(new NoArgSTA(delegate { MessageBox.Show(this, "同步出错: 未能创建文件夹（错误代码：" + ex.Errno + "），目录结构可能不一致", "Error message", MessageBoxButtons.OK, MessageBoxIcon.Error); }));
            //                return;
            //            }
            //            if (data.FS_ID == 0)
            //            {
            //                Invoke(new NoArgSTA(delegate { MessageBox.Show(this, "同步出错: 未能创建文件夹，目录结构可能不一致", "Error message", MessageBoxButtons.OK, MessageBoxIcon.Error); }));
            //                return;
            //            }

            //        }
            //    }
            //    //calling refresh
            //    刷新EToolStripMenuItem_Click(sender, e);

            //    //calling uploading
            //    foreach (var item in upload_list)
            //    {
            //        if (!item.IsDir)
            //        {
            //            var relative_path = item.Path.Substring(local_path.Length + 1);
            //            var absolute_remote_path = remote_path + relative_path;
            //            var uploader = new Uploader(this, _pcsAPI, absolute_remote_path, _local_file_list, item.Path, ondup.overwrite, false);
            //            uploader.TaskFinished += _on_upload_finished;
            //            uploadTransferList1.AddTask(uploader);
            //        }
            //    }
            //}, "获取文件信息...（该过程可能会比较长）");
        }

        private void 同步到本地ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //if (downloadFileDir.ShowDialog() != DialogResult.OK) return;
            //var base_local_path = downloadFileDir.SelectedPath.Replace("\\", "/");
            //if (!base_local_path.EndsWith("/")) base_local_path += "/";
            //var base_remote_path = __current_listview_path;
            ////multi select resolving;
            //var selected_name = new List<string>();
            //foreach (ListViewItem item in listView_DirData.SelectedItems)
            //{
            //    selected_name.Add(item.Text + (item.ImageKey == ":/dir/" ? "/" : ""));
            //}
            //if (selected_name.Count == 0) return;

            //_asyncCall(delegate
            //{
            //    foreach (var item in selected_name)
            //    {
            //        var data = _file_list.GetData(base_remote_path + item);
            //        var local_path = base_local_path + item;
            //        var remote_path = base_remote_path + item;
            //        if (data.IsDir)
            //        {
            //            List<TrackedData> delete_list;
            //            List<ObjectMetadata> download_list;
            //            _pcsAPI.GetSyncDownData(local_path, remote_path, _local_file_list, _file_list, out delete_list, out download_list);

            //            //delete path
            //            foreach (var item2 in delete_list)
            //            {
            //                if (item2.IsDir)
            //                    Directory.Delete(item2.Path, true);
            //                else
            //                    File.Delete(item2.Path);
            //            }

            //            //create path
            //            foreach (var item2 in download_list)
            //            {
            //                if (item2.IsDir)
            //                {
            //                    var relative_path = item2.Path.Substring(remote_path.Length);
            //                    var absolute_local_path = local_path + relative_path;
            //                    Directory.CreateDirectory(absolute_local_path);
            //                }
            //            }

            //            //downloading
            //            foreach (var item2 in download_list)
            //            {
            //                if (!item2.IsDir)
            //                {
            //                    var relative_path = item2.Path.Substring(remote_path.Length);
            //                    var absolute_local_path = local_path + relative_path;
            //                    var frm = new frmDownload(_pcsAPI, item2, absolute_local_path);
            //                    downloadTransferList1.AddTask(frm);
            //                }
            //            }

            //        }
            //        else
            //        {
            //            var remote_data = _file_list.GetData(remote_path);
            //            var local_data = _local_file_list.GetDataFromPath(base_local_path, false).FirstOrDefault(o => o.Path == local_path);
            //            if (string.IsNullOrEmpty(local_data.MD5) || local_data.ContentSize != remote_data.Size || local_data.MD5 != remote_data.MD5)
            //            {
            //                var frm = new frmDownload(_pcsAPI, remote_data, local_path);
            //                downloadTransferList1.AddTask(frm);
            //            }
            //        }
            //    }
            //}, "获取文件信息...（该过程可能会比较长）");
        }

        private void 分享ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var path = __current_listview_path;
            var item_names = new List<string>();
            foreach (ListViewItem item in listView_DirData.SelectedItems)
            {
                if (item.ImageKey == ":/dir/")
                    item_names.Add(item.Text + "/");
                else
                    item_names.Add(item.Text);
            }
            if (item_names.Count == 0) return;

            _asyncCall(delegate
            {
                var data = new List<ulong>();
                foreach (var item in item_names)
                {
                    //data.Add(_remote_file_list.GetData(path + item).FS_ID);
                }
                Invoke(new NoArgSTA(delegate
                {
                    var frm_share = new frmCreateShare(_pcsAPI, data);
                    frm_share.ShowDialog(this);
                }));
            }, "获取分享文件信息...");
        }

        #region static config
        private void nMaxDownload_ValueChanged(object sender, EventArgs e)
        {
            StaticConfig.MAX_DOWNLOAD_PARALLEL_TASK_COUNT = (int)nMaxDownload.Value;
            StaticConfig.SaveStaticConfig();
        }

        private void nMaxUpload_ValueChanged(object sender, EventArgs e)
        {
            StaticConfig.MAX_UPLOAD_PARALLEL_TASK_COUNT = (int)nMaxUpload.Value;
            StaticConfig.SaveStaticConfig();
        }

        private void nDlThdCnt_ValueChanged(object sender, EventArgs e)
        {
            StaticConfig.MAX_DOWNLOAD_THREAD = (int)nDlThdCnt.Value;
            StaticConfig.SaveStaticConfig();
        }

        private void nListCount_ValueChanged(object sender, EventArgs e)
        {
            StaticConfig.MAX_LIST_SIZE = (int)nListCount.Value;
            StaticConfig.SaveStaticConfig();
        }

        private void nDebugListCnt_ValueChanged(object sender, EventArgs e)
        {
            StaticConfig.MAX_DEBUG_OUTPUT_COUNT = (int)nDebugListCnt.Value;
            StaticConfig.SaveStaticConfig();
        }
        private void cIgnoreUploadFail_CheckedChanged(object sender, EventArgs e)
        {
            StaticConfig.IGNORE_UPLOAD_ERROR = cIgnoreUploadFail.Checked;
            StaticConfig.SaveStaticConfig();
        }

        private void cAutoReupload_CheckedChanged(object sender, EventArgs e)
        {
            StaticConfig.AUTO_REUPLOAD_WHEN_MD5_ERROR = cAutoReupload.Checked;
            StaticConfig.SaveStaticConfig();
        }
        #endregion

        private void 上传加密文件ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!frmKeyCreate.HasRsaPrivateKey && !frmKeyCreate.HasAesKey)
            {
                frmKeyCreate frm = new frmKeyCreate();
                frm.ShowDialog(this);
                if (frm.Cancelled) return;
            }
            if (string.IsNullOrEmpty(__current_listview_path)) return;

            if (UploadFilePath.ShowDialog() != DialogResult.OK) return;
            var local_paths = UploadFilePath.FileNames;

            _asyncCall(delegate
            {
                lock (__thd_lck)
                {
                    bool cancelled = false;
                    bool always = false;
                    ondup o = ondup.newcopy;
                    bool skip = false;
                    foreach (var local_path in local_paths)
                    {
                        var temp_str = local_path.Replace(@"\", "/");
                        var file_name = temp_str.Split('/').Last();

                        var remote_path = __current_listview_path + file_name;
                        if (!remote_path.EndsWith(".bcsd"))
                            remote_path += ".bcsd";

                        //var data = _remote_file_list.GetData(remote_path);
                        //if (data.FS_ID != 0 && !always)
                        //{
                        //    skip = false;
                        //    Invoke(new NoArgSTA(delegate
                        //    {
                        //        var frmQuery = new frmOverwrite(remote_path);
                        //        frmQuery.ShowDialog(this);
                        //        if (frmQuery.Cancelled) cancelled = true;
                        //        if (frmQuery.Always) always = true;
                        //        if (frmQuery.Confirmed) o = ondup.overwrite;
                        //        else if (frmQuery.SaveAsNew) o = ondup.newcopy;
                        //        else skip = true;
                        //    }));

                        //    if (cancelled) return;
                        //}
                        //if (skip && data.FS_ID != 0) continue;

                        //var new_class = new Uploader(this, _pcsAPI, remote_path, _local_file_list, local_path, o, true);
                        //new_class.TaskFinished += _on_upload_finished;
                        //uploadTransferList1.AddTask(new_class);
                    }
                }
            }, "获取文件信息...");

        }

        private void 上传加密文件夹ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!frmKeyCreate.HasRsaPrivateKey && !frmKeyCreate.HasAesKey)
            {
                frmKeyCreate frm = new frmKeyCreate();
                frm.ShowDialog(this);
                if (frm.Cancelled) return;
            }
            if (string.IsNullOrEmpty(__current_listview_path)) return;
            if (UploadFileDir.ShowDialog() != DialogResult.OK) return;
            _asyncCall(delegate
            {
                lock (__thd_lck)
                {
                    var path = UploadFileDir.SelectedPath.Replace(@"\", "/");
                    var pathname = path.Substring(path.LastIndexOf('/') + 1);
                    upload_files(path + "/", __current_listview_path + pathname + "/", true);
                }
            }, "获取文件信息...");
        }

        private void bResetKey_Click(object sender, EventArgs e)
        {
            string result = Microsoft.VisualBasic.Interaction.InputBox("二次确认：\r\n请输入“重置密钥”或者“Reset Key”(区分大小写)进行密钥重置", "二次确认");
            if (result == "重置密钥" || result == "Reset Key")
            {
                frmKeyCreate.ResetKey();
                MessageBox.Show(this, "重置完成", "成功", MessageBoxButtons.OK);
            }
            else
            {
                MessageBox.Show(this, "已取消重置", "输入错误", MessageBoxButtons.OK);
            }
        }

    }
}
