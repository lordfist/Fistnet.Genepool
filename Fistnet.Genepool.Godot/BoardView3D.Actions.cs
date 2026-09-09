using Fistnet.Genepool.Control;
using Fistnet.Genepool.Control.Presentation;
using Godot;
using System.Runtime.InteropServices;

namespace Fistnet.Genepool.GodotViewer;

public partial class BoardView3D
{
    private readonly Dictionary<Color, List<Vector2>> actionLineBatches = new();
    private bool batchActionLines;
    private Vector2 overlayScale;
    public int VisibleWorldEdgeCount
    {
        get
        {
            if (frame == null) return 0;
            Vector2[] corners = WorldCorners();
            int count = 0;
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = ProjectGround(corners[i]), b = ProjectGround(corners[(i + 1) % 4]);
                if (ClipLineToView(ref a, ref b, Size)) count++;
            }
            return count;
        }
    }

    private (int X, int Y)? SelectedPosition()
    {
        if (frame == null) return null;
        if (selectedOrganismId.HasValue)
        {
            if (frame.Selected != null && frame.Selected.Id == selectedOrganismId)
                return (frame.Selected.X, frame.Selected.Y);
            foreach (ViewCell cell in frame.Cells)
                if (cell.OrganismId == selectedOrganismId) return (cell.X, cell.Y);
        }
        return selectedCell;
    }

    private bool InWorld(int x, int y) => frame != null && BoardPresentation.ContainsCell(x, y, frame.Width, frame.Height);
    private bool VisibleAction(ViewAction action) => IsCellVisible(action.SourceX, action.SourceY) ||
        IsCellVisible(action.DestinationX, action.DestinationY) || IsCellVisible(action.TargetX, action.TargetY) ||
        (action.BirthPlaced && IsCellVisible(action.BirthX, action.BirthY));
    private Vector2 ActionPoint(int x, int y) => ProjectGround(new Vector2(x + .5f, y + .5f)) * overlayScale;
    private Vector2[] WorldCorners() => new[] { Vector2.Zero, new Vector2(frame!.Width, 0),
        new Vector2(frame.Width, frame.Height), new Vector2(0, frame.Height) };

    // Clip projected world edges to the window. Interior viewport crops never acquire a rim.
    private static bool ClipLineToView(ref Vector2 a, ref Vector2 b, Vector2 size)
    {
        Vector2 start = a, delta = b - a;
        float enter = 0, leave = 1;
        bool Clip(float p, float q)
        {
            if (Math.Abs(p) < .00001f) return q >= 0;
            float t = q / p;
            if (p < 0) { if (t > leave) return false; enter = Math.Max(enter, t); }
            else { if (t < enter) return false; leave = Math.Min(leave, t); }
            return true;
        }
        if (!Clip(-delta.X, start.X) || !Clip(delta.X, size.X - start.X) ||
            !Clip(-delta.Y, start.Y) || !Clip(delta.Y, size.Y - start.Y)) return false;
        a = start + delta * enter; b = start + delta * leave;
        return true;
    }

    public static string ClassifyActionCue(ViewAction action) => action.Moved ? "move" : action.BirthPlaced ? "birth" :
        action.Mutations > 0 ? "mutate" :
        action.Damage > 0 ? "attack" : action.FoodGathered > 0 ? "gather" : action.FoodTransferred > 0 ? "transfer" :
        action.FoodSpent > 0 && action.Action.Contains("Eat", StringComparison.OrdinalIgnoreCase) ? "consume" :
        action.Healing > 0 ? "heal" :
        action.Action.Equals("Wait", StringComparison.OrdinalIgnoreCase) ? "wait" : "ineffective";

    private void DrawOverlay()
    {
        ActionCueCount = 0;
        batchActionLines = false;
        if (overlay == null || frame == null || Size.X <= 0 || Size.Y <= 0) return;
        Vector2 localSize = Size;
        Vector2I viewportSize = viewport!.Size;
        overlayScale = new Vector2(viewportSize.X / localSize.X, viewportSize.Y / localSize.Y);
        int detail = DetailLevel;
        float deviceScale = viewportSize.Y / Math.Max(1, localSize.Y);
        float width = Math.Max(1, deviceScale);
        Vector2[] corners = WorldCorners();
        for (int i = 0; i < 4; i++)
        {
            Vector2 a = ProjectGround(corners[i]), b = ProjectGround(corners[(i + 1) % 4]);
            if (ClipLineToView(ref a, ref b, localSize))
                overlay.DrawLine(a * overlayScale, b * overlayScale, new Color("c2ac7480"), width, true);
        }

        if (showTrail && detail >= 3 && frame.Selected != null && frame.Selected.Id == selectedOrganismId)
        {
            foreach (ViewAction action in BoardPresentation.MovementTrail(frame.Selected, frame.Width, frame.Height))
                if (VisibleAction(action)) overlay.DrawLine(ActionPoint(action.SourceX, action.SourceY),
                    ActionPoint(action.DestinationX, action.DestinationY), new Color("e9d9a74c"), width, true);
        }

        if (MarkersVisible && detail >= 3)
        {
            bool focused = detail == 4;
            batchActionLines = !focused;
            if (batchActionLines)
                foreach (List<Vector2> lines in actionLineBatches.Values) lines.Clear();
            foreach (ViewObservedAction observation in frame.SeasonActions)
            {
                ViewAction action = observation.Action;
                if (action.Season != frame.Season || !VisibleAction(action)) continue;
                if (focused && observation.OrganismId != selectedOrganismId) continue;
                DrawObservedAction(action, focused, deviceScale);
                ActionCueCount++;
            }
            if (batchActionLines)
                foreach (var batch in actionLineBatches)
                    if (batch.Value.Count > 0)
                        overlay.DrawMultiline(CollectionsMarshal.AsSpan(batch.Value), batch.Key, 1.15f * deviceScale, true);
            batchActionLines = false;
        }

        if (SelectedPosition() is { } selected && InWorld(selected.X, selected.Y) && IsCellVisible(selected.X, selected.Y))
        {
            Vector2 position = ActionPoint(selected.X, selected.Y);
            if (SelectionIsHistorical)
            {
                // This cell records where a selected life ended; a later occupant does not inherit selection.
                float historicalSize = Math.Clamp(ProjectedCellSizeAt(new Vector2(selected.X + .5f, selected.Y + .5f)) * .25f, 4, 15) * deviceScale;
                Cross(position + new Vector2(historicalSize, -historicalSize), historicalSize * .5f,
                    new Color(.77f, .72f, .61f, .65f), width);
                return;
            }
            Vector2 groundCenter = new(selected.X + .5f, selected.Y + .5f);
            float radius = Math.Max(.45f, 5 / Math.Max(.01f, ProjectedCellSizeAt(groundCenter)));
            const int sides = 36;
            var points = new Vector2[sides + 1];
            for (int i = 0; i <= sides; i++)
            {
                float theta = i * Mathf.Tau / sides;
                points[i] = ProjectGround(groundCenter + new Vector2(MathF.Cos(theta), MathF.Sin(theta)) * radius) * overlayScale;
            }
            overlay.DrawPolyline(points, new Color("ffe59a"), Math.Max(1.5f, 2 * deviceScale), true);
            overlay.DrawCircle(position, Math.Max(1.2f, 1.5f * deviceScale), new Color("fff4ca"));
        }
    }

    private void DrawObservedAction(ViewAction action, bool focused, float deviceScale)
    {
        if (overlay == null) return;
        float emphasis = focused ? 1 : .40f;
        Vector2 cueCell = InWorld(action.TargetX, action.TargetY) ? new Vector2(action.TargetX + .5f, action.TargetY + .5f) :
            new Vector2(action.DestinationX + .5f, action.DestinationY + .5f);
        float size = Math.Clamp(ProjectedCellSizeAt(cueCell) * (focused ? .15f : .11f), 3, focused ? 12 : 6) * deviceScale;
        float lineWidth = (focused ? 2 : 1.15f) * deviceScale;
        Vector2 source = InWorld(action.SourceX, action.SourceY) ? ActionPoint(action.SourceX, action.SourceY) :
            ActionPoint(action.DestinationX, action.DestinationY);
        Vector2 destination = InWorld(action.DestinationX, action.DestinationY) ? ActionPoint(action.DestinationX, action.DestinationY) : source;
        Vector2 target = InWorld(action.TargetX, action.TargetY) ? ActionPoint(action.TargetX, action.TargetY) : source;
        Color gold = new(1, .83f, .36f, emphasis);
        bool effect = false;

        if (action.Moved && InWorld(action.SourceX, action.SourceY) && InWorld(action.DestinationX, action.DestinationY) && source.DistanceTo(destination) > 1)
        {
            // The body is in the completed snapshot's destination. Its old location is an outline, never a second body.
            ActionRing(source, size * 1.5f, new Color(.87f, .95f, .92f, emphasis * .64f), lineWidth, 18);
            Arrow(source, destination, gold, lineWidth, size);
            effect = true;
        }
        if (action.BirthPlaced)
        {
            Color birth = new(.62f, 1, .66f, emphasis);
            Vector2 child = action.BirthOrganismId > 0 && InWorld(action.BirthX, action.BirthY) ? ActionPoint(action.BirthX, action.BirthY) : source;
            ActionRing(child, size * 2, birth, lineWidth, 22);
            if (child != source) Arrow(source, child, birth, lineWidth, size);
            effect = true;
        }
        if (action.Damage > 0)
        {
            Color attack = new(1, .42f, .32f, emphasis);
            Arrow(source, target, attack, lineWidth, size);
            // Outcomes refer to the target cell at action time. The completed frame can contain another occupant.
            Cross(target + new Vector2(size * 1.8f, -size * 1.8f), size * .55f, attack, lineWidth); effect = true;
        }
        if (action.FoodGathered > 0)
        {
            Color gather = new(.81f, .94f, .38f, emphasis);
            Vector2 offset = new(-size * 1.7f, size * 1.1f);
            if (batchActionLines) ActionRing(target + offset, size * .30f, gather, lineWidth, 6);
            else overlay.DrawCircle(target + offset, size * .30f, gather);
            Arrow(target + offset, target, gather, lineWidth, size * .5f);
            if (focused && target.DistanceTo(source) > 1) Arrow(source, target, gather, lineWidth, size);
            effect = true;
        }
        if (action.FoodTransferred > 0)
        {
            Color transfer = new(1, .71f, .34f, emphasis);
            Arrow(target, destination, transfer, lineWidth, size * .7f); effect = true;
        }
        if (action.FoodSpent > 0 && action.Action.Contains("Eat", StringComparison.OrdinalIgnoreCase))
        {
            Color carried = new(.96f, .80f, .45f, emphasis);
            Vector2 p = destination + new Vector2(-size * 1.5f, -size * 1.3f);
            ActionSquare(p, size * .4f, carried, lineWidth);
            Arrow(p, destination, carried, lineWidth, size * .5f);
            effect = true;
        }
        if (action.Healing > 0)
        {
            Color heal = new(.62f, 1, .72f, emphasis);
            Vector2 p = target + new Vector2(size * 1.4f, -size);
            ActionLine(p - Vector2.Right * size * .6f, p + Vector2.Right * size * .6f, heal, lineWidth);
            ActionLine(p - Vector2.Down * size * .6f, p + Vector2.Down * size * .6f, heal, lineWidth);
            if (focused && target.DistanceTo(source) > 1) Arrow(source, target, heal, lineWidth, size);
            effect = true;
        }
        if (action.Mutations > 0)
        {
            Color mutation = new(.83f, .65f, 1, emphasis);
            Vector2 p = target + new Vector2(size * 1.2f, -size);
            ActionLine(p + Vector2.Left * size, p + Vector2.Up * size, mutation, lineWidth);
            ActionLine(p + Vector2.Up * size, p + Vector2.Right * size, mutation, lineWidth);
            ActionLine(p + Vector2.Right * size, p + Vector2.Down * size, mutation, lineWidth);
            ActionLine(p + Vector2.Down * size, p + Vector2.Left * size, mutation, lineWidth);
            effect = true;
        }
        if (!effect)
        {
            string kind = ClassifyActionCue(action);
            bool wait = kind == "wait";
            bool consumed = kind == "consume";
            Color quiet = consumed ? new Color(.96f, .80f, .45f, emphasis) : new Color(.90f, .89f, .80f, emphasis * .84f);
            Vector2 p = destination + new Vector2(size * 1.25f, -size * 1.15f);
            if (wait || consumed)
                ActionRing(p, size * .55f, quiet, lineWidth, 12);
            else
            {
                if (source.DistanceTo(target) > 1) DashedLine(source, target, quiet, lineWidth, size);
                Cross(p, size * .45f, quiet, lineWidth);
            }
        }

        if (focused)
        {
            string quantity = ClassifyActionCue(action) == "consume" ? $"ate {action.FoodSpent} · healed {action.Healing}" :
                action.Mutations > 0 ? $"DNA changes {action.Mutations}" + (action.Healing > 0 ? $" · healed {action.Healing}" : "") :
                action.Moved ? "moved" : action.BirthPlaced ? "child placed" :
                action.Damage > 0 ? $"damage {action.Damage}" : action.Healing > 0 ? $"healed {action.Healing}" :
                action.FoodGathered > 0 ? $"gathered {action.FoodGathered}" : action.Result;
            string slot = action.Slot < 0 ? "No chosen slot" : $"Slot {action.Slot + 1}";
            string text = $"{slot} · {action.Action} · {quantity}";
            if (InWorld(action.TargetX, action.TargetY) &&
                (action.TargetX != action.SourceX || action.TargetY != action.SourceY))
                text += $" · target ({action.TargetX}, {action.TargetY}) at action";
            Vector2 p = destination + new Vector2(size * 1.4f, -size * 2.2f);
            int fontSize = Math.Max(12, (int)(13 * deviceScale));
            float textWidth = ThemeDB.FallbackFont.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize).X;
            p.X = Math.Clamp(p.X, 6, Math.Max(6, viewport!.Size.X - textWidth - 6));
            p.Y = Math.Clamp(p.Y, fontSize + 6, Math.Max(fontSize + 6, viewport!.Size.Y - 6));
            overlay.DrawRect(new Rect2(p - new Vector2(5, fontSize), new Vector2(textWidth + 10, fontSize + 6)), new Color(.04f, .07f, .09f, .88f));
            overlay.DrawString(ThemeDB.FallbackFont, p, text, HorizontalAlignment.Left, -1, fontSize, new Color("fff2c4"));
        }
    }

    private void Cross(Vector2 position, float size, Color color, float width)
    {
        ActionLine(position - new Vector2(size, size), position + new Vector2(size, size), color, width);
        ActionLine(position + new Vector2(size, -size), position + new Vector2(-size, size), color, width);
    }
    private void Arrow(Vector2 source, Vector2 target, Color color, float width, float size)
    {
        Vector2 direction = target - source;
        if (direction.LengthSquared() < 1) return;
        direction = direction.Normalized();
        Vector2 end = target - direction * size;
        ActionLine(source + direction * size, end, color, width);
        Vector2 side = new(-direction.Y, direction.X);
        ActionLine(end, end - direction * size + side * size * .52f, color, width);
        ActionLine(end, end - direction * size - side * size * .52f, color, width);
    }
    private void DashedLine(Vector2 source, Vector2 target, Color color, float width, float size)
    {
        Vector2 line = target - source;
        float length = line.Length();
        if (length < 1) return;
        Vector2 direction = line / length;
        float dash = Math.Max(2, size * .7f);
        for (float start = size; start < length - size; start += dash * 2)
            ActionLine(source + direction * start, source + direction * Math.Min(start + dash, length - size), color, width);
    }

    private void ActionLine(Vector2 from, Vector2 to, Color color, float width)
    {
        if (!batchActionLines) { overlay!.DrawLine(from, to, color, width, true); return; }
        if (!actionLineBatches.TryGetValue(color, out List<Vector2>? lines))
            actionLineBatches.Add(color, lines = new List<Vector2>(256));
        lines.Add(from); lines.Add(to);
    }

    private void ActionRing(Vector2 centerPoint, float radius, Color color, float width, int segments)
    {
        if (!batchActionLines) { overlay!.DrawArc(centerPoint, radius, 0, Mathf.Tau, segments, color, width, true); return; }
        segments = Math.Min(8, segments);
        Vector2 previous = centerPoint + Vector2.Right * radius;
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * Mathf.Tau / segments;
            Vector2 next = centerPoint + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            ActionLine(previous, next, color, width); previous = next;
        }
    }

    private void ActionSquare(Vector2 centerPoint, float halfSize, Color color, float width)
    {
        if (!batchActionLines)
        { overlay!.DrawRect(new Rect2(centerPoint - Vector2.One * halfSize, Vector2.One * halfSize * 2), color, false, width); return; }
        Vector2 a = centerPoint + new Vector2(-halfSize, -halfSize), b = centerPoint + new Vector2(halfSize, -halfSize);
        Vector2 c = centerPoint + new Vector2(halfSize, halfSize), d = centerPoint + new Vector2(-halfSize, halfSize);
        ActionLine(a, b, color, width); ActionLine(b, c, color, width);
        ActionLine(c, d, color, width); ActionLine(d, a, color, width);
    }
}
