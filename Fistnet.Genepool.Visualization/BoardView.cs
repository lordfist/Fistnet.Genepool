using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows.Forms;
using Fistnet.Genepool.Control;

namespace Fistnet.Genepool.Visualization
{
    public enum BoardLayer { Organisms, Food, Combined }

    /// <summary>A passive, zoomable surface over detached completed-season values.</summary>
    [SupportedOSPlatform("windows6.1")]
    public sealed class BoardView : System.Windows.Forms.Control
    {
        private ViewFrame frame;
        private Bitmap raster;
        private int[] pixels;
        private float zoom = 1;
        private PointF pan;
        private Point dragOrigin;
        private PointF dragPan;
        private bool dragging;
        private BoardLayer layer = BoardLayer.Combined;
        private string highlightedPattern;
        private long markerDeadline;
        private bool markersExpired;
        public event Action<Point> CellPicked;
        public event Action ViewChanged;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ViewFrame Frame => frame;
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public float Zoom => zoom;
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Point? SelectedCell { get; set; }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public long? SelectedOrganismId { get; set; }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowTrail { get; set; }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowMarkers { get; set; }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public BoardLayer Layer
        {
            get => layer;
            set { if (layer == value) return; layer = value; RebuildRaster(); Invalidate(); }
        }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string HighlightedPattern
        {
            get => highlightedPattern;
            set { if (highlightedPattern == value) return; highlightedPattern = value; RebuildRaster(); Invalidate(); }
        }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public RectangleF WorldRectangle
        {
            get
            {
                float size = Math.Max(1, Math.Min(ClientSize.Width - 28, ClientSize.Height - 28)) * zoom;
                return new RectangleF((ClientSize.Width - size) / 2 + pan.X, (ClientSize.Height - size) / 2 + pan.Y, size, size);
            }
        }

        public BoardView()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(11, 17, 26);
            ForeColor = Color.FromArgb(225, 236, 247);
            Cursor = Cursors.Cross;
            TabStop = true;
            AccessibleName = "Simulation board";
            AccessibleDescription = "Click a cell to inspect. Mouse wheel zooms; drag with the right or middle mouse button to pan.";
        }

        public void SetFrame(ViewFrame value)
        {
            if (ReferenceEquals(frame, value)) return;
            bool newSeason = frame == null || value == null || frame.RunId != value.RunId || frame.Season != value.Season;
            frame = value;
            if (newSeason) { markerDeadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 2; markersExpired = false; }
            RebuildRaster();
            Invalidate();
        }

        public void ExpireMarkers()
        {
            if (markersExpired || Stopwatch.GetTimestamp() < markerDeadline) return;
            markersExpired = true; if (ShowMarkers) Invalidate();
        }

        public void Fit()
        {
            zoom = 1; pan = PointF.Empty; Invalidate(); ViewChanged?.Invoke();
        }

        public void ZoomBy(float factor, Point anchor)
        {
            RectangleF before = WorldRectangle;
            float next = Math.Clamp(zoom * factor, 1, 12);
            if (next == zoom) return;
            float fractionX = (anchor.X - before.Left) / before.Width;
            float fractionY = (anchor.Y - before.Top) / before.Height;
            zoom = next;
            RectangleF after = WorldRectangle;
            pan.X += anchor.X - (after.Left + fractionX * after.Width);
            pan.Y += anchor.Y - (after.Top + fractionY * after.Height);
            ClampPan(); Invalidate(); ViewChanged?.Invoke();
        }

        public Point? CellAt(Point location)
        {
            RectangleF world = WorldRectangle;
            if (frame == null || !world.Contains(location)) return null;
            int x = (int)((location.X - world.Left) * frame.Width / world.Width);
            int y = (int)((location.Y - world.Top) * frame.Height / world.Height);
            return x < 0 || y < 0 || x >= frame.Width || y >= frame.Height ? null : new Point(x, y);
        }

        public static Color FoodColor(int food)
        {
            float t = Math.Clamp(food, 0, 10) / 10f;
            return Color.FromArgb((int)(17 + 99 * t), (int)(28 + 160 * t), (int)(37 + 99 * t));
        }

