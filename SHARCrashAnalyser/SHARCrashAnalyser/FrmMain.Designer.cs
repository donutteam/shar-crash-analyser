namespace SHARCrashAnalyser
{
    partial class FrmMain
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmMain));
            this.GBDump = new System.Windows.Forms.GroupBox();
            this.TxtDumpPath = new System.Windows.Forms.TextBox();
            this.BtnBrowse = new System.Windows.Forms.Button();
            this.GBAnalysis = new System.Windows.Forms.GroupBox();
            this.RTBAnalysis = new System.Windows.Forms.RichTextBox();
            this.GBDump.SuspendLayout();
            this.GBAnalysis.SuspendLayout();
            this.SuspendLayout();
            // 
            // GBDump
            // 
            this.GBDump.Controls.Add(this.TxtDumpPath);
            this.GBDump.Controls.Add(this.BtnBrowse);
            this.GBDump.Dock = System.Windows.Forms.DockStyle.Top;
            this.GBDump.Location = new System.Drawing.Point(3, 3);
            this.GBDump.Name = "GBDump";
            this.GBDump.Size = new System.Drawing.Size(794, 39);
            this.GBDump.TabIndex = 0;
            this.GBDump.TabStop = false;
            this.GBDump.Text = "SHAR Dump Path";
            // 
            // TxtDumpPath
            // 
            this.TxtDumpPath.BackColor = System.Drawing.SystemColors.Window;
            this.TxtDumpPath.Dock = System.Windows.Forms.DockStyle.Fill;
            this.TxtDumpPath.Location = new System.Drawing.Point(3, 16);
            this.TxtDumpPath.Name = "TxtDumpPath";
            this.TxtDumpPath.ReadOnly = true;
            this.TxtDumpPath.Size = new System.Drawing.Size(713, 20);
            this.TxtDumpPath.TabIndex = 1;
            this.TxtDumpPath.Enter += new System.EventHandler(this.TxtDumpPath_Enter);
            // 
            // BtnBrowse
            // 
            this.BtnBrowse.Dock = System.Windows.Forms.DockStyle.Right;
            this.BtnBrowse.Location = new System.Drawing.Point(716, 16);
            this.BtnBrowse.Name = "BtnBrowse";
            this.BtnBrowse.Size = new System.Drawing.Size(75, 20);
            this.BtnBrowse.TabIndex = 0;
            this.BtnBrowse.Text = "Browse";
            this.BtnBrowse.UseVisualStyleBackColor = true;
            this.BtnBrowse.Click += new System.EventHandler(this.BtnBrowse_Click);
            // 
            // GBAnalysis
            // 
            this.GBAnalysis.Controls.Add(this.RTBAnalysis);
            this.GBAnalysis.Dock = System.Windows.Forms.DockStyle.Fill;
            this.GBAnalysis.Location = new System.Drawing.Point(3, 42);
            this.GBAnalysis.Name = "GBAnalysis";
            this.GBAnalysis.Size = new System.Drawing.Size(794, 405);
            this.GBAnalysis.TabIndex = 1;
            this.GBAnalysis.TabStop = false;
            this.GBAnalysis.Text = "Analysis";
            // 
            // RTBAnalysis
            // 
            this.RTBAnalysis.Dock = System.Windows.Forms.DockStyle.Fill;
            this.RTBAnalysis.Font = new System.Drawing.Font("Consolas", 12F);
            this.RTBAnalysis.Location = new System.Drawing.Point(3, 16);
            this.RTBAnalysis.Name = "RTBAnalysis";
            this.RTBAnalysis.Size = new System.Drawing.Size(788, 386);
            this.RTBAnalysis.TabIndex = 0;
            this.RTBAnalysis.Text = "";
            // 
            // FrmMain
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.GBAnalysis);
            this.Controls.Add(this.GBDump);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "FrmMain";
            this.Padding = new System.Windows.Forms.Padding(3);
            this.Text = "SHAR Crash Analyser";
            this.Load += new System.EventHandler(this.FrmMain_Load);
            this.DragDrop += new System.Windows.Forms.DragEventHandler(this.FrmMain_DragDrop);
            this.DragEnter += new System.Windows.Forms.DragEventHandler(this.FrmMain_DragEnter);
            this.GBDump.ResumeLayout(false);
            this.GBDump.PerformLayout();
            this.GBAnalysis.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox GBDump;
        private System.Windows.Forms.TextBox TxtDumpPath;
        private System.Windows.Forms.Button BtnBrowse;
        private System.Windows.Forms.GroupBox GBAnalysis;
        private System.Windows.Forms.RichTextBox RTBAnalysis;
    }
}

