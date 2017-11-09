namespace BaiduCloudSync
{
    partial class frmDownload
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmDownload));
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.lPath = new System.Windows.Forms.Label();
            this.lContentLength = new System.Windows.Forms.Label();
            this.lDownloadSize = new System.Windows.Forms.Label();
            this.pFinished = new System.Windows.Forms.ProgressBar();
            this.bStartPause = new System.Windows.Forms.Button();
            this.lThreadInfo = new System.Windows.Forms.ListView();
            this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader2 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader3 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader4 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader5 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.pSegments = new System.Windows.Forms.PictureBox();
            this.bCancel = new System.Windows.Forms.Button();
            this.bHide = new System.Windows.Forms.Button();
            this.label4 = new System.Windows.Forms.Label();
            this.lSpeed = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.thdCount = new System.Windows.Forms.NumericUpDown();
            this.label6 = new System.Windows.Forms.Label();
            this.lETA = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.pSegments)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.thdCount)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(59, 12);
            this.label1.TabIndex = 0;
            this.label1.Text = "文件路径:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 31);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(59, 12);
            this.label2.TabIndex = 0;
            this.label2.Text = "文件大小:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(199, 31);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(47, 12);
            this.label3.TabIndex = 0;
            this.label3.Text = "已下载:";
            // 
            // lPath
            // 
            this.lPath.AutoSize = true;
            this.lPath.Location = new System.Drawing.Point(77, 9);
            this.lPath.Name = "lPath";
            this.lPath.Size = new System.Drawing.Size(59, 12);
            this.lPath.TabIndex = 0;
            this.lPath.Text = "文件路径:";
            // 
            // lContentLength
            // 
            this.lContentLength.AutoSize = true;
            this.lContentLength.Location = new System.Drawing.Point(77, 31);
            this.lContentLength.Name = "lContentLength";
            this.lContentLength.Size = new System.Drawing.Size(59, 12);
            this.lContentLength.TabIndex = 0;
            this.lContentLength.Text = "文件大小:";
            // 
            // lDownloadSize
            // 
            this.lDownloadSize.AutoSize = true;
            this.lDownloadSize.Location = new System.Drawing.Point(252, 31);
            this.lDownloadSize.Name = "lDownloadSize";
            this.lDownloadSize.Size = new System.Drawing.Size(47, 12);
            this.lDownloadSize.TabIndex = 0;
            this.lDownloadSize.Text = "已下载:";
            // 
            // pFinished
            // 
            this.pFinished.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pFinished.Location = new System.Drawing.Point(14, 103);
            this.pFinished.Name = "pFinished";
            this.pFinished.Size = new System.Drawing.Size(515, 20);
            this.pFinished.TabIndex = 1;
            // 
            // bStartPause
            // 
            this.bStartPause.Location = new System.Drawing.Point(14, 65);
            this.bStartPause.Name = "bStartPause";
            this.bStartPause.Size = new System.Drawing.Size(100, 32);
            this.bStartPause.TabIndex = 2;
            this.bStartPause.Text = "开始下载";
            this.bStartPause.UseVisualStyleBackColor = true;
            this.bStartPause.Click += new System.EventHandler(this.bStartPause_Click);
            // 
            // lThreadInfo
            // 
            this.lThreadInfo.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lThreadInfo.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1,
            this.columnHeader2,
            this.columnHeader3,
            this.columnHeader4,
            this.columnHeader5});
            this.lThreadInfo.FullRowSelect = true;
            this.lThreadInfo.Location = new System.Drawing.Point(14, 158);
            this.lThreadInfo.Name = "lThreadInfo";
            this.lThreadInfo.Size = new System.Drawing.Size(515, 89);
            this.lThreadInfo.TabIndex = 3;
            this.lThreadInfo.UseCompatibleStateImageBehavior = false;
            this.lThreadInfo.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader1
            // 
            this.columnHeader1.Text = "序号";
            this.columnHeader1.Width = 50;
            // 
            // columnHeader2
            // 
            this.columnHeader2.Text = "URL";
            this.columnHeader2.Width = 180;
            // 
            // columnHeader3
            // 
            this.columnHeader3.Text = "下载大小";
            this.columnHeader3.Width = 72;
            // 
            // columnHeader4
            // 
            this.columnHeader4.Text = "位置";
            this.columnHeader4.Width = 73;
            // 
            // columnHeader5
            // 
            this.columnHeader5.Text = "线程状态";
            // 
            // pSegments
            // 
            this.pSegments.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pSegments.BackColor = System.Drawing.Color.White;
            this.pSegments.Location = new System.Drawing.Point(14, 129);
            this.pSegments.Name = "pSegments";
            this.pSegments.Size = new System.Drawing.Size(515, 23);
            this.pSegments.TabIndex = 4;
            this.pSegments.TabStop = false;
            // 
            // bCancel
            // 
            this.bCancel.Location = new System.Drawing.Point(120, 65);
            this.bCancel.Name = "bCancel";
            this.bCancel.Size = new System.Drawing.Size(100, 32);
            this.bCancel.TabIndex = 2;
            this.bCancel.Text = "取消下载";
            this.bCancel.UseVisualStyleBackColor = true;
            this.bCancel.Click += new System.EventHandler(this.bCancel_Click);
            // 
            // bHide
            // 
            this.bHide.Location = new System.Drawing.Point(226, 65);
            this.bHide.Name = "bHide";
            this.bHide.Size = new System.Drawing.Size(100, 32);
            this.bHide.TabIndex = 2;
            this.bHide.Text = "隐藏窗口";
            this.bHide.UseVisualStyleBackColor = true;
            this.bHide.Click += new System.EventHandler(this.bHide_Click);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(12, 50);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(35, 12);
            this.label4.TabIndex = 0;
            this.label4.Text = "速度:";
            // 
            // lSpeed
            // 
            this.lSpeed.AutoSize = true;
            this.lSpeed.Location = new System.Drawing.Point(53, 50);
            this.lSpeed.Name = "lSpeed";
            this.lSpeed.Size = new System.Drawing.Size(35, 12);
            this.lSpeed.TabIndex = 0;
            this.lSpeed.Text = "速度:";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(343, 75);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(71, 12);
            this.label5.TabIndex = 0;
            this.label5.Text = "下载线程数:";
            // 
            // thdCount
            // 
            this.thdCount.Location = new System.Drawing.Point(420, 72);
            this.thdCount.Maximum = new decimal(new int[] {
            200,
            0,
            0,
            0});
            this.thdCount.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.thdCount.Name = "thdCount";
            this.thdCount.Size = new System.Drawing.Size(104, 21);
            this.thdCount.TabIndex = 5;
            this.thdCount.Value = new decimal(new int[] {
            96,
            0,
            0,
            0});
            this.thdCount.ValueChanged += new System.EventHandler(this.thdCount_ValueChanged);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(199, 50);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(59, 12);
            this.label6.TabIndex = 0;
            this.label6.Text = "剩余时间:";
            // 
            // lETA
            // 
            this.lETA.AutoSize = true;
            this.lETA.Location = new System.Drawing.Point(264, 50);
            this.lETA.Name = "lETA";
            this.lETA.Size = new System.Drawing.Size(59, 12);
            this.lETA.TabIndex = 0;
            this.lETA.Text = "剩余时间:";
            // 
            // frmDownload
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(541, 251);
            this.Controls.Add(this.thdCount);
            this.Controls.Add(this.pSegments);
            this.Controls.Add(this.lThreadInfo);
            this.Controls.Add(this.bHide);
            this.Controls.Add(this.bCancel);
            this.Controls.Add(this.bStartPause);
            this.Controls.Add(this.pFinished);
            this.Controls.Add(this.lETA);
            this.Controls.Add(this.lSpeed);
            this.Controls.Add(this.lDownloadSize);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.lContentLength);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.lPath);
            this.Controls.Add(this.label1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "frmDownload";
            this.Text = "百度白嫖会员加速下载器";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.frmDownload_FormClosing);
            this.Load += new System.EventHandler(this.frmDownload_Load);
            ((System.ComponentModel.ISupportInitialize)(this.pSegments)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.thdCount)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label lPath;
        private System.Windows.Forms.Label lContentLength;
        private System.Windows.Forms.Label lDownloadSize;
        private System.Windows.Forms.ProgressBar pFinished;
        private System.Windows.Forms.Button bStartPause;
        private System.Windows.Forms.ListView lThreadInfo;
        private System.Windows.Forms.ColumnHeader columnHeader1;
        private System.Windows.Forms.ColumnHeader columnHeader2;
        private System.Windows.Forms.ColumnHeader columnHeader3;
        private System.Windows.Forms.ColumnHeader columnHeader4;
        private System.Windows.Forms.PictureBox pSegments;
        private System.Windows.Forms.Button bCancel;
        private System.Windows.Forms.ColumnHeader columnHeader5;
        private System.Windows.Forms.Button bHide;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label lSpeed;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.NumericUpDown thdCount;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label lETA;
    }
}