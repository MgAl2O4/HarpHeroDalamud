
namespace HarpHeroConverter
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.buttonLoad = new System.Windows.Forms.Button();
            this.comboBox1 = new System.Windows.Forms.ComboBox();
            this.buttonPlay = new System.Windows.Forms.Button();
            this.panelNotes = new System.Windows.Forms.Panel();
            this.numericTargetBPM = new System.Windows.Forms.NumericUpDown();
            this.label1 = new System.Windows.Forms.Label();
            this.labelOrgBPM = new System.Windows.Forms.Label();
            this.panelTransformHint = new System.Windows.Forms.Panel();
            this.labelTransformHint = new System.Windows.Forms.Label();
            this.hScrollBar1 = new System.Windows.Forms.HScrollBar();
            this.checkPlayOrgTrack = new System.Windows.Forms.CheckBox();
            this.panelBindings = new System.Windows.Forms.Panel();
            this.buttonSave = new System.Windows.Forms.Button();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.timerUpdate = new System.Windows.Forms.Timer(this.components);
            this.labelTrackPos = new System.Windows.Forms.Label();
            this.textBoxError = new System.Windows.Forms.TextBox();
            this.labelUpdateNotify = new System.Windows.Forms.Label();
            this.saveFileDialog1 = new System.Windows.Forms.SaveFileDialog();
            ((System.ComponentModel.ISupportInitialize)(this.numericTargetBPM)).BeginInit();
            this.panelTransformHint.SuspendLayout();
            this.SuspendLayout();
            // 
            // buttonLoad
            // 
            this.buttonLoad.Location = new System.Drawing.Point(12, 12);
            this.buttonLoad.Name = "buttonLoad";
            this.buttonLoad.Size = new System.Drawing.Size(75, 23);
            this.buttonLoad.TabIndex = 0;
            this.buttonLoad.Text = "Load";
            this.buttonLoad.UseVisualStyleBackColor = true;
            this.buttonLoad.Click += new System.EventHandler(this.buttonLoad_Click);
            // 
            // comboBox1
            // 
            this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Location = new System.Drawing.Point(93, 12);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new System.Drawing.Size(312, 23);
            this.comboBox1.TabIndex = 1;
            this.comboBox1.SelectedIndexChanged += new System.EventHandler(this.comboBox1_SelectedIndexChanged);
            // 
            // buttonPlay
            // 
            this.buttonPlay.Location = new System.Drawing.Point(12, 41);
            this.buttonPlay.Name = "buttonPlay";
            this.buttonPlay.Size = new System.Drawing.Size(75, 23);
            this.buttonPlay.TabIndex = 2;
            this.buttonPlay.Text = "Play";
            this.buttonPlay.UseVisualStyleBackColor = true;
            this.buttonPlay.Click += new System.EventHandler(this.buttonPlay_Click);
            // 
            // panelNotes
            // 
            this.panelNotes.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelNotes.BackColor = System.Drawing.SystemColors.Control;
            this.panelNotes.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelNotes.Location = new System.Drawing.Point(12, 109);
            this.panelNotes.Name = "panelNotes";
            this.panelNotes.Size = new System.Drawing.Size(1060, 356);
            this.panelNotes.TabIndex = 4;
            this.panelNotes.Visible = false;
            // 
            // numericTargetBPM
            // 
            this.numericTargetBPM.Location = new System.Drawing.Point(93, 41);
            this.numericTargetBPM.Maximum = new decimal(new int[] {
            200,
            0,
            0,
            0});
            this.numericTargetBPM.Minimum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.numericTargetBPM.Name = "numericTargetBPM";
            this.numericTargetBPM.Size = new System.Drawing.Size(65, 23);
            this.numericTargetBPM.TabIndex = 5;
            this.numericTargetBPM.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.numericTargetBPM.ValueChanged += new System.EventHandler(this.numericUpDown1_ValueChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(164, 45);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(32, 15);
            this.label1.TabIndex = 6;
            this.label1.Text = "BPM";
            // 
            // labelOrgBPM
            // 
            this.labelOrgBPM.AutoSize = true;
            this.labelOrgBPM.Location = new System.Drawing.Point(202, 45);
            this.labelOrgBPM.Name = "labelOrgBPM";
            this.labelOrgBPM.Size = new System.Drawing.Size(67, 15);
            this.labelOrgBPM.TabIndex = 7;
            this.labelOrgBPM.Text = "(Source: --)";
            // 
            // panelTransformHint
            // 
            this.panelTransformHint.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelTransformHint.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelTransformHint.Controls.Add(this.labelTransformHint);
            this.panelTransformHint.Location = new System.Drawing.Point(12, 70);
            this.panelTransformHint.Name = "panelTransformHint";
            this.panelTransformHint.Size = new System.Drawing.Size(1060, 33);
            this.panelTransformHint.TabIndex = 8;
            // 
            // labelTransformHint
            // 
            this.labelTransformHint.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelTransformHint.Location = new System.Drawing.Point(0, 0);
            this.labelTransformHint.Name = "labelTransformHint";
            this.labelTransformHint.Size = new System.Drawing.Size(1058, 31);
            this.labelTransformHint.TabIndex = 0;
            this.labelTransformHint.Text = "Mouse over notes for details";
            this.labelTransformHint.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // hScrollBar1
            // 
            this.hScrollBar1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.hScrollBar1.Location = new System.Drawing.Point(9, 468);
            this.hScrollBar1.Name = "hScrollBar1";
            this.hScrollBar1.Size = new System.Drawing.Size(1066, 13);
            this.hScrollBar1.TabIndex = 9;
            this.hScrollBar1.Scroll += new System.Windows.Forms.ScrollEventHandler(this.hScrollBar1_Scroll);
            // 
            // checkPlayOrgTrack
            // 
            this.checkPlayOrgTrack.AutoSize = true;
            this.checkPlayOrgTrack.Location = new System.Drawing.Point(411, 14);
            this.checkPlayOrgTrack.Name = "checkPlayOrgTrack";
            this.checkPlayOrgTrack.Size = new System.Drawing.Size(142, 19);
            this.checkPlayOrgTrack.TabIndex = 10;
            this.checkPlayOrgTrack.Text = "Play unmodified track";
            this.checkPlayOrgTrack.UseVisualStyleBackColor = true;
            // 
            // panelBindings
            // 
            this.panelBindings.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelBindings.BackColor = System.Drawing.SystemColors.Control;
            this.panelBindings.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelBindings.Location = new System.Drawing.Point(13, 484);
            this.panelBindings.Name = "panelBindings";
            this.panelBindings.Size = new System.Drawing.Size(1059, 65);
            this.panelBindings.TabIndex = 11;
            this.panelBindings.Visible = false;
            // 
            // buttonSave
            // 
            this.buttonSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonSave.Location = new System.Drawing.Point(909, 12);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(166, 23);
            this.buttonSave.TabIndex = 12;
            this.buttonSave.Text = "Save changes";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.DefaultExt = "*.mid";
            // 
            // timerUpdate
            // 
            this.timerUpdate.Interval = 10;
            this.timerUpdate.Tick += new System.EventHandler(this.timerUpdate_Tick);
            // 
            // labelTrackPos
            // 
            this.labelTrackPos.AutoSize = true;
            this.labelTrackPos.Location = new System.Drawing.Point(430, 43);
            this.labelTrackPos.Name = "labelTrackPos";
            this.labelTrackPos.Size = new System.Drawing.Size(0, 15);
            this.labelTrackPos.TabIndex = 13;
            // 
            // textBoxError
            // 
            this.textBoxError.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxError.Location = new System.Drawing.Point(12, 70);
            this.textBoxError.Multiline = true;
            this.textBoxError.Name = "textBoxError";
            this.textBoxError.ReadOnly = true;
            this.textBoxError.Size = new System.Drawing.Size(1063, 479);
            this.textBoxError.TabIndex = 0;
            this.textBoxError.Visible = false;
            // 
            // labelUpdateNotify
            // 
            this.labelUpdateNotify.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.labelUpdateNotify.BackColor = System.Drawing.Color.Lime;
            this.labelUpdateNotify.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.labelUpdateNotify.Location = new System.Drawing.Point(12, 468);
            this.labelUpdateNotify.Name = "labelUpdateNotify";
            this.labelUpdateNotify.Size = new System.Drawing.Size(1063, 84);
            this.labelUpdateNotify.TabIndex = 14;
            this.labelUpdateNotify.Text = "New version found! Please restart program to finish update. Click on this message" +
    " to hide it.";
            this.labelUpdateNotify.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.labelUpdateNotify.Visible = false;
            this.labelUpdateNotify.Click += new System.EventHandler(this.labelUpdateNotify_Click);
            // 
            // saveFileDialog1
            // 
            this.saveFileDialog1.DefaultExt = "*.mid";
            this.saveFileDialog1.Filter = "Midi files|*.mid";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1084, 561);
            this.Controls.Add(this.labelTrackPos);
            this.Controls.Add(this.buttonSave);
            this.Controls.Add(this.panelBindings);
            this.Controls.Add(this.checkPlayOrgTrack);
            this.Controls.Add(this.hScrollBar1);
            this.Controls.Add(this.panelTransformHint);
            this.Controls.Add(this.labelOrgBPM);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.numericTargetBPM);
            this.Controls.Add(this.panelNotes);
            this.Controls.Add(this.buttonPlay);
            this.Controls.Add(this.comboBox1);
            this.Controls.Add(this.buttonLoad);
            this.Controls.Add(this.textBoxError);
            this.Controls.Add(this.labelUpdateNotify);
            this.DoubleBuffered = true;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MinimumSize = new System.Drawing.Size(800, 300);
            this.Name = "MainForm";
            this.Text = "HarpHero: converter";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.MainForm_Paint);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.MainForm_MouseMove);
            ((System.ComponentModel.ISupportInitialize)(this.numericTargetBPM)).EndInit();
            this.panelTransformHint.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button buttonLoad;
        private System.Windows.Forms.ComboBox comboBox1;
        private System.Windows.Forms.Button buttonPlay;
        private System.Windows.Forms.Panel panelNotes;
        private System.Windows.Forms.NumericUpDown numericTargetBPM;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label labelOrgBPM;
        private System.Windows.Forms.Panel panelTransformHint;
        private System.Windows.Forms.Label labelTransformHint;
        private System.Windows.Forms.HScrollBar hScrollBar1;
        private System.Windows.Forms.CheckBox checkPlayOrgTrack;
        private System.Windows.Forms.Panel panelBindings;
        private System.Windows.Forms.Button buttonSave;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.Timer timerUpdate;
        private System.Windows.Forms.Label labelTrackPos;
        private System.Windows.Forms.TextBox textBoxError;
        private System.Windows.Forms.Label labelUpdateNotify;
        private System.Windows.Forms.SaveFileDialog saveFileDialog1;
    }
}

