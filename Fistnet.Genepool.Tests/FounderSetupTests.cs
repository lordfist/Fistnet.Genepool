using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Windows.Forms;
using Fistnet.Genepool.App;
using Fistnet.Genepool.Control;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Dna;
using Fistnet.Genepool.Dna.Elements;

namespace Fistnet.Genepool.Tests;

[SupportedOSPlatform("windows6.1")]
internal static class FounderSetupTests
{
    public static IEnumerable<TestCase> Cases()
    {
        yield return new("founders: constrained repertoire retains valid ownership, slots and random directions", "founder-setup", FounderGenes);
        yield return new("founders: late mandatory genes count toward the per-type limit", "founder-setup", ReservedTypeCapacity);
        yield return new("founders: unrestricted defaults and explicit replay preserve reference state", "founder-setup", Replay);
        yield return new("founders: invalid repertoire is rejected before world or RNG changes", "founder-setup", InvalidOptions);
        yield return new("founders: supplied occupants and inherited DNA are not rewritten", "founder-setup", FounderOnly);
        yield return new("founders: empty-world export and detached restart retain the chosen repertoire", "founder-setup", OptionsSurviveObservation);
        yield return new("founders: current UI settings round-trip the repertoire and explicit seed", "founder-setup", DialogRoundTrip);
        yield return new("founders: named comparisons change one family and restore unrestricted defaults", "founder-setup", DialogPresets);
    }

    private static SimulationRunOptions Options(FounderRepertoire repertoire = FounderRepertoire.UnrestrictedRandom, int density = 0) => new()
    { Mode = SimulationMode.DeterministicReference, Seed = 701, InitialPopulationPercent = density, FounderRepertoire = repertoire };

    private static void FounderGenes()
    {
        var slots = new HashSet<int>(); var targets = new HashSet<TargetTypes>(); var patterns = new HashSet<string>();
        foreach (int seed in new[] { 701, 702, 703, 704 })
        {
            Common.ConfigureRandom(new SeededRandomSource(seed), true); Common.ConfigurePolicy(new SimulationPolicy());
            for (int n = 0; n < 32; n++)
            {
                var actor = new Organism(2, FounderRepertoire.GatherAndReproduce);
                Check.Equal(8, actor.DnaSequence.Count); Check.Equal(2, actor.FoodBalance);
                Check.True(actor.DnaSequence.Any(g => g.DnaType == DnaTypes.GenerateFood), "No resource-gathering gene");
                Check.True(actor.DnaSequence.Any(g => g.DnaType == DnaTypes.CombineDna), "No reproduction gene");
                foreach (var group in actor.DnaSequence.GroupBy(g => g.DnaType))
                    Check.True(group.Count() <= Organism.DNA_SEQUENCE_MAXSINGLETYPE, "Founder exceeded the per-type limit");
                for (int i = 0; i < actor.DnaSequence.Count; i++)
                {
                    var gene = actor.DnaSequence[i];
                    Check.True(ReferenceEquals(actor, gene.Me), "Gene belongs to another actor");
                    Check.Equal((byte)i, gene.DnaSequenceIndex); Check.True(Enum.IsDefined(gene.Target), "Invalid target");
                    if (gene.DnaType == DnaTypes.GenerateFood) { slots.Add(i); targets.Add(gene.Target); }
                }
                patterns.Add(string.Join(",", actor.DnaSequence.Select(g => $"{g.DnaType}:{g.Target}")));
            }
        }
        // Fixed fixture seeds exercise variability; this is not a statistical claim of uniformity.
        Check.True(slots.Count > 1 && targets.Count > 1 && patterns.Count > 1, "Optional setup fixed slots, directions or the whole genome");
    }

    private static void ReservedTypeCapacity()
    {
        Common.ConfigurePolicy(new SimulationPolicy());
        Common.ConfigureRandom(new ScriptedRandomSource(7, 6, 2, 0, 2, 0, 2, 0, 3, 0, 3, 0, 3, 0, 0, 0), true);
        var actor = new Organism(5, FounderRepertoire.GatherAndReproduce);
        Check.Equal(DnaTypes.GenerateFood, actor.DnaSequence[7].DnaType);
        Check.Equal(DnaTypes.CombineDna, actor.DnaSequence[6].DnaType);
        Check.Equal(4, actor.DnaSequence.Count(g => g.DnaType == DnaTypes.GenerateFood));
        Check.Equal(4, actor.DnaSequence.Count(g => g.DnaType == DnaTypes.CombineDna));
        Check.True(actor.DnaSequence.All(g => g.Target == TargetTypes.TopLeft), "Preset silently forced self-targets");
    }

