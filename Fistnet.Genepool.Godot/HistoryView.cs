using Fistnet.Genepool.Control;
using Godot;

namespace Fistnet.Genepool.GodotViewer;

public partial class HistoryView : Godot.Control
{
    public ViewFrame? Frame { get; private set; }
    public int SampleCount => Frame?.History.Count ?? 0;
    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; Resized += QueueRedraw; }
    public void SetFrame(ViewFrame frame)
    {
        if (ReferenceEquals(Frame, frame)) return;
        Frame = frame; QueueRedraw();
    }
    public static double IntervalRate(long current, long previous, int currentSeason, int previousSeason) =>
        Math.Max(0, current - previous) / (double)Math.Max(1, currentSeason - previousSeason);

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), Main.Surface);
        if (Size.X < 400 || Size.Y < 90) return;
        Font font = GetThemeDefaultFont(); float columnWidth = Size.X / 4;
        Plot(0, "POPULATION", Frame?.Population.ToString("N0") ?? "—", Main.Accent, h => h.Population);
        Plot(1, "FOOD", "local / carried", new Color("84cf93"), h => h.LocalFood, h => h.StoredFood, new Color("edc464"));
        Plot(2, "BIRTHS / DEATHS", "average / season", Main.Accent, h => h.Births, h => h.Deaths, new Color("f57e8f"), true);
        Plot(3, "PERFORMED ACTIONS", "average / season", new Color("88aaf9"), h => h.ActionCount, difference: true);

        void Text(string value, Vector2 position, Color color, int size = 12) => DrawString(font, position, value, HorizontalAlignment.Left, -1, size, color);
        void Plot(int column, string title, string subtitle, Color color, Func<ViewHistory, long> primary,
            Func<ViewHistory, long>? secondary = null, Color? other = null, bool difference = false)
        {
            float left = column * columnWidth;
            Text(title, new Vector2(left + 14, 21), Main.Ink); Text(subtitle, new Vector2(left + 14, 39), Main.Muted);
            var plot = new Rect2(left + 43, 52, columnWidth - 59, Size.Y - 79);
            DrawLine(plot.Position, new Vector2(plot.End.X, plot.Position.Y), new Color("283647"));
            DrawLine(new Vector2(plot.Position.X, plot.End.Y), plot.End, new Color("283647"));
            if (Frame == null || Frame.History.Count == 0) { Text("Waiting for completed seasons", plot.Position + new Vector2(0, 17), Main.Muted, 11); return; }
            var samples = Frame.History; int count = samples.Count;
            double maximum = 1;
            for (int i = 0; i < count; i++) { maximum = Math.Max(maximum, Value(primary, i)); if (secondary != null) maximum = Math.Max(maximum, Value(secondary, i)); }
            maximum = Math.Ceiling(maximum * 1.05);
            Text(Compact(maximum), new Vector2(left + 5, plot.Position.Y + 5), Main.Muted, 11);
            Text("0", new Vector2(left + 28, plot.End.Y), Main.Muted, 11);
            int first = samples[0].Season, last = samples[count - 1].Season;
            Text(first.ToString(), new Vector2(plot.Position.X, plot.End.Y + 18), Main.Muted, 11);
            string end = $"{last} seasons";
            float endWidth = font.GetStringSize(end, HorizontalAlignment.Left, -1, 11).X;
            Text(end, new Vector2(plot.End.X - endWidth, plot.End.Y + 18), Main.Muted, 11);
            Line(primary, color); if (secondary != null) Line(secondary, other ?? color);
            double Value(Func<ViewHistory, long> source, int index) => !difference ? source(samples[index]) : index == 0 ? 0 : IntervalRate(source(samples[index]), source(samples[index - 1]), samples[index].Season, samples[index - 1].Season);
            void Line(Func<ViewHistory, long> source, Color lineColor)
            {
                var points = new Vector2[count];
                for (int i = 0; i < count; i++) points[i] = new Vector2(
                    plot.Position.X + (count == 1 ? .5f : (samples[i].Season - first) / (float)Math.Max(1, last - first)) * plot.Size.X,
                    plot.End.Y - (float)(Value(source, i) / maximum) * plot.Size.Y);
                if (count > 1) DrawPolyline(points, lineColor, 1.7f, true);
                DrawCircle(points[count - 1], 2, lineColor);
            }
        }
    }
    private static string Compact(double value) => value >= 1_000_000 ? $"{value / 1_000_000:0.#}m" : value >= 1_000 ? $"{value / 1_000:0.#}k" : $"{value:0}";
}
