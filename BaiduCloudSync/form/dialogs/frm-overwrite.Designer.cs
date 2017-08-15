namespace BaiduCloudSync
{
    partial class frmOverwrite
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmOverwrite));
            this.lText = new System.Windows.Forms.Label();
            this.bYes = new System.Windows.Forms.Button();
            this.bNo = new System.Windows.Forms.Button();
            this.bCancel = new System.Windows.Forms.Button();
            this.cAlways = new System.Windows.Forms.CheckBox();
            this.bNewFile = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lText
            // 
            this.lText.AutoSize = true;
            this.lText.Location = new System.Drawing.Point(11, 12);
            this.lText.Name = "lText";
            this.lText.Size = new System.Drawing.Size(161, 12);
            this.lText.TabIndex = 0;
            this.lText.Text = "文件已存在，是否覆盖原文件";
            // 
            // bYes
            // 
            this.bYes.Location = new System.Drawing.Point(12, 38);
            this.bYes.Name = "bYes";
            this.bYes.Size = new System.Drawing.Size(109, 34);
            this.bYes.TabIndex = 1;
            this.bYes.Text = "是";
            this.bYes.UseVisualStyleBackColor = true;
            this.bYes.Click += new System.EventHandler(this.bYes_Click);
            // 
            // bNo
            // 
            this.bNo.Location = new System.Drawing.Point(13, 78);
            this.bNo.Name = "bNo";
            this.bNo.Size = new System.Drawing.Size(108, 34);
            this.bNo.TabIndex = 1;
            this.bNo.Text = "否\r\n(跳过该文件)\r\n";
            this.bNo.UseVisualStyleBackColor = true;
            this.bNo.Click += new System.EventHandler(this.bNo_Click);
            // 
            // bCancel
            // 
            this.bCancel.Location = new System.Drawing.Point(127, 78);
            this.bCancel.Name = "bCancel";
            this.bCancel.Size = new System.Drawing.Size(109, 34);
            this.bCancel.TabIndex = 1;
            this.bCancel.Text = "取消";
            this.bCancel.UseVisualStyleBackColor = true;
            this.bCancel.Click += new System.EventHandler(this.bCancel_Click);
            // 
            // cAlways
            // 
            this.cAlways.AutoSize = true;
            this.cAlways.Location = new System.Drawing.Point(12, 122);
            this.cAlways.Name = "cAlways";
            this.cAlways.Size = new System.Drawing.Size(132, 16);
            this.cAlways.TabIndex = 2;
            this.cAlways.Text = "不再询问后续的文件";
            this.cAlways.UseVisualStyleBackColor = true;
            this.cAlways.CheckedChanged += new System.EventHandler(this.cAlways_CheckedChanged);
            // 
            // bNewFile
            // 
            this.bNewFile.Location = new System.Drawing.Point(127, 38);
            this.bNewFile.Name = "bNewFile";
            this.bNewFile.Size = new System.Drawing.Size(109, 34);
            this.bNewFile.TabIndex = 1;
            this.bNewFile.Text = "否\r\n(保存为新文件)";
            this.bNewFile.UseVisualStyleBackColor = true;
            this.bNewFile.Click += new System.EventHandler(this.bNewFile_Click);
            // 
            // frmOverwrite
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(250, 145);
            this.Controls.Add(this.cAlways);
            this.Controls.Add(this.bNewFile);
            this.Controls.Add(this.bCancel);
            this.Controls.Add(this.bNo);
            this.Controls.Add(this.bYes);
            this.Controls.Add(this.lText);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "frmOverwrite";
            this.Text = "frmOverwrite";
            this.Load += new System.EventHandler(this.frmOverwrite_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lText;
        private System.Windows.Forms.Button bYes;
        private System.Windows.Forms.Button bNo;
        private System.Windows.Forms.Button bCancel;
        private System.Windows.Forms.CheckBox cAlways;
        private System.Windows.Forms.Button bNewFile;
    }
}