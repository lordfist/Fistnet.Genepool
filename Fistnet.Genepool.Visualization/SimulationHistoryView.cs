using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.Versioning;
using System.Windows.Forms;
using Fistnet.Genepool.Control;

namespace Fistnet.Genepool.Visualization
{
    /// <summary>Bounded completed-season histories, independent of display frequency.</summary>
    [SupportedOSPlatform("windows6.1")]
    public sealed class SimulationHistoryView : System.Windows.Forms.Control
    {
        private ViewFrame frame;
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ViewFrame Frame { get => frame; set { frame = value; Invalidate(); } }
        public SimulationHistoryView()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(19, 28, 40); ForeColor = Color.FromArgb(222, 232, 242);
            AccessibleName = "Recent simulation history";
            AccessibleDescription = "Population, food, births and deaths, and committed actions over 240 sampled completed seasons. Event curves are interval averages per season.";
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics; g.Clear(BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
            if (ClientSize.Width < 400 || ClientSize.Height < 90) return;
            float width = ClientSize.Width / 4f;
            DrawPlot(0, "POPULATION", frame?.Population.ToString("N0") ?? "—", Color.FromArgb(96, 213, 189), h => h.Population, null, null, false);
            DrawPlot(1, "FOOD", "local / carried", Color.FromArgb(132, 207, 147), h => h.LocalFood, h => h.StoredFood, Color.FromArgb(237, 196, 100), false);
            DrawPlot(2, "BIRTHS / DEATHS", "average / season", Color.FromArgb(96, 213, 189), h => h.Births, h => h.Deaths, Color.FromArgb(245, 126, 143), true);
            DrawPlot(3, "PERFORMED ACTIONS", "average / season", Color.FromArgb(136, 170, 249), h => h.ActionCount, null, null, true);

            void DrawPlot(int column, string title, string value, Color primaryColor, Func<ViewHistory, double> primary,
                Func<ViewHistory, double> secondary, Color? secondaryColor, bool difference)
            {
                float left = column * width;
                using var primaryPen = new Pen(primaryColor, 1.8f);
                using var secondaryPen = new Pen(secondaryColor ?? primaryColor, 1.5f);
                using var text = new SolidBrush(ForeColor);
                using var muted = new SolidBrush(Color.FromArgb(142, 161, 184));
                using var titleFont = new Font(Font.FontFamily, Math.Max(8, Font.Size - .5f), FontStyle.Bold);
                using var grid = new Pen(Color.FromArgb(40, 54, 71));
                g.DrawString(title, titleFont, text, left + 14, 8);
                g.DrawString(value, Font, muted, left + 14, 27);
                RectangleF plot = new RectangleF(left + 42, 51, width - 57, ClientSize.Height - 77);
                if (plot.Height < 10) return;
                g.DrawLine(grid, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
                g.DrawLine(grid, plot.Left, plot.Top, plot.Right, plot.Top);
                if (frame == null || frame.History.Count == 0)
                { g.DrawString("Waiting for completed seasons", Font, muted, plot.Left, plot.Top + 5); return; }
                int count = frame.History.Count;
                double maximum = 1;
                for (int i = 0; i < count; i++)
                {
                    maximum = Math.Max(maximum, Value(primary, i));
                    if (secondary != null) maximum = Math.Max(maximum, Value(secondary, i));
                }
                maximum = Math.Ceiling(maximum * 1.05);
                using var right = new StringFormat { Alignment = StringAlignment.Far };
                g.DrawString(Compact(maximum), Font, muted, new RectangleF(left, plot.Top - 5, 36, 20), right);
                g.DrawString("0", Font, muted, new RectangleF(left, plot.Bottom - 12, 36, 20), right);
                int first = frame.History[0].Season, last = frame.History[count - 1].Season;
                g.DrawString(first.ToString(CultureInfo.CurrentCulture), Font, muted, plot.Left, plot.Bottom + 3);
                g.DrawString(last + " seasons", Font, muted, new RectangleF(plot.Left, plot.Bottom + 3, plot.Width, 20), right);
                DrawLine(primary, primaryPen); if (secondary != null) DrawLine(secondary, secondaryPen);
                double Value(Func<ViewHistory, double> source, int i) => difference ? i == 0 ? 0 : Math.Max(0, source(frame.History[i]) - source(frame.History[i - 1])) / Math.Max(1, frame.History[i].Season - frame.History[i - 1].Season) : source(frame.History[i]);
                void DrawLine(Func<ViewHistory, double> source, Pen pen)
                {
                    PointF previous = PointF.Empty;
                    for (int i = 0; i < count; i++)
                    {
                        float x = plot.Left + (count == 1 ? .5f : (frame.History[i].Season - first) / (float)Math.Max(1, last - first)) * plot.Width;
                        float y = plot.Bottom - (float)(Value(source, i) / maximum) * plot.Height;
                        PointF point = new PointF(x, y);
                        if (i != 0) g.DrawLine(pen, previous, point);
                        previous = point;
                    }
                    using var dot = new SolidBrush(pen.Color);
                    g.FillEllipse(dot, previous.X - 2, previous.Y - 2, 4, 4);
                }
            }
        }
        private static string Compact(double value) => value >= 1000000 ? (value / 1000000).ToString("0.#") + "m" : value >= 1000 ? (value / 1000).ToString("0.#") + "k" : value.ToString("0");
    }
}