    private static void Replay()
    {
        Board.Reset(new SimulationRunOptions { Mode = SimulationMode.DeterministicReference, Seed = 701, InitialPopulationPercent = 1 });
        var defaultState = (SimulationSnapshot.Capture().Sha256, Common.RandomSource.State, Common.RandomSource.DrawCount);
        Board.Reset(Options(density: 1));
        Check.Equal(defaultState, (SimulationSnapshot.Capture().Sha256, Common.RandomSource.State, Common.RandomSource.DrawCount), "Explicit unrestricted initialization changed default replay");
        Board.Reset(Options(FounderRepertoire.GatherAndReproduce, 1));
        var first = (SimulationSnapshot.Capture().Sha256, Common.RandomSource.State, Common.RandomSource.DrawCount);
        Check.True(first.Item1 != defaultState.Item1, "Constrained setup did not affect the initialized state");
        Board.Reset(Options(FounderRepertoire.GatherAndReproduce, 1));
        Check.Equal(first, (SimulationSnapshot.Capture().Sha256, Common.RandomSource.State, Common.RandomSource.DrawCount), "Constrained initialization cannot be replayed");
    }

    private static void InvalidOptions()
    {
        Board.Reset(Options());
        var original = (SimulationSnapshot.Capture().Sha256, Common.RandomSource.State, Common.RandomSource.DrawCount, Common.OrganismIdentityCounter);
        bool refused = false;
        try { Board.Reset(Options((FounderRepertoire)999)); } catch (ArgumentException) { refused = true; }
        Check.True(refused, "Board accepted an unknown repertoire");
        Check.Equal(original, (SimulationSnapshot.Capture().Sha256, Common.RandomSource.State, Common.RandomSource.DrawCount, Common.OrganismIdentityCounter));
        refused = false;
        try { _ = new Organism(5, (FounderRepertoire)999); } catch (ArgumentException) { refused = true; }
        Check.True(refused, "Direct founder construction accepted an unknown repertoire");
        Check.Equal(original, (SimulationSnapshot.Capture().Sha256, Common.RandomSource.State, Common.RandomSource.DrawCount, Common.OrganismIdentityCounter));
    }

    private static void FounderOnly()
    {
        FixtureOrganism supplied = null;
        Board.Reset(Options(FounderRepertoire.GatherAndReproduce, 100), (x, y) =>
        {
            if (x != 0 || y != 0) return null;
            supplied = new FixtureOrganism(); supplied.SetGenes((owner, slot) => new MoveDnaElement(owner, slot)); return supplied;
        });
        Check.Equal(1, Board.BoardOrganismCount); Check.True(ReferenceEquals(supplied, Board.BoardElement[0, 0].Occupant), "Supplied occupant replaced");
        Check.True(supplied.DnaSequence.All(g => g.DnaType == DnaTypes.Move), "Supplied genome constrained");
        Common.ConfigureRandom(new ScriptedRandomSource(), true);
        var child = new Organism(supplied, supplied);
        Check.True(child.DnaSequence.All(g => g.DnaType == DnaTypes.Move), "Inherited DNA was forced into the founder preset");
        Check.Equal(supplied.Id, child.Parent1Id); Check.Equal(supplied.Id, child.Parent2Id);
    }

    private static void OptionsSurviveObservation()
    {
        static bool ContainsRepertoireField(string json)
        {
            using var document = JsonDocument.Parse(json);
            int optionsId = document.RootElement.GetProperty("options").GetProperty("reference").GetInt32();
            JsonElement optionsNode = document.RootElement.GetProperty("objects").EnumerateArray()
                .Single(node => node.GetProperty("id").GetInt32() == optionsId);
            return optionsNode.GetProperty("fields").TryGetProperty(
                typeof(SimulationRunOptions).FullName + ".<FounderRepertoire>k__BackingField", out _);
        }
        Board.Reset(Options());
        var original = SimulationSnapshot.Capture();
        Check.True(!ContainsRepertoireField(original.Json), "Compatibility default was added to old reference format");
        Board.Reset(Options(FounderRepertoire.GatherAndReproduce));
        var changed = SimulationSnapshot.Capture();
        Check.True(ContainsRepertoireField(changed.Json) && changed.Sha256 != original.Sha256, "Nondefault repertoire omitted from empty-world state");
        using var runner = new SimulationRunner(Options(FounderRepertoire.GatherAndReproduce));
        runner.Ready.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        var frame = runner.LatestFrame;
        Check.Equal(FounderRepertoire.GatherAndReproduce, frame.Options.FounderRepertoire);
        Check.True(frame.Options.RandomSource == null, "Detached options exposed mutable RNG");
        long command = runner.Reset(frame.Options);
        var timer = Stopwatch.StartNew();
        while (runner.LatestFrame.AcknowledgedCommand < command)
        { Check.True(timer.ElapsedMilliseconds < 5000, "Restart did not settle"); Thread.Sleep(2); }
        Check.Equal(FounderRepertoire.GatherAndReproduce, runner.LatestFrame.Options.FounderRepertoire);
        Check.Equal(0, runner.LatestFrame.Season);
        runner.StopAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        Check.Equal(changed.Sha256, SimulationSnapshot.Capture().Sha256, "Detached restart changed the initial state");
    }

