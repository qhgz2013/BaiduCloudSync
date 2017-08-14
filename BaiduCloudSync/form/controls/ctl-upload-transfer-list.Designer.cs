namespace BaiduCloudSync
{
    partial class UploadTransferList
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

        #region 组件设计器生成的代码

        /// <summary> 
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.scroll = new System.Windows.Forms.VScrollBar();
            this.label1 = new System.Windows.Forms.Label();
            this.lTaskCount = new System.Windows.Forms.Label();
            this.pFinishRate = new System.Windows.Forms.ProgressBar();
            this.lDownloadSize = new System.Windows.Forms.Label();
            this.lSpeed = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.bStart = new System.Windows.Forms.Button();
            this.bPause = new System.Windows.Forms.Button();
            this.bCancel = new System.Windows.Forms.Button();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // scroll
            // 
            this.scroll.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.scroll.LargeChange = 31;
            this.scroll.Location = new System.Drawing.Point(706, 0);
            this.scroll.Maximum = 30;
            this.scroll.Name = "scroll";
            this.scroll.Size = new System.Drawing.Size(15, 434);
            this.scroll.SmallChange = 30;
            this.scroll.TabIndex = 0;
            this.scroll.Scroll += new System.Windows.Forms.ScrollEventHandler(this.scroll_Scroll);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(17, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(47, 12);
            this.label1.TabIndex = 1;
            this.label1.Text = "任务数:";
            // 
            // lTaskCount
            // 
            this.lTaskCount.AutoSize = true;
            this.lTaskCount.Location = new System.Drawing.Point(70, 9);
            this.lTaskCount.Name = "lTaskCount";
            this.lTaskCount.Size = new System.Drawing.Size(11, 12);
            this.lTaskCount.TabIndex = 1;
            this.lTaskCount.Text = "0";
            // 
            // pFinishRate
            // 
            this.pFinishRate.Location = new System.Drawing.Point(103, 9);
            this.pFinishRate.Name = "pFinishRate";
            this.pFinishRate.Size = new System.Drawing.Size(176, 12);
            this.pFinishRate.TabIndex = 2;
            // 
            // lDownloadSize
            // 
            this.lDownloadSize.AutoSize = true;
            this.lDownloadSize.Location = new System.Drawing.Point(285, 9);
            this.lDownloadSize.Name = "lDownloadSize";
            this.lDownloadSize.Size = new System.Drawing.Size(35, 12);
            this.lDownloadSize.TabIndex = 3;
            this.lDownloadSize.Text = "0B/0B";
            // 
            // lSpeed
            // 
            this.lSpeed.AutoSize = true;
            this.lSpeed.Location = new System.Drawing.Point(392, 9);
            this.lSpeed.Name = "lSpeed";
            this.lSpeed.Size = new System.Drawing.Size(29, 12);
            this.lSpeed.TabIndex = 3;
            this.lSpeed.Text = "0B/s";
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.bCancel);
            this.panel1.Controls.Add(this.bPause);
            this.panel1.Controls.Add(this.bStart);
            this.panel1.Controls.Add(this.lSpeed);
            this.panel1.Controls.Add(this.lDownloadSize);
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(703, 28);
            this.panel1.TabIndex = 4;
            // 
            // bStart
            // 
            this.bStart.Location = new System.Drawing.Point(496, 2);
            this.bStart.Name = "bStart";
            this.bStart.Size = new System.Drawing.Size(64, 23);
            this.bStart.TabIndex = 4;
            this.bStart.Text = "全部开始";
            this.bStart.UseVisualStyleBackColor = true;
            this.bStart.Click += new System.EventHandler(this.bStart_Click);
            // 
            // bPause
            // 
            this.bPause.Location = new System.Drawing.Point(566, 2);
            this.bPause.Name = "bPause";
            this.bPause.Size = new System.Drawing.Size(64, 23);
            this.bPause.TabIndex = 4;
            this.bPause.Text = "全部暂停";
            this.bPause.UseVisualStyleBackColor = true;
            this.bPause.Click += new System.EventHandler(this.bPause_Click);
            // 
            // bCancel
            // 
            this.bCancel.Location = new System.Drawing.Point(636, 2);
            this.bCancel.Name = "bCancel";
            this.bCancel.Size = new System.Drawing.Size(64, 23);
            this.bCancel.TabIndex = 4;
            this.bCancel.Text = "全部取消";
            this.bCancel.UseVisualStyleBackColor = true;
            this.bCancel.Click += new System.EventHandler(this.bCancel_Click);
            // 
            // UploadTransferList
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.Controls.Add(this.pFinishRate);
            this.Controls.Add(this.lTaskCount);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.scroll);
            this.Controls.Add(this.panel1);
            this.Name = "UploadTransferList";
            this.Size = new System.Drawing.Size(721, 434);
            this.Load += new System.EventHandler(this.UploadTransferList_Load);
            this.Resize += new System.EventHandler(this.UploadTransferList_Resize);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.VScrollBar scroll;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label lTaskCount;
        private System.Windows.Forms.ProgressBar pFinishRate;
        private System.Windows.Forms.Label lDownloadSize;
        private System.Windows.Forms.Label lSpeed;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button bCancel;
        private System.Windows.Forms.Button bPause;
        private System.Windows.Forms.Button bStart;
    }
}
