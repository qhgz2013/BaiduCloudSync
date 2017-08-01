namespace BaiduCloudSync
{
    partial class frmCreateShare
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmCreateShare));
            this.btnConfirm = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.rbEver = new System.Windows.Forms.RadioButton();
            this.rb7d = new System.Windows.Forms.RadioButton();
            this.rb1d = new System.Windows.Forms.RadioButton();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.rbPublic = new System.Windows.Forms.RadioButton();
            this.rbPrivate = new System.Windows.Forms.RadioButton();
            this.label1 = new System.Windows.Forms.Label();
            this.tPassword = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.tLongUrl = new System.Windows.Forms.TextBox();
            this.tShortUrl = new System.Windows.Forms.TextBox();
            this.linkLabel1 = new System.Windows.Forms.LinkLabel();
            this.linkLabel2 = new System.Windows.Forms.LinkLabel();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnConfirm
            // 
            this.btnConfirm.Location = new System.Drawing.Point(161, 119);
            this.btnConfirm.Name = "btnConfirm";
            this.btnConfirm.Size = new System.Drawing.Size(95, 63);
            this.btnConfirm.TabIndex = 1;
            this.btnConfirm.Text = "确定";
            this.btnConfirm.UseVisualStyleBackColor = true;
            this.btnConfirm.Click += new System.EventHandler(this.btnConfirm_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(262, 119);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(95, 63);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "取消";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.rbEver);
            this.groupBox1.Controls.Add(this.rb7d);
            this.groupBox1.Controls.Add(this.rb1d);
            this.groupBox1.Location = new System.Drawing.Point(14, 66);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(343, 47);
            this.groupBox1.TabIndex = 2;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "有效时间";
            // 
            // rbEver
            // 
            this.rbEver.AutoSize = true;
            this.rbEver.Checked = true;
            this.rbEver.Location = new System.Drawing.Point(232, 20);
            this.rbEver.Name = "rbEver";
            this.rbEver.Size = new System.Drawing.Size(47, 16);
            this.rbEver.TabIndex = 0;
            this.rbEver.TabStop = true;
            this.rbEver.Text = "永久";
            this.rbEver.UseVisualStyleBackColor = true;
            // 
            // rb7d
            // 
            this.rb7d.AutoSize = true;
            this.rb7d.Location = new System.Drawing.Point(129, 20);
            this.rb7d.Name = "rb7d";
            this.rb7d.Size = new System.Drawing.Size(47, 16);
            this.rb7d.TabIndex = 0;
            this.rb7d.Text = "七天";
            this.rb7d.UseVisualStyleBackColor = true;
            // 
            // rb1d
            // 
            this.rb1d.AutoSize = true;
            this.rb1d.Location = new System.Drawing.Point(6, 20);
            this.rb1d.Name = "rb1d";
            this.rb1d.Size = new System.Drawing.Size(47, 16);
            this.rb1d.TabIndex = 0;
            this.rb1d.Text = "一天";
            this.rb1d.UseVisualStyleBackColor = true;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.rbPublic);
            this.groupBox2.Controls.Add(this.rbPrivate);
            this.groupBox2.Location = new System.Drawing.Point(12, 12);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(345, 49);
            this.groupBox2.TabIndex = 3;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "分享类型";
            // 
            // rbPublic
            // 
            this.rbPublic.AutoSize = true;
            this.rbPublic.Location = new System.Drawing.Point(163, 20);
            this.rbPublic.Name = "rbPublic";
            this.rbPublic.Size = new System.Drawing.Size(95, 16);
            this.rbPublic.TabIndex = 1;
            this.rbPublic.Text = "创建公开分享";
            this.rbPublic.UseVisualStyleBackColor = true;
            // 
            // rbPrivate
            // 
            this.rbPrivate.AutoSize = true;
            this.rbPrivate.Checked = true;
            this.rbPrivate.Location = new System.Drawing.Point(6, 20);
            this.rbPrivate.Name = "rbPrivate";
            this.rbPrivate.Size = new System.Drawing.Size(95, 16);
            this.rbPrivate.TabIndex = 2;
            this.rbPrivate.TabStop = true;
            this.rbPrivate.Text = "创建加密分享";
            this.rbPrivate.UseVisualStyleBackColor = true;
            this.rbPrivate.CheckedChanged += new System.EventHandler(this.rbPrivate_CheckedChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 130);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(29, 12);
            this.label1.TabIndex = 4;
            this.label1.Text = "密码";
            // 
            // tPassword
            // 
            this.tPassword.Location = new System.Drawing.Point(57, 127);
            this.tPassword.Name = "tPassword";
            this.tPassword.Size = new System.Drawing.Size(79, 21);
            this.tPassword.TabIndex = 5;
            this.tPassword.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.tPassword_KeyPress);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Enabled = false;
            this.label2.Location = new System.Drawing.Point(12, 155);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(143, 12);
            this.label2.TabIndex = 4;
            this.label2.Text = "(4位数字或字母，可更改)";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(12, 193);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(65, 12);
            this.label3.TabIndex = 4;
            this.label3.Text = "分享链接：";
            this.label3.Visible = false;
            // 
            // tLongUrl
            // 
            this.tLongUrl.Location = new System.Drawing.Point(12, 208);
            this.tLongUrl.Name = "tLongUrl";
            this.tLongUrl.Size = new System.Drawing.Size(310, 21);
            this.tLongUrl.TabIndex = 5;
            this.tLongUrl.Visible = false;
            this.tLongUrl.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.tPassword_KeyPress);
            // 
            // tShortUrl
            // 
            this.tShortUrl.Location = new System.Drawing.Point(12, 235);
            this.tShortUrl.Name = "tShortUrl";
            this.tShortUrl.Size = new System.Drawing.Size(310, 21);
            this.tShortUrl.TabIndex = 5;
            this.tShortUrl.Visible = false;
            this.tShortUrl.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.tPassword_KeyPress);
            // 
            // linkLabel1
            // 
            this.linkLabel1.AutoSize = true;
            this.linkLabel1.Location = new System.Drawing.Point(328, 211);
            this.linkLabel1.Name = "linkLabel1";
            this.linkLabel1.Size = new System.Drawing.Size(29, 12);
            this.linkLabel1.TabIndex = 6;
            this.linkLabel1.TabStop = true;
            this.linkLabel1.Text = "复制";
            this.linkLabel1.Visible = false;
            this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked);
            // 
            // linkLabel2
            // 
            this.linkLabel2.AutoSize = true;
            this.linkLabel2.Location = new System.Drawing.Point(328, 238);
            this.linkLabel2.Name = "linkLabel2";
            this.linkLabel2.Size = new System.Drawing.Size(29, 12);
            this.linkLabel2.TabIndex = 6;
            this.linkLabel2.TabStop = true;
            this.linkLabel2.Text = "复制";
            this.linkLabel2.Visible = false;
            this.linkLabel2.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel2_LinkClicked);
            // 
            // frmCreateShare
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(377, 279);
            this.Controls.Add(this.linkLabel2);
            this.Controls.Add(this.linkLabel1);
            this.Controls.Add(this.tShortUrl);
            this.Controls.Add(this.tLongUrl);
            this.Controls.Add(this.tPassword);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnConfirm);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "frmCreateShare";
            this.Text = "创建分享";
            this.Load += new System.EventHandler(this.frmCreateShare_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button btnConfirm;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.RadioButton rbEver;
        private System.Windows.Forms.RadioButton rb7d;
        private System.Windows.Forms.RadioButton rb1d;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.RadioButton rbPublic;
        private System.Windows.Forms.RadioButton rbPrivate;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tPassword;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox tLongUrl;
        private System.Windows.Forms.TextBox tShortUrl;
        private System.Windows.Forms.LinkLabel linkLabel1;
        private System.Windows.Forms.LinkLabel linkLabel2;
    }
}