    private static SimulationRunOptions CustomOptions() => new()
    {
        Mode = SimulationMode.DeterministicReference, CellExecution = CellExecutionMode.Serial, Seed = 702,
        InitialPopulationPercent = 27, InitialCellFood = 7, FoodRegrowthPerAge = 4, InitialOrganismFood = 2,
        FounderRepertoire = FounderRepertoire.GatherAndReproduce,
        Policy = new SimulationPolicy { Learning = LearningPolicy.BoundedExploratory, Health = HealthPolicy.Capped, Attack = AttackPolicy.ReserveTransfer }
    };

    private static void DialogRoundTrip()
    {
        var current = CustomOptions();
        using var dialog = new NewSimulationDialog(current);
        Check.Equal(1, Check.Field<ComboBox>(dialog, "FounderDna").SelectedIndex);
        Check.Field<CheckBox>(dialog, "RandomSeed").Checked = false;
        Check.Invoke(dialog, "Create_Click", dialog, EventArgs.Empty);
        var chosen = dialog.Options;
        Check.Equal(current.FounderRepertoire, chosen.FounderRepertoire); Check.Equal(current.Seed, chosen.Seed);
        Check.Equal(current.Mode, chosen.Mode); Check.Equal(current.CellExecution, chosen.CellExecution);
        Check.Equal(current.InitialPopulationPercent, chosen.InitialPopulationPercent); Check.Equal(current.InitialCellFood, chosen.InitialCellFood);
        Check.Equal(current.FoodRegrowthPerAge, chosen.FoodRegrowthPerAge); Check.Equal(current.InitialOrganismFood, chosen.InitialOrganismFood);
        Check.Equal(current.Policy.Learning, chosen.Policy.Learning); Check.Equal(current.Policy.Health, chosen.Policy.Health); Check.Equal(current.Policy.Attack, chosen.Policy.Attack);
        Check.True(MainForm.Configuration(chosen).Contains("Constrained DNA: gather + reproduce"), "Viewer configuration concealed constrained DNA");
        Check.True(MainForm.Configuration(Options()).Contains("Unrestricted random DNA"), "Default configuration label is ambiguous");
    }

    private static void DialogPresets()
    {
        foreach (int index in new[] { 3, 5, 6 })
        {
            using var dialog = new NewSimulationDialog(CustomOptions());
            Check.Field<ComboBox>(dialog, "Preset").SelectedIndex = index;
            Check.Invoke(dialog, "Create_Click", dialog, EventArgs.Empty);
            var chosen = dialog.Options;
            Check.Equal(index == 5 ? FounderRepertoire.GatherAndReproduce : FounderRepertoire.UnrestrictedRandom, chosen.FounderRepertoire);
            Check.Equal(index == 6 ? 2 : 1, chosen.FoodRegrowthPerAge);
            Check.Equal(index == 3 ? HealthPolicy.Capped : HealthPolicy.LegacyOverweight, chosen.Policy.Health);
            Check.Equal(10, chosen.InitialPopulationPercent); Check.Equal(3, chosen.InitialCellFood); Check.Equal(5, chosen.InitialOrganismFood);
            Check.Equal(LearningPolicy.RepairedLegacy, chosen.Policy.Learning); Check.Equal(AttackPolicy.DamageOnly, chosen.Policy.Attack);
        }
        using var restore = new NewSimulationDialog(CustomOptions());
        RenderingTests.ShowOffscreen(restore);
        Check.Field<ComboBox>(restore, "Preset").SelectedIndex = 6;
        Check.Field<ComboBox>(restore, "Preset").SelectedIndex = 0;
        Check.Equal(1, Check.Field<ComboBox>(restore, "FounderDna").SelectedIndex); Check.Equal(4m, Check.Field<NumericUpDown>(restore, "Regrowth").Value);
        Check.Field<Button>(restore, "DefaultsButton").PerformClick();
        Check.Equal(0, Check.Field<ComboBox>(restore, "FounderDna").SelectedIndex); Check.Equal(1m, Check.Field<NumericUpDown>(restore, "Regrowth").Value);
        Check.True(Check.Field<CheckBox>(restore, "RandomSeed").Checked, "Restore defaults removed random seeding");
    }
}
