namespace BaiduCloudSync
{
    partial class frm_sync
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
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.tLocalPath = new System.Windows.Forms.TextBox();
            this.tRemotePath = new System.Windows.Forms.TextBox();
            this.bChangeLocalPath = new System.Windows.Forms.Button();
            this.bChangeRemotePath = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.lStatus = new System.Windows.Forms.Label();
            this.button1 = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(53, 12);
            this.label1.TabIndex = 0;
            this.label1.Text = "本地地址";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 31);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(53, 12);
            this.label2.TabIndex = 0;
            this.label2.Text = "网盘地址";
            // 
            // tLocalPath
            // 
            this.tLocalPath.Location = new System.Drawing.Point(71, 6);
            this.tLocalPath.Name = "tLocalPath";
            this.tLocalPath.Size = new System.Drawing.Size(348, 21);
            this.tLocalPath.TabIndex = 1;
            this.tLocalPath.TextChanged += new System.EventHandler(this.tLocalPath_TextChanged);
            // 
            // tRemotePath
            // 
            this.tRemotePath.Location = new System.Drawing.Point(71, 28);
            this.tRemotePath.Name = "tRemotePath";
            this.tRemotePath.Size = new System.Drawing.Size(348, 21);
            this.tRemotePath.TabIndex = 1;
            this.tRemotePath.TextChanged += new System.EventHandler(this.tRemotePath_TextChanged);
            // 
            // bChangeLocalPath
            // 
            this.bChangeLocalPath.Enabled = false;
            this.bChangeLocalPath.Location = new System.Drawing.Point(425, 5);
            this.bChangeLocalPath.Name = "bChangeLocalPath";
            this.bChangeLocalPath.Size = new System.Drawing.Size(58, 22);
            this.bChangeLocalPath.TabIndex = 2;
            this.bChangeLocalPath.Text = "确定";
            this.bChangeLocalPath.UseVisualStyleBackColor = true;
            this.bChangeLocalPath.Click += new System.EventHandler(this.bChangeLocalPath_Click);
            // 
            // bChangeRemotePath
            // 
            this.bChangeRemotePath.Enabled = false;
            this.bChangeRemotePath.Location = new System.Drawing.Point(425, 27);
            this.bChangeRemotePath.Name = "bChangeRemotePath";
            this.bChangeRemotePath.Size = new System.Drawing.Size(58, 22);
            this.bChangeRemotePath.TabIndex = 2;
            this.bChangeRemotePath.Text = "确定";
            this.bChangeRemotePath.UseVisualStyleBackColor = true;
            this.bChangeRemotePath.Click += new System.EventHandler(this.bChangeRemotePath_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(12, 67);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(35, 12);
            this.label3.TabIndex = 3;
            this.label3.Text = "状态:";
            // 
            // lStatus
            // 
            this.lStatus.AutoSize = true;
            this.lStatus.Location = new System.Drawing.Point(69, 67);
            this.lStatus.Name = "lStatus";
            this.lStatus.Size = new System.Drawing.Size(41, 12);
            this.lStatus.TabIndex = 3;
            this.lStatus.Text = "------";
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(14, 106);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(124, 58);
            this.button1.TabIndex = 4;
            this.button1.Text = "button1";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // frm_sync
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(495, 317);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.lStatus);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.bChangeRemotePath);
            this.Controls.Add(this.bChangeLocalPath);
            this.Controls.Add(this.tRemotePath);
            this.Controls.Add(this.tLocalPath);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Margin = new System.Windows.Forms.Padding(1);
            this.Name = "frm_sync";
            this.Text = "frm_sync";
            this.Load += new System.EventHandler(this.frm_sync_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox tLocalPath;
        private System.Windows.Forms.TextBox tRemotePath;
        private System.Windows.Forms.Button bChangeLocalPath;
        private System.Windows.Forms.Button bChangeRemotePath;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label lStatus;
        private System.Windows.Forms.Button button1;
    }
}