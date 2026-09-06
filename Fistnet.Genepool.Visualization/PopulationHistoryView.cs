using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace Fistnet.Genepool.Visualization
{
    /// <summary>A passive view. The UI invalidates it after adding or resetting observations.</summary>
    public sealed class PopulationHistoryView : System.Windows.Forms.Control
    {
        private PopulationHistory history;

        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public PopulationHistory History
        {
            get { return history; }
            set { history = value; Invalidate(); }
        }

        public PopulationHistoryView()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(25, 29, 35);
            ForeColor = Color.FromArgb(227, 233, 240);
            Size = new Size(480, 180);
            TabStop = false;
            AccessibleName = "Population history";
            AccessibleDescription = "Population after completed seasons, retaining the latest 256 samples.";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            PopulationHistory observedHistory = history;
            PopulationSample[] samples = observedHistory == null ? Array.Empty<PopulationSample>() : observedHistory.Snapshot();
            Graphics graphics = e.Graphics;
            GraphicsState saved = graphics.Save();
            try
            {
                graphics.Clear(BackColor);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (Brush text = new SolidBrush(ForeColor))
                using (Brush muted = new SolidBrush(Color.FromArgb(157, 171, 188)))
                using (Brush accent = new SolidBrush(Color.FromArgb(80, 211, 180)))
                using (Pen grid = new Pen(Color.FromArgb(60, 69, 82)))
                using (Pen curve = new Pen(Color.FromArgb(80, 211, 180), 2f))
                using (Font titleFont = new Font(Font, FontStyle.Bold))
                using (StringFormat right = new StringFormat { Alignment = StringAlignment.Far })
                using (StringFormat center = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                })
                {
                    graphics.DrawString("Population history", titleFont, text, 12, 8);
                    if (ClientSize.Width < 180 || ClientSize.Height < 120)
                        return;

                    if (samples.Length > 0 && ClientSize.Width >= 350)
                    {
                        string latest = samples[samples.Length - 1].Population.ToString("N0", CultureInfo.CurrentCulture) + " organisms";
                        graphics.DrawString(latest, Font, accent,
                            new RectangleF(ClientSize.Width / 2f, 9, ClientSize.Width / 2f - 12, 20), right);
                    }

                    graphics.DrawString("Population", Font, muted, 12, 31);
                    RectangleF plot = new RectangleF(54, 52, ClientSize.Width - 70, ClientSize.Height - 94);
                    double maximum = 1;
                    foreach (PopulationSample sample in samples)
                        maximum = Math.Max(maximum, sample.Population);
                    maximum = Math.Ceiling(maximum * 1.1);

                    for (int i = 0; i <= 2; i++)
                    {
                        float y = plot.Bottom - plot.Height * i / 2f;
                        graphics.DrawLine(grid, plot.Left, y, plot.Right, y);
                        string value = Math.Round(maximum * i / 2).ToString("0", CultureInfo.CurrentCulture);
                        graphics.DrawString(value, Font, muted,
                            new RectangleF(0, y - Font.Height / 2f, plot.Left - 7, Font.Height + 2), right);
                    }

                    graphics.DrawString("Seasons", Font, muted,
                        new RectangleF(plot.Left, ClientSize.Height - 22, plot.Width, 18), center);

                    if (samples.Length == 0)
                    {
                        graphics.DrawString("No completed seasons yet", Font, muted, plot, center);
                        return;
                    }

                    int firstSeason = samples[0].Season;
                    int lastSeason = samples[samples.Length - 1].Season;
                    double seasonSpan = Math.Max(1, (double)lastSeason - firstSeason);
                    PointF[] points = new PointF[samples.Length];
                    for (int i = 0; i < samples.Length; i++)
                    {
                        float x = samples.Length == 1 ? plot.Left + plot.Width / 2f :
                            plot.Left + (float)(((double)samples[i].Season - firstSeason) / seasonSpan * plot.Width);
                        float y = plot.Bottom - (float)(samples[i].Population / maximum * plot.Height);
                        points[i] = new PointF(x, y);
                    }

                    if (points.Length > 1)
                        graphics.DrawLines(curve, points);
                    PointF last = points[points.Length - 1];
                    graphics.FillEllipse(accent, last.X - 3, last.Y - 3, 6, 6);

                    if (samples.Length == 1)
                    {
                        graphics.DrawString(firstSeason.ToString(CultureInfo.CurrentCulture), Font, muted,
                            new RectangleF(plot.Left, plot.Bottom + 4, plot.Width, 18), center);
                    }
                    else
                    {
                        graphics.DrawString(firstSeason.ToString(CultureInfo.CurrentCulture), Font, muted, plot.Left, plot.Bottom + 4);
                        graphics.DrawString(lastSeason.ToString(CultureInfo.CurrentCulture), Font, muted,
                            new RectangleF(plot.Left, plot.Bottom + 4, plot.Width, 18), right);
                    }
                }
            }
            finally
            {
                graphics.Restore(saved);
            }
        }
    }
}
