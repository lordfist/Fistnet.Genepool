namespace Fistnet.Genepool.App
{
    partial class MainForm
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
            this.components = new System.ComponentModel.Container();
            this.StartButton = new System.Windows.Forms.Button();
            this.BoardVisualizer = new System.Windows.Forms.PictureBox();
            this.BoardRefreshTimer = new System.Windows.Forms.Timer(this.components);
            this.BoardWorker = new System.ComponentModel.BackgroundWorker();
            this.StopButton = new System.Windows.Forms.Button();
            this.StatisticsLabel = new System.Windows.Forms.Label();
            this.BoardItemLabel = new System.Windows.Forms.Label();
            this.TopRatedLabel = new System.Windows.Forms.Label();
            this.ButtonComplexStats = new System.Windows.Forms.Button();
            this.AgeRunCheck = new System.Windows.Forms.CheckBox();
            this.ResetButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.BoardVisualizer)).BeginInit();
            this.SuspendLayout();
            // 
            // StartButton
            // 
            this.StartButton.Location = new System.Drawing.Point(729, 589);
            this.StartButton.Name = "StartButton";
            this.StartButton.Size = new System.Drawing.Size(75, 23);
            this.StartButton.TabIndex = 0;
            this.StartButton.Text = "Start";
            this.StartButton.UseVisualStyleBackColor = true;
            this.StartButton.Click += new System.EventHandler(this.StartButton_Click);
            // 
            // BoardVisualizer
            // 
            this.BoardVisualizer.BackColor = System.Drawing.Color.Black;
            this.BoardVisualizer.Cursor = System.Windows.Forms.Cursors.Hand;
            this.BoardVisualizer.Location = new System.Drawing.Point(12, 12);
            this.BoardVisualizer.Name = "BoardVisualizer";
            this.BoardVisualizer.Size = new System.Drawing.Size(600, 600);
            this.BoardVisualizer.TabIndex = 1;
            this.BoardVisualizer.TabStop = false;
            this.BoardVisualizer.MouseClick += new System.Windows.Forms.MouseEventHandler(this.BoardVisualizer_MouseClick);
            // 
            // BoardRefreshTimer
            // 
            this.BoardRefreshTimer.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // BoardWorker
            // 
            this.BoardWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.BoardWorker_DoWork);
            this.BoardWorker.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.BoardWorker_RunWorkerCompleted);
            // 
            // StopButton
            // 
            this.StopButton.Location = new System.Drawing.Point(810, 589);
            this.StopButton.Name = "StopButton";
            this.StopButton.Size = new System.Drawing.Size(75, 23);
            this.StopButton.TabIndex = 2;
            this.StopButton.Text = "Pause";
            this.StopButton.UseVisualStyleBackColor = true;
            this.StopButton.Click += new System.EventHandler(this.StopButton_Click);
            // 
            // StatisticsLabel
            // 
            this.StatisticsLabel.AutoSize = true;
            this.StatisticsLabel.Location = new System.Drawing.Point(619, 13);
            this.StatisticsLabel.Name = "StatisticsLabel";
            this.StatisticsLabel.Size = new System.Drawing.Size(79, 13);
            this.StatisticsLabel.TabIndex = 3;
            this.StatisticsLabel.Text = "Statistical data:";
            // 
            // BoardItemLabel
            // 
            this.BoardItemLabel.AutoSize = true;
            this.BoardItemLabel.Location = new System.Drawing.Point(619, 284);
            this.BoardItemLabel.Name = "BoardItemLabel";
            this.BoardItemLabel.Size = new System.Drawing.Size(60, 13);
            this.BoardItemLabel.TabIndex = 4;
            this.BoardItemLabel.Text = "Board item:";
            // 
            // TopRatedLabel
            // 
            this.TopRatedLabel.AutoSize = true;
            this.TopRatedLabel.Location = new System.Drawing.Point(846, 13);
            this.TopRatedLabel.Name = "TopRatedLabel";
            this.TopRatedLabel.Size = new System.Drawing.Size(82, 13);
            this.TopRatedLabel.TabIndex = 5;
            this.TopRatedLabel.Text = "Top rated DNA:";
            // 
            // ButtonComplexStats
            // 
            this.ButtonComplexStats.Location = new System.Drawing.Point(1043, 8);
            this.ButtonComplexStats.Name = "ButtonComplexStats";
            this.ButtonComplexStats.Size = new System.Drawing.Size(75, 23);
            this.ButtonComplexStats.TabIndex = 6;
            this.ButtonComplexStats.Text = "REFRESH";
            this.ButtonComplexStats.UseVisualStyleBackColor = true;
            this.ButtonComplexStats.Click += new System.EventHandler(this.ButtonComplexStats_Click);
            // 
            // AgeRunCheck
            // 
            this.AgeRunCheck.AutoSize = true;
            this.AgeRunCheck.Location = new System.Drawing.Point(622, 593);
            this.AgeRunCheck.Name = "AgeRunCheck";
            this.AgeRunCheck.Size = new System.Drawing.Size(96, 17);
            this.AgeRunCheck.TabIndex = 7;
            this.AgeRunCheck.Text = "Run entire age";
            this.AgeRunCheck.UseVisualStyleBackColor = true;
            // 
            // ResetButton
            // 
            this.ResetButton.Location = new System.Drawing.Point(1043, 589);
            this.ResetButton.Name = "ResetButton";
            this.ResetButton.Size = new System.Drawing.Size(75, 23);
            this.ResetButton.TabIndex = 8;
            this.ResetButton.Text = "Reset";
            this.ResetButton.UseVisualStyleBackColor = true;
            this.ResetButton.Click += new System.EventHandler(this.button1_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1123, 624);
            this.Controls.Add(this.ResetButton);
            this.Controls.Add(this.AgeRunCheck);
            this.Controls.Add(this.ButtonComplexStats);
            this.Controls.Add(this.TopRatedLabel);
            this.Controls.Add(this.BoardItemLabel);
            this.Controls.Add(this.StatisticsLabel);
            this.Controls.Add(this.StopButton);
            this.Controls.Add(this.BoardVisualizer);
            this.Controls.Add(this.StartButton);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "MainForm";
            this.Text = "Gene game";
            ((System.ComponentModel.ISupportInitialize)(this.BoardVisualizer)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button StartButton;
        private System.Windows.Forms.PictureBox BoardVisualizer;
        private System.Windows.Forms.Timer BoardRefreshTimer;
        private System.ComponentModel.BackgroundWorker BoardWorker;
        private System.Windows.Forms.Button StopButton;
        private System.Windows.Forms.Label StatisticsLabel;
        private System.Windows.Forms.Label BoardItemLabel;
        private System.Windows.Forms.Label TopRatedLabel;
        private System.Windows.Forms.Button ButtonComplexStats;
        private System.Windows.Forms.CheckBox AgeRunCheck;
        private System.Windows.Forms.Button ResetButton;
    }
}