        private void RebuildRaster()
        {
            if (frame == null) return;
            int scale = layer == BoardLayer.Combined ? 3 : 1;
            int width = frame.Width * scale, height = frame.Height * scale;
            if (raster == null || raster.Width != width || raster.Height != height)
            {
                raster?.Dispose(); raster = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                pixels = new int[width * height];
            }
            foreach (ViewCell cell in frame.Cells)
            {
                int background = layer == BoardLayer.Organisms ? Color.FromArgb(14, 21, 31).ToArgb() : FoodColor(cell.Food).ToArgb();
                int organism = cell.ColorArgb | unchecked((int)0xff000000);
                if ((organism & 0x00ffffff) == 0) organism = Color.FromArgb(95, 112, 133).ToArgb();
                if (highlightedPattern != null && cell.PatternKey != highlightedPattern)
                {
                    Color color = Color.FromArgb(organism);
                    organism = Color.FromArgb(color.R / 4 + 16, color.G / 4 + 16, color.B / 4 + 20).ToArgb();
                }
                int index = cell.Y * scale * width + cell.X * scale;
                if (scale == 1) pixels[index] = layer == BoardLayer.Organisms && cell.OrganismId.HasValue ? organism : background;
                else
                {
                    for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++) pixels[index + y * width + x] = background;
                    if (cell.OrganismId.HasValue) pixels[index + width + 1] = organism;
                }
            }
            BitmapData data = raster.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try { Marshal.Copy(pixels, 0, data.Scan0, pixels.Length); }
            finally { raster.UnlockBits(data); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            long started = Stopwatch.GetTimestamp();
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            if (frame == null || raster == null)
            {
                TextRenderer.DrawText(g, "Preparing a new world…", Font, ClientRectangle, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }
            RectangleF world = WorldRectangle;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(raster, world);
            float cellSize = world.Width / frame.Width;
            if (cellSize >= 15)
            {
                using var grid = new Pen(Color.FromArgb(30, 255, 255, 255));
                for (int i = 0; i <= frame.Width; i++) g.DrawLine(grid, world.Left + i * cellSize, world.Top, world.Left + i * cellSize, world.Bottom);
                for (int i = 0; i <= frame.Height; i++) g.DrawLine(grid, world.Left, world.Top + i * cellSize, world.Right, world.Top + i * cellSize);
            }
            if (ShowTrail && frame.Selected != null && frame.Selected.Id == SelectedOrganismId)
            {
                using var trail = new Pen(Color.FromArgb(170, 249, 217, 111), 2);
                foreach (var action in frame.Selected.Trace)
                    if (action.SourceX >= 0 && action.DestinationX >= 0)
                        g.DrawLine(trail, Center(action.SourceX, action.SourceY), Center(action.DestinationX, action.DestinationY));
            }
            if (ShowMarkers && Stopwatch.GetTimestamp() < markerDeadline)
            {
                foreach (var marker in frame.Markers)
                {
                    Color color = marker.Kind == "birth" ? Color.FromArgb(125, 239, 187) : marker.Kind == "death" ? Color.FromArgb(255, 126, 136) : Color.FromArgb(255, 216, 114);
                    using var markerPen = new Pen(color, 1.5f);
                    PointF p = Center(marker.X, marker.Y);
                    float radius = Math.Max(3, cellSize * .7f);
                    if (marker.Kind == "death") { g.DrawLine(markerPen, p.X - radius, p.Y - radius, p.X + radius, p.Y + radius); g.DrawLine(markerPen, p.X + radius, p.Y - radius, p.X - radius, p.Y + radius); }
                    else g.DrawEllipse(markerPen, p.X - radius, p.Y - radius, radius * 2, radius * 2);
                }
            }
            Point? selected = frame.Selected != null && frame.Selected.Id == SelectedOrganismId ? new Point(frame.Selected.X, frame.Selected.Y) : SelectedCell;
            if (selected.HasValue)
            {
                PointF p = Center(selected.Value.X, selected.Value.Y);
                float radius = Math.Max(5, cellSize * .65f);
                using var selection = new Pen(Color.FromArgb(255, 237, 165), 2);
                g.DrawRectangle(selection, p.X - radius, p.Y - radius, radius * 2, radius * 2);
            }
            using var border = new Pen(Color.FromArgb(66, 88, 110));
            g.DrawRectangle(border, world.X, world.Y, world.Width, world.Height);
            LastPaintMilliseconds = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            PointF Center(int x, int y) => new PointF(world.Left + (x + .5f) * cellSize, world.Top + (y + .5f) * cellSize);
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double LastPaintMilliseconds { get; private set; }

        protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); ZoomBy(e.Delta > 0 ? 1.25f : .8f, e.Location); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e); Focus();
            if (e.Button == MouseButtons.Right || e.Button == MouseButtons.Middle)
            { dragging = true; dragOrigin = e.Location; dragPan = pan; Capture = true; Cursor = Cursors.SizeAll; }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging) return;
            pan = new PointF(dragPan.X + e.X - dragOrigin.X, dragPan.Y + e.Y - dragOrigin.Y);
            ClampPan(); Invalidate();
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (dragging) { dragging = false; Capture = false; Cursor = Cursors.Cross; }
            else if (e.Button == MouseButtons.Left) { Point? cell = CellAt(e.Location); if (cell.HasValue) CellPicked?.Invoke(cell.Value); }
        }
        protected override void OnResize(EventArgs e) { base.OnResize(e); ClampPan(); }
        private void ClampPan()
        {
            if (zoom <= 1) { pan = PointF.Empty; return; }
            float size = Math.Max(1, Math.Min(ClientSize.Width - 28, ClientSize.Height - 28)) * zoom;
            pan.X = Math.Clamp(pan.X, -Math.Max(0, (size - ClientSize.Width) / 2 + 14), Math.Max(0, (size - ClientSize.Width) / 2 + 14));
            pan.Y = Math.Clamp(pan.Y, -Math.Max(0, (size - ClientSize.Height) / 2 + 14), Math.Max(0, (size - ClientSize.Height) / 2 + 14));
        }
        protected override void Dispose(bool disposing) { if (disposing) raster?.Dispose(); base.Dispose(disposing); }
    }
}
