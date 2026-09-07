namespace Fistnet.Genepool.App
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components;
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            ClientSize = new System.Drawing.Size(1440, 960);
            MinimumSize = new System.Drawing.Size(1120, 790);
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Genepool · Living patterns";
            Name = "MainForm";
            Font = new System.Drawing.Font("Segoe UI", 9F);
            BackColor = System.Drawing.Color.FromArgb(11, 17, 26);
            ForeColor = System.Drawing.Color.FromArgb(225, 235, 245);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            MaximizeBox = true;
            BuildLayout();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { ReleaseSimulationResources(); components?.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
