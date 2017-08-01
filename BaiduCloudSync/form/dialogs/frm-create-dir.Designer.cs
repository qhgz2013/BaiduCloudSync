namespace BaiduCloudSync
{
    partial class frmCreateDir
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmCreateDir));
            this.label1 = new System.Windows.Forms.Label();
            this.tPath = new System.Windows.Forms.TextBox();
            this.bConfirm = new System.Windows.Forms.Button();
            this.bCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(149, 12);
            this.label1.TabIndex = 0;
            this.label1.Text = "输入新的文件夹/文件名称:";
            // 
            // tPath
            // 
            this.tPath.Location = new System.Drawing.Point(14, 24);
            this.tPath.Multiline = true;
            this.tPath.Name = "tPath";
            this.tPath.ScrollBars = System.Windows.Forms.ScrollBars.Horizontal;
            this.tPath.Size = new System.Drawing.Size(250, 39);
            this.tPath.TabIndex = 1;
            this.tPath.TextChanged += new System.EventHandler(this.tPath_TextChanged);
            this.tPath.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.tPath_KeyPress);
            // 
            // bConfirm
            // 
            this.bConfirm.Location = new System.Drawing.Point(73, 71);
            this.bConfirm.Name = "bConfirm";
            this.bConfirm.Size = new System.Drawing.Size(88, 56);
            this.bConfirm.TabIndex = 2;
            this.bConfirm.Text = "确定";
            this.bConfirm.UseVisualStyleBackColor = true;
            this.bConfirm.Click += new System.EventHandler(this.bConfirm_Click);
            // 
            // bCancel
            // 
            this.bCancel.Location = new System.Drawing.Point(176, 71);
            this.bCancel.Name = "bCancel";
            this.bCancel.Size = new System.Drawing.Size(88, 56);
            this.bCancel.TabIndex = 3;
            this.bCancel.Text = "取消";
            this.bCancel.UseVisualStyleBackColor = true;
            this.bCancel.Click += new System.EventHandler(this.bCancel_Click);
            // 
            // frmCreateDir
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(275, 139);
            this.Controls.Add(this.bCancel);
            this.Controls.Add(this.bConfirm);
            this.Controls.Add(this.tPath);
            this.Controls.Add(this.label1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "frmCreateDir";
            this.Text = "创建/修改";
            this.Load += new System.EventHandler(this.frm_create_dir_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tPath;
        private System.Windows.Forms.Button bConfirm;
        private System.Windows.Forms.Button bCancel;
    }
}