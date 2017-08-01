using System;

namespace BaiduCloudSync
{
    partial class frmMain
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.TreeNode treeNode1 = new System.Windows.Forms.TreeNode("<根目录>");
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmMain));
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.上传ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.上传文件ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.上传文件夹ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.下载ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.同步ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.同步到云端ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.同步到本地ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
            this.刷新EToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.分享ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.动态链接ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.创建文件链接ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.由链接生成文件ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.重命名MToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.删除ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.新建文件夹WToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.剪切ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.复制ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.粘贴VToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.属性ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pQuota = new System.Windows.Forms.ProgressBar();
            this.lQuota = new System.Windows.Forms.Label();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.treeView_DirList = new System.Windows.Forms.TreeView();
            this.listView_DirData = new System.Windows.Forms.ListView();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.downloadTransferList1 = new BaiduCloudSync.DownloadTransferList();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.uploadTransferList1 = new BaiduCloudSync.UploadTransferList();
            this.tabPage4 = new System.Windows.Forms.TabPage();
            this.ctlDebugOutput1 = new BaiduCloudSync.CtlDebugOutput();
            this.downloadFilePath = new System.Windows.Forms.SaveFileDialog();
            this.downloadFileDir = new System.Windows.Forms.FolderBrowserDialog();
            this.UploadFilePath = new System.Windows.Forms.OpenFileDialog();
            this.UploadFileDir = new System.Windows.Forms.FolderBrowserDialog();
            this.lAsyncStatus = new System.Windows.Forms.Label();
            this.bAsyncCancel = new System.Windows.Forms.Button();
            this.tabPage5 = new System.Windows.Forms.TabPage();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.nMaxDownload = new System.Windows.Forms.NumericUpDown();
            this.label3 = new System.Windows.Forms.Label();
            this.nMaxUpload = new System.Windows.Forms.NumericUpDown();
            this.nDlThdCnt = new System.Windows.Forms.NumericUpDown();
            this.label4 = new System.Windows.Forms.Label();
            this.nListCount = new System.Windows.Forms.NumericUpDown();
            this.label5 = new System.Windows.Forms.Label();
            this.nDebugListCnt = new System.Windows.Forms.NumericUpDown();
            this.label6 = new System.Windows.Forms.Label();
            this.contextMenuStrip1.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.tabPage4.SuspendLayout();
            this.tabPage5.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nMaxDownload)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nMaxUpload)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nDlThdCnt)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nListCount)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nDebugListCnt)).BeginInit();
            this.SuspendLayout();
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.ImageScalingSize = new System.Drawing.Size(36, 36);
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.上传ToolStripMenuItem,
            this.下载ToolStripMenuItem,
            this.同步ToolStripMenuItem,
            this.toolStripSeparator4,
            this.刷新EToolStripMenuItem,
            this.分享ToolStripMenuItem,
            this.动态链接ToolStripMenuItem,
            this.toolStripSeparator1,
            this.重命名MToolStripMenuItem,
            this.删除ToolStripMenuItem,
            this.新建文件夹WToolStripMenuItem,
            this.toolStripSeparator3,
            this.剪切ToolStripMenuItem,
            this.复制ToolStripMenuItem,
            this.粘贴VToolStripMenuItem,
            this.toolStripSeparator2,
            this.属性ToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(161, 314);
            // 
            // 上传ToolStripMenuItem
            // 
            this.上传ToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.上传文件ToolStripMenuItem,
            this.上传文件夹ToolStripMenuItem});
            this.上传ToolStripMenuItem.Name = "上传ToolStripMenuItem";
            this.上传ToolStripMenuItem.Size = new System.Drawing.Size(160, 22);
            this.上传ToolStripMenuItem.Text = "上传 (&U)";
            // 
            // 上传文件ToolStripMenuItem
            // 
            this.上传文件ToolStripMenuItem.Name = "上传文件ToolStripMenuItem";
            this.上传文件ToolStripMenuItem.Size = new System.Drawing.Size(136, 22);
            this.上传文件ToolStripMenuItem.Text = "上传文件";
            this.上传文件ToolStripMenuItem.Click += new System.EventHandler(this.上传文件ToolStripMenuItem_Click);
            // 
            // 上传文件夹ToolStripMenuItem
            // 
            this.上传文件夹ToolStripMenuItem.Name = "上传文件夹ToolStripMenuItem";
            this.上传文件夹ToolStripMenuItem.Size = new System.Drawing.Size(136, 22);
            this.上传文件夹ToolStripMenuItem.Text = "上传文件夹";
            this.上传文件夹ToolStripMenuItem.Click += new System.EventHandler(this.上传文件夹ToolStripMenuItem_Click);
            // 
            // 下载ToolStripMenuItem
            // 
            this.下载ToolStripMenuItem.Name = "下载ToolStripMenuItem";
            this.下载ToolStripMenuItem.Size = new System.Drawing.Size(160, 22);
            this.下载ToolStripMenuItem.Text = "下载 (&O)";
            this.下载ToolStripMenuItem.Click += new System.EventHandler(this.下载ToolStripMenuItem_Click);
            // 
            // 同步ToolStripMenuItem
            // 
            this.同步ToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.同步到云端ToolStripMenuItem,
            this.同步到本地ToolStripMenuItem});
            this.同步ToolStripMenuItem.Name = "同步ToolStripMenuItem";
            this.同步ToolStripMenuItem.Size = new System.Drawing.Size(160, 22);
            this.同步ToolStripMenuItem.Text = "同步 (&S)";
            // 
            // 同步到云端ToolStripMenuItem
            // 
            this.同步到云端ToolStripMenuItem.Name = "同步到云端ToolStripMenuItem";
            this.同步到云端ToolStripMenuItem.Size = new System.Drawing.Size(157, 22);
            this.同步到云端ToolStripMenuItem.Text = "同步到云端 (&U)";
            this.同步到云端ToolStripMenuItem.Click += new System.EventHandler(this.同步到云端ToolStripMenuItem_Click);
            // 
            // 同步到本地ToolStripMenuItem
            // 
            this.同步到本地ToolStripMenuItem.Name = "同步到本地ToolStripMenuItem";
            this.同步到本地ToolStripMenuItem.Size = new System.Drawing.Size(157, 22);
            this.同步到本地ToolStripMenuItem.Text = "同步到本地 (&D)";
            this.同步到本地ToolStripMenuItem.Click += new System.EventHandler(this.同步到本地ToolStripMenuItem_Click);
            // 
            // toolStripSeparator4
            // 
            this.toolStripSeparator4.Name = "toolStripSeparator4";
            this.toolStripSeparator4.Size = new System.Drawing.Size(157, 6);
            // 
            // 刷新EToolStripMenuItem
            // 
            this.刷新EToolStripMenuItem.Name = "刷新EToolStripMenuItem";
            this.刷新EToolStripMenuItem.Size = new System.Drawing.Size(160, 22);
            this.刷新EToolStripMenuItem.Text = "刷新 (&E)";
            this.刷新EToolStripMenuItem.Click += new System.EventHandler(this.刷新EToolStripMenuItem_Click);
            // 
            // 分享ToolStripMenuItem
            // 
            this.分享ToolStripMenuItem.Name = "分享ToolStripMenuItem";
            this.分享ToolStripMenuItem.Size = new System.Drawing.Size(160, 22);
            this.分享ToolStripMenuItem.Text = "创建分享 (&S)";
            this.分享ToolStripMenuItem.Click += new System.EventHandler(this.分享ToolStripMenuItem_Click);
            // 
            // 动态链接ToolStripMenuItem
            // 
            this.动态链接ToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.创建文件链接ToolStripMenuItem,
            this.由链接生成文件ToolStripMenuItem});
            this.动态链接ToolStripMenuItem.Name = "动态链接ToolStripMenuItem";
            this.动态链接ToolStripMenuItem.Size = new System.Drawing.Size(160, 22);
            this.动态链接ToolStripMenuItem.Text = "动态链接 (&L)";
            // 
            // 创建文件链接ToolStripMenuItem
            // 
            this.创建文件链接ToolStripMenuItem.Name = "创建文件链接ToolStripMenuItem";
            this.创建文件链接ToolStripMenuItem.Size = new System.Drawing.Size(179, 22);
            this.创建文件链接ToolStripMenuItem.Text = "创建文件链接 (&F)";
            this.创建文件链接ToolStripMenuItem.Click += new System.EventHandler(this.创建文件链接ToolStripMenuItem_Click);
            // 
            // 由链接生成文件ToolStripMenuItem
            // 
            this.由链接生成文件ToolStripMenuItem.Name = "由链接生成文件ToolStripMenuItem";
            this.由链接生成文件ToolStripMenuItem.Size = new System.Drawing.Size(179, 22);
            this.由链接生成文件ToolStripMenuItem.Text = "由链接生成文件 (&T)";
            this.由链接生成文件ToolStripMenuItem.Click += new System.EventHandler(this.由链接生成文件ToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(157, 6);
            // 
            // 重命名MToolStripMenuItem
            // 
            this.重命名MToolStripMenuItem.Name = "重命名MToolStripMenuItem";
            this.重命名MToolStripMenuItem.Size = new System.Drawing.Size(160, 22);
            this.重命名MToolStripMenuItem.Text = "重命名 (&M)";
            this.重命名MToolStripMenuItem.Click += new System.EventHandler(this.重命名MToolStripMenuItem_Click);
            // 
            // 删除ToolStripMenuItem
            // 
            this.删除ToolStripMenuItem.Name = "删除ToolStripMenuItem";
            this.删除ToolStripMenuItem.Size = new System.Drawing.Size(160, 22);
            this.删除ToolStripMenuItem.Text = "删除 (&D)";
            this.删除ToolStripMenuItem.Click += new System.EventHandler(this.删除ToolStripMenuItem_Click);
            // 
            // 新建文件夹WToolStripMenuItem
            // 
            this.新建文件夹WToolStripMenuItem.Name = "新建文件夹WToolStripMenuItem";
            this.新建文件夹WToolStripMenuItem.Size = new System.Drawing.Size(160, 22);
            this.新建文件夹WToolStripMenuItem.Text = "新建文件夹 (&W)";
            this.新建文件夹WToolStripMenuItem.Click += new System.EventHandler(this.新建文件夹WToolStripMenuItem_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(157, 6);
            // 
            // 剪切ToolStripMenuItem
            // 
            this.剪切ToolStripMenuItem.Name = "剪切ToolStripMenuItem";
            this.剪切ToolStripMenuItem.Size = new System.Drawing.Size(160, 22);
            this.剪切ToolStripMenuItem.Text = "剪切 (&T)";
            this.剪切ToolStripMenuItem.Click += new System.EventHandler(this.剪切ToolStripMenuItem_Click);
            // 
            // 复制ToolStripMenuItem
            // 
            this.复制ToolStripMenuItem.Name = "复制ToolStripMenuItem";
            this.复制ToolStripMenuItem.Size = new System.Drawing.Size(160, 22);
            this.复制ToolStripMenuItem.Text = "复制 (&C)";
            this.复制ToolStripMenuItem.Click += new System.EventHandler(this.复制ToolStripMenuItem_Click);
            // 
            // 粘贴VToolStripMenuItem
            // 
            this.粘贴VToolStripMenuItem.Enabled = false;
            this.粘贴VToolStripMenuItem.Name = "粘贴VToolStripMenuItem";
            this.粘贴VToolStripMenuItem.Size = new System.Drawing.Size(160, 22);
            this.粘贴VToolStripMenuItem.Text = "粘贴 (&V)";
            this.粘贴VToolStripMenuItem.Click += new System.EventHandler(this.粘贴VToolStripMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(157, 6);
            // 
            // 属性ToolStripMenuItem
            // 
            this.属性ToolStripMenuItem.Name = "属性ToolStripMenuItem";
            this.属性ToolStripMenuItem.Size = new System.Drawing.Size(160, 22);
            this.属性ToolStripMenuItem.Text = "属性 (&R)";
            this.属性ToolStripMenuItem.Click += new System.EventHandler(this.属性ToolStripMenuItem_Click);
            // 
            // pQuota
            // 
            this.pQuota.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.pQuota.Location = new System.Drawing.Point(4, 407);
            this.pQuota.Name = "pQuota";
            this.pQuota.Size = new System.Drawing.Size(305, 18);
            this.pQuota.TabIndex = 3;
            // 
            // lQuota
            // 
            this.lQuota.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lQuota.AutoSize = true;
            this.lQuota.Location = new System.Drawing.Point(4, 431);
            this.lQuota.Name = "lQuota";
            this.lQuota.Size = new System.Drawing.Size(0, 12);
            this.lQuota.TabIndex = 4;
            // 
            // tabControl1
            // 
            this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Controls.Add(this.tabPage4);
            this.tabControl1.Controls.Add(this.tabPage5);
            this.tabControl1.Location = new System.Drawing.Point(3, 3);
            this.tabControl1.Multiline = true;
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(734, 405);
            this.tabControl1.TabIndex = 5;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.splitContainer1);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(726, 379);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "文件管理";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(3, 3);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.treeView_DirList);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.listView_DirData);
            this.splitContainer1.Size = new System.Drawing.Size(720, 373);
            this.splitContainer1.SplitterDistance = 179;
            this.splitContainer1.TabIndex = 4;
            // 
            // treeView_DirList
            // 
            this.treeView_DirList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeView_DirList.FullRowSelect = true;
            this.treeView_DirList.HideSelection = false;
            this.treeView_DirList.Location = new System.Drawing.Point(0, 0);
            this.treeView_DirList.Margin = new System.Windows.Forms.Padding(1);
            this.treeView_DirList.Name = "treeView_DirList";
            treeNode1.Name = "RootNode";
            treeNode1.Text = "<根目录>";
            this.treeView_DirList.Nodes.AddRange(new System.Windows.Forms.TreeNode[] {
            treeNode1});
            this.treeView_DirList.PathSeparator = "/";
            this.treeView_DirList.Size = new System.Drawing.Size(179, 373);
            this.treeView_DirList.TabIndex = 3;
            this.treeView_DirList.BeforeExpand += new System.Windows.Forms.TreeViewCancelEventHandler(this.treeView_DirList_BeforeExpand);
            this.treeView_DirList.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.treeView_DirList_AfterSelect);
            this.treeView_DirList.KeyUp += new System.Windows.Forms.KeyEventHandler(this.treeView_DirList_KeyUp);
            this.treeView_DirList.MouseClick += new System.Windows.Forms.MouseEventHandler(this.treeView_DirList_MouseClick);
            // 
            // listView_DirData
            // 
            this.listView_DirData.ContextMenuStrip = this.contextMenuStrip1;
            this.listView_DirData.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listView_DirData.Location = new System.Drawing.Point(0, 0);
            this.listView_DirData.Name = "listView_DirData";
            this.listView_DirData.Size = new System.Drawing.Size(537, 373);
            this.listView_DirData.TabIndex = 0;
            this.listView_DirData.UseCompatibleStateImageBehavior = false;
            this.listView_DirData.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.listView_DirData_KeyPress);
            this.listView_DirData.KeyUp += new System.Windows.Forms.KeyEventHandler(this.listView_DirData_KeyUp);
            this.listView_DirData.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.listView_DirData_MouseDoubleClick);
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.downloadTransferList1);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(726, 379);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "下载列表";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // downloadTransferList1
            // 
            this.downloadTransferList1.BackColor = System.Drawing.SystemColors.Control;
            this.downloadTransferList1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.downloadTransferList1.Location = new System.Drawing.Point(3, 3);
            this.downloadTransferList1.Name = "downloadTransferList1";
            this.downloadTransferList1.Size = new System.Drawing.Size(720, 373);
            this.downloadTransferList1.TabIndex = 0;
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.uploadTransferList1);
            this.tabPage3.Location = new System.Drawing.Point(4, 22);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage3.Size = new System.Drawing.Size(726, 379);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "上传列表";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // uploadTransferList1
            // 
            this.uploadTransferList1.BackColor = System.Drawing.SystemColors.Control;
            this.uploadTransferList1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uploadTransferList1.Location = new System.Drawing.Point(3, 3);
            this.uploadTransferList1.Name = "uploadTransferList1";
            this.uploadTransferList1.Size = new System.Drawing.Size(720, 373);
            this.uploadTransferList1.TabIndex = 0;
            // 
            // tabPage4
            // 
            this.tabPage4.Controls.Add(this.ctlDebugOutput1);
            this.tabPage4.Location = new System.Drawing.Point(4, 22);
            this.tabPage4.Name = "tabPage4";
            this.tabPage4.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage4.Size = new System.Drawing.Size(726, 379);
            this.tabPage4.TabIndex = 3;
            this.tabPage4.Text = "调试输出";
            this.tabPage4.UseVisualStyleBackColor = true;
            // 
            // ctlDebugOutput1
            // 
            this.ctlDebugOutput1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ctlDebugOutput1.Location = new System.Drawing.Point(3, 3);
            this.ctlDebugOutput1.Name = "ctlDebugOutput1";
            this.ctlDebugOutput1.Size = new System.Drawing.Size(720, 373);
            this.ctlDebugOutput1.TabIndex = 0;
            // 
            // UploadFilePath
            // 
            this.UploadFilePath.Multiselect = true;
            // 
            // lAsyncStatus
            // 
            this.lAsyncStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lAsyncStatus.Location = new System.Drawing.Point(315, 417);
            this.lAsyncStatus.Name = "lAsyncStatus";
            this.lAsyncStatus.Size = new System.Drawing.Size(346, 19);
            this.lAsyncStatus.TabIndex = 6;
            this.lAsyncStatus.Text = "当前执行:";
            this.lAsyncStatus.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.lAsyncStatus.Visible = false;
            // 
            // bAsyncCancel
            // 
            this.bAsyncCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.bAsyncCancel.Location = new System.Drawing.Point(667, 411);
            this.bAsyncCancel.Name = "bAsyncCancel";
            this.bAsyncCancel.Size = new System.Drawing.Size(67, 31);
            this.bAsyncCancel.TabIndex = 7;
            this.bAsyncCancel.Text = "取消执行";
            this.bAsyncCancel.UseVisualStyleBackColor = true;
            this.bAsyncCancel.Visible = false;
            this.bAsyncCancel.Click += new System.EventHandler(this.bAsyncCancel_Click);
            // 
            // tabPage5
            // 
            this.tabPage5.Controls.Add(this.label6);
            this.tabPage5.Controls.Add(this.nDebugListCnt);
            this.tabPage5.Controls.Add(this.nListCount);
            this.tabPage5.Controls.Add(this.nDlThdCnt);
            this.tabPage5.Controls.Add(this.label5);
            this.tabPage5.Controls.Add(this.label4);
            this.tabPage5.Controls.Add(this.nMaxUpload);
            this.tabPage5.Controls.Add(this.label3);
            this.tabPage5.Controls.Add(this.nMaxDownload);
            this.tabPage5.Controls.Add(this.label2);
            this.tabPage5.Controls.Add(this.label1);
            this.tabPage5.Location = new System.Drawing.Point(4, 22);
            this.tabPage5.Name = "tabPage5";
            this.tabPage5.Size = new System.Drawing.Size(726, 379);
            this.tabPage5.TabIndex = 4;
            this.tabPage5.Text = "设置&使用说明";
            this.tabPage5.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(14, 10);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(89, 12);
            this.label1.TabIndex = 0;
            this.label1.Text = "最大下载任务数";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(14, 34);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(89, 12);
            this.label2.TabIndex = 0;
            this.label2.Text = "最大上传任务数";
            // 
            // nMaxDownload
            // 
            this.nMaxDownload.Location = new System.Drawing.Point(121, 8);
            this.nMaxDownload.Maximum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.nMaxDownload.Name = "nMaxDownload";
            this.nMaxDownload.Size = new System.Drawing.Size(65, 21);
            this.nMaxDownload.TabIndex = 1;
            this.nMaxDownload.ValueChanged += new System.EventHandler(this.nMaxDownload_ValueChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(14, 58);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(53, 12);
            this.label3.TabIndex = 0;
            this.label3.Text = "下载线程";
            // 
            // nMaxUpload
            // 
            this.nMaxUpload.Location = new System.Drawing.Point(121, 32);
            this.nMaxUpload.Maximum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.nMaxUpload.Name = "nMaxUpload";
            this.nMaxUpload.Size = new System.Drawing.Size(65, 21);
            this.nMaxUpload.TabIndex = 1;
            this.nMaxUpload.ValueChanged += new System.EventHandler(this.nMaxUpload_ValueChanged);
            // 
            // nDlThdCnt
            // 
            this.nDlThdCnt.Location = new System.Drawing.Point(121, 56);
            this.nDlThdCnt.Maximum = new decimal(new int[] {
            200,
            0,
            0,
            0});
            this.nDlThdCnt.Name = "nDlThdCnt";
            this.nDlThdCnt.Size = new System.Drawing.Size(65, 21);
            this.nDlThdCnt.TabIndex = 1;
            this.nDlThdCnt.ValueChanged += new System.EventHandler(this.nDlThdCnt_ValueChanged);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(14, 82);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(101, 12);
            this.label4.TabIndex = 0;
            this.label4.Text = "列表显示的任务数";
            // 
            // nListCount
            // 
            this.nListCount.Location = new System.Drawing.Point(121, 80);
            this.nListCount.Maximum = new decimal(new int[] {
            200,
            0,
            0,
            0});
            this.nListCount.Name = "nListCount";
            this.nListCount.Size = new System.Drawing.Size(65, 21);
            this.nListCount.TabIndex = 1;
            this.nListCount.ValueChanged += new System.EventHandler(this.nListCount_ValueChanged);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(14, 106);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(101, 12);
            this.label5.TabIndex = 0;
            this.label5.Text = "调试输出数量限制";
            // 
            // nDebugListCnt
            // 
            this.nDebugListCnt.Location = new System.Drawing.Point(121, 104);
            this.nDebugListCnt.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.nDebugListCnt.Name = "nDebugListCnt";
            this.nDebugListCnt.Size = new System.Drawing.Size(65, 21);
            this.nDebugListCnt.TabIndex = 1;
            this.nDebugListCnt.ValueChanged += new System.EventHandler(this.nDebugListCnt_ValueChanged);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(218, 10);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(167, 168);
            this.label6.TabIndex = 2;
            this.label6.Text = "简单的使用说明:\r\n以文件管理的右边栏为准：\r\n鼠标右击 显示操作菜单\r\n鼠标双击文件夹 进入该文件夹\r\n键盘快捷键\r\nBackspace: 返回上层目录\r\nEn" +
    "ter: 进入文件夹\r\nCtrl+C: 复制\r\nCtrl+X: 剪切\r\nCtrl+V: 粘贴\r\n\r\n目前就这么多了\r\n上传的分块跟暂停还在坑着\r\n程序关闭时保存" +
    "列表也在坑着";
            // 
            // frmMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(737, 448);
            this.Controls.Add(this.bAsyncCancel);
            this.Controls.Add(this.lAsyncStatus);
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.lQuota);
            this.Controls.Add(this.pQuota);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "frmMain";
            this.Text = "绝对零度云管家";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.Load += new System.EventHandler(this.Form1_Load);
            this.contextMenuStrip1.ResumeLayout(false);
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            this.tabPage3.ResumeLayout(false);
            this.tabPage4.ResumeLayout(false);
            this.tabPage5.ResumeLayout(false);
            this.tabPage5.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nMaxDownload)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nMaxUpload)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nDlThdCnt)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nListCount)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nDebugListCnt)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        
        #endregion
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem 上传ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 上传文件ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 上传文件夹ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 下载ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 同步ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 同步到云端ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 同步到本地ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 动态链接ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 创建文件链接ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 由链接生成文件ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 分享ToolStripMenuItem;
        private System.Windows.Forms.ProgressBar pQuota;
        private System.Windows.Forms.Label lQuota;
        private System.Windows.Forms.ToolStripMenuItem 删除ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 属性ToolStripMenuItem;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TreeView treeView_DirList;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator4;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem 重命名MToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 剪切ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 复制ToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem 粘贴VToolStripMenuItem;
        private System.Windows.Forms.SaveFileDialog downloadFilePath;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.ListView listView_DirData;
        private System.Windows.Forms.ToolStripMenuItem 刷新EToolStripMenuItem;
        private System.Windows.Forms.FolderBrowserDialog downloadFileDir;
        private System.Windows.Forms.OpenFileDialog UploadFilePath;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.FolderBrowserDialog UploadFileDir;
        private System.Windows.Forms.ToolStripMenuItem 新建文件夹WToolStripMenuItem;
        private System.Windows.Forms.Label lAsyncStatus;
        private System.Windows.Forms.Button bAsyncCancel;
        private DownloadTransferList downloadTransferList1;
        private UploadTransferList uploadTransferList1;
        private System.Windows.Forms.TabPage tabPage4;
        private CtlDebugOutput ctlDebugOutput1;
        private System.Windows.Forms.TabPage tabPage5;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.NumericUpDown nMaxUpload;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.NumericUpDown nMaxDownload;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.NumericUpDown nDlThdCnt;
        private System.Windows.Forms.NumericUpDown nDebugListCnt;
        private System.Windows.Forms.NumericUpDown nListCount;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label6;
    }
}

