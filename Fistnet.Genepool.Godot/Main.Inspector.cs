using Fistnet.Genepool.Control;
using Fistnet.Genepool.Dna;
using Godot;

namespace Fistnet.Genepool.GodotViewer;

public partial class Main
{
    private void UpdateInspector()
    {
        double scroll = InspectorText.GetVScrollBar().Value;
        InspectorText.Clear();
        var frame = DisplayedFrame;
        if (frame == null) { Welcome(); return; }
        var selected = frame.Selected;
        if (selected == null || selected.Id != selectedId)
        {
            if (selectedId.HasValue && selectionCommand != 0 && frame.AcknowledgedCommand >= selectionCommand)
            {
                Append(InspectorText, $"ORGANISM #{selectedId}\n\n", Accent, true);
                Append(InspectorText, "This organism was no longer present when your selection reached a completed season.\n\nNo earlier action history was collected for this selection. Click another organism to begin following it.", Muted);
            }
            else if (selectedCell is { } position)
            {
                var cell = frame.Cells[position.Y * frame.Width + position.X];
                Append(InspectorText, $"CELL {cell.X}, {cell.Y}\n\n", Accent, true);
                Append(InspectorText, $"Local food   {cell.Food} / 10\n" + (cell.OrganismId.HasValue ? $"Organism #{cell.OrganismId} · fetching completed details…" : "Empty · resources remain available here."), Ink);
            }
            else Welcome();
            return;
        }
        Append(InspectorText, $"ORGANISM #{selected.Id}" + (selected.IsAlive ? "  ·  following\n" : "  ·  died\n"), selected.IsAlive ? Accent : Error, true);
        Append(InspectorText, $"Position {selected.X}, {selected.Y}   ·   Generation {selected.Generation}\n", Ink);
        Append(InspectorText, $"Parents {Parent(selected.Parent1Id)} / {Parent(selected.Parent2Id)}\n", Muted);
        Append(InspectorText, $"Lifetime {selected.LifetimeSeasons} seasons\nBiological age {selected.Age}   ·   Sequence age {selected.SequenceAge} (inheritable)\n\n", Muted);
        Append(InspectorText, $"Health {selected.Health}   ·   Carried reserves {selected.Food} / 10\nLocal food {selected.LocalFood} / 10\nOffspring {selected.ChildrenObserved} observed since selection\n\n", Ink);
        Append(InspectorText, "DNA · EIGHT ORDERED SLOTS\n", Accent, true);
        var last = selected.Trace.LastOrDefault();
        foreach (var gene in selected.Genes)
        {
            bool chosen = last != null && gene.Slot == last.Slot;
            Append(InspectorText, $"{(chosen ? "▶" : " ")} {gene.Slot + 1}  {gene.Name}  {Target(gene.Target)}\n", chosen ? Warm : Ink, chosen);
        }
        Append(InspectorText, "\nTargets form a 3 × 3 neighborhood; ● means self.\n▶ marks the last chosen slot; DNA can change afterward.\n", Muted);
        Append(InspectorText, $"\nCOMPLETED ACTIONS · latest {selected.Trace.Count} / 16\n", Accent, true);
        if (!selected.IsAlive) Append(InspectorText, "Last observed state is retained. Select another organism to follow a new life.\n\n", Muted);
        if (selected.Trace.Count == 0) Append(InspectorText, "No actions observed for this selection yet.\n", Muted);
        foreach (var action in selected.Trace.Reverse())
        {
            Append(InspectorText, $"S{action.Season}  {action.Action} {Target(action.Target)}\n", Ink, true);
            Append(InspectorText, action.Result + "\n", Muted);
            var effects = new List<string>();
            if (action.FoodGathered != 0) effects.Add($"gathered {action.FoodGathered} at target");
            if (action.FoodSpent != 0) effects.Add($"spent {action.FoodSpent}");
            if (action.FoodTransferred != 0) effects.Add($"transferred {action.FoodTransferred}");
            if (action.Damage != 0) effects.Add($"damage {action.Damage}");
            if (action.Healing != 0) effects.Add($"healed {action.Healing}");
            if (action.BirthPlaced) effects.Add("child placed");
            if (action.Moved) effects.Add($"moved to {action.DestinationX}, {action.DestinationY}");
            if (effects.Count != 0) Append(InspectorText, string.Join(" · ", effects) + "\n", Ink);
            Append(InspectorText, $"Whole-season change: reserves {action.FoodChange:+0;-0;0}, health {action.HealthChange:+0;-0;0}\nAssociated reward {action.Reward:+0.00;-0.00;0.00} · {action.ChoiceLabel}\n\n", Muted);
        }
        RestoreScroll(InspectorText, scroll);
        static string Parent(long id) => id == 0 ? "founder" : $"#{id}";
        void Welcome()
        {
            Append(InspectorText, "FOLLOW A LIFE\n\n", Accent, true);
            Append(InspectorText, "Click an organism to follow its movement, DNA and completed actions. Click an empty cell to inspect its food.\n\nThe food layer includes food under organisms. In Combined view, each colored center is an organism and the surrounding green is local food.\n\nSeason = one opportunity to act\nCycle = eight seasons\nGeneration = parent-to-child depth\nBiological age = the organism's aging clock\nSequence age = DNA clock, which can be inherited", Muted);
        }
    }

    private void UpdatePatterns()
    {
        if (DisplayedFrame is not { } frame) return;
        var scroll = (ScrollContainer)patternList.GetParent(); int previous = scroll.ScrollVertical;
        foreach (Node child in patternList.GetChildren()) { patternList.RemoveChild(child); child.QueueFree(); }
        int index = 0;
        foreach (var pattern in frame.Patterns.Take(12))
        {
            string key = pattern.Key;
            var card = MakeButton("Pattern" + index++, "", () =>
            { patternKey = patternKey == key ? null : key; BoardView.HighlightedPattern = patternKey; UpdatePatterns(); }, patternKey == key);
            card.CustomMinimumSize = new Vector2(0, 104);
            card.TooltipText = pattern.Description + "\nGroups ordered action types only. Directions and learned preferences may differ. This is not a fitness ranking.";
            var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore }; Pad(margin, 9); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); card.AddChild(margin);
            var body = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; margin.AddChild(body);
            var count = MakeLabel($"{pattern.Count:N0} organisms   ·   {(frame.Population == 0 ? 0 : pattern.Count * 100.0 / frame.Population):0.0}%", 14, patternKey == key ? Accent : Ink);
            count.MouseFilter = MouseFilterEnum.Ignore; body.AddChild(count);
            var description = MakeLabel(pattern.Description, 12, Muted); description.AutowrapMode = TextServer.AutowrapMode.WordSmart; description.MouseFilter = MouseFilterEnum.Ignore; body.AddChild(description);
            patternList.AddChild(card);
        }
        if (frame.Patterns.Count == 0) patternList.AddChild(MakeLabel("No living patterns in this world.", 13, Muted));
        Callable.From(() => { if (GodotObject.IsInstanceValid(scroll)) scroll.ScrollVertical = previous; }).CallDeferred();
    }

    private void UpdateActivity()
    {
        if (DisplayedFrame is not { } frame) return;
        double scroll = ActivityText.GetVScrollBar().Value;
        ActivityText.Clear(); Append(ActivityText, "GENES ARE NOT ACTIONS\n\n", Accent, true);
        Append(ActivityText, "Gene abundance counts slots in living DNA. Performed actions count committed actions across the run; a gene can be present without ever acting.\n\n", Muted);
        Append(ActivityText, "GENE ABUNDANCE · living slots\n", Accent, true);
        foreach (var item in frame.GeneCounts) Append(ActivityText, $"{item.Name}   {item.Count:N0}\n", Ink);
        Append(ActivityText, "\nPERFORMED ACTIONS · run total\n", Accent, true);
        foreach (var item in frame.ActionCounts) Append(ActivityText, $"{item.Name}   {item.Count:N0}\n", Ink);
        Append(ActivityText, $"\nLast simulation season: {frame.LastSeasonMilliseconds:0.0} ms\nLast measured throughput: {frame.ActualSeasonsPerSecond:0.0} seasons/s\nRequested target: {(frame.TargetSeasonsPerSecond.HasValue ? frame.TargetSeasonsPerSecond.Value.ToString("0.#") + " seasons/s" : "Fastest")}\n\n", Muted);
        Append(ActivityText, "Camera and controls update with the display. Board data changes only when a completed snapshot arrives, generally up to 10 times/s during fast playback. Repainting does not advance the simulation.\n\nHistory retains 240 sampled completed seasons. Birth, death and action curves show interval averages per season. The selected trace records each completed season after selection (last 16).\n\nMarkers show at most 64 events from the latest published season; fast playback may skip events. Trail lines show completed movement positions.", Muted);
        if (lastSettlement != null) Append(ActivityText, "\n\n" + lastSettlement + " (request to boundary acknowledgment observed by the UI, including display scheduling).", Muted);
        RestoreScroll(ActivityText, scroll);
    }

    private static void Append(RichTextLabel label, string text, Color color, bool bold = false)
    { label.PushColor(color); if (bold) label.PushBold(); label.AddText(text); if (bold) label.Pop(); label.Pop(); }
    private static void RestoreScroll(RichTextLabel label, double value) => Callable.From(() =>
    { if (GodotObject.IsInstanceValid(label)) label.GetVScrollBar().Value = value; }).CallDeferred();

    public static string Target(string target) => target switch
    {
        "TopLeft" => "↖ NW", "TopCenter" => "↑ N", "TopRight" => "↗ NE", "MiddleLeft" => "← W", "Self" => "● self",
        "MiddleRight" => "→ E", "BottomLeft" => "↙ SW", "BottomCenter" => "↓ S", "BottomRight" => "↘ SE", _ => target
    };
    public static string Configuration(SimulationRunOptions options) => $"Random placement {options.InitialPopulationPercent}%  ·  " +
        (options.FounderRepertoire == FounderRepertoire.UnrestrictedRandom ? "Unrestricted random DNA" : "Constrained DNA: gather + reproduce") +
        $"  ·  Seed {options.Seed}  ·  Food {options.InitialCellFood}/10  ·  Regrowth {options.FoodRegrowthPerAge}/cycle  ·  Reserves {options.InitialOrganismFood}/10  ·  " +
        (options.Policy.Learning == LearningPolicy.RepairedLegacy ? "Legacy learning" : "Exploratory learning") + " / " +
        (options.Policy.Health == HealthPolicy.LegacyOverweight ? "overweight rule" : "capped health") + " / " +
        (options.Policy.Attack == AttackPolicy.DamageOnly ? "damage-only attacks" : "reserve-transfer attacks");
}
