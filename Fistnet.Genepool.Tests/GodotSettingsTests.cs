using Fistnet.Genepool.Control;
using Fistnet.Genepool.Control.Presentation;
using Fistnet.Genepool.Dna;

namespace Fistnet.Genepool.Tests;

internal static class GodotSettingsTests
{
    public static IEnumerable<TestCase> Cases()
    {
        yield return new("Godot settings: current draft retains hidden execution and policy settings", "godot-settings", PreserveCurrent);
        yield return new("Godot settings: presets reset other families and restore random choice", "godot-settings", Presets);
        yield return new("Godot settings: explicit signed seeds and random acceptance are distinct", "godot-settings", Seeds);
        yield return new("Godot settings: invalid drafts are rejected without changing original options", "godot-settings", Validation);
    }
    private static SimulationRunOptions Original() => new()
    {
        Mode = SimulationMode.DeterministicReference, CellExecution = CellExecutionMode.Serial,
        Seed = -1729, InitialPopulationPercent = 37, InitialCellFood = 7,
        InitialOrganismFood = 8, FoodRegrowthPerAge = 4, FounderRepertoire = FounderRepertoire.GatherAndReproduce,
        Policy = new SimulationPolicy { Learning = LearningPolicy.BoundedExploratory, Health = HealthPolicy.Capped,
            Attack = AttackPolicy.ReserveTransfer, ExternalContextLimit = 17, ExplorationPercent = 23,
            MaximumHealth = 91, AttackCost = 3, AttackFoodLimit = 7 }
    };
    private static void PreserveCurrent()
    {
        var source = Original(); var draft = new SimulationSettings(source) { RandomSeed = false, Density = 42 };
        var result = draft.CreateOptions(12);
        Check.Equal(42, result.InitialPopulationPercent); Check.Equal(source.Seed, result.Seed);
        Check.Equal(source.Mode, result.Mode); Check.Equal(source.CellExecution, result.CellExecution);
        Check.Equal(17, result.Policy.ExternalContextLimit); Check.Equal(23, result.Policy.ExplorationPercent);
        Check.Equal(91, result.Policy.MaximumHealth); Check.Equal(3, result.Policy.AttackCost); Check.Equal(7, result.Policy.AttackFoodLimit);
        Check.Equal(7, result.InitialCellFood); Check.Equal(8, result.InitialOrganismFood); Check.Equal(4, result.FoodRegrowthPerAge);
        Check.Equal(FounderRepertoire.GatherAndReproduce, result.FounderRepertoire);
        Check.True(result.RandomSource == null, "Mutable random source crossed the presentation boundary");
        Check.Equal(37, source.InitialPopulationPercent);
    }
    private static void Presets()
    {
        var source = Original(); var draft = new SimulationSettings(source);
        for (int preset = 1; preset <= 6; preset++)
        {
            draft.RandomSeed = false; draft.Density = 99; draft.ApplyPreset(preset);
            var result = draft.CreateOptions(234);
            Check.True(draft.RandomSeed, "Preset must restore random seed selection");
            Check.Equal(234, result.Seed); Check.Equal(10, result.InitialPopulationPercent);
            Check.Equal(3, result.InitialCellFood); Check.Equal(5, result.InitialOrganismFood);
            Check.Equal(preset == 6 ? 2 : 1, result.FoodRegrowthPerAge);
            Check.Equal(preset == 2 ? LearningPolicy.BoundedExploratory : LearningPolicy.RepairedLegacy, result.Policy.Learning);
            Check.Equal(preset == 3 ? HealthPolicy.Capped : HealthPolicy.LegacyOverweight, result.Policy.Health);
            Check.Equal(preset == 4 ? AttackPolicy.ReserveTransfer : AttackPolicy.DamageOnly, result.Policy.Attack);
            Check.Equal(preset == 5 ? FounderRepertoire.GatherAndReproduce : FounderRepertoire.UnrestrictedRandom, result.FounderRepertoire);
            Check.Equal(64, result.Policy.ExternalContextLimit); Check.Equal(10, result.Policy.ExplorationPercent);
            Check.Equal(50, result.Policy.MaximumHealth); Check.Equal(1, result.Policy.AttackCost); Check.Equal(5, result.Policy.AttackFoodLimit);
            Check.Equal(source.Mode, result.Mode); Check.Equal(source.CellExecution, result.CellExecution);
        }
        draft.ApplyPreset(0); draft.RandomSeed = false;
        Check.Equal(37, draft.CreateOptions(0).InitialPopulationPercent); Check.Equal(91, draft.CreateOptions(0).Policy.MaximumHealth);
    }
    private static void Seeds()
    {
        var draft = new SimulationSettings(Original()) { RandomSeed = false };
        foreach (int seed in new[] { int.MinValue, -1, 0, int.MaxValue })
        { draft.Seed = seed; Check.Equal(seed, draft.CreateOptions(19).Seed); }
        draft.RandomSeed = true; Check.Equal(19, draft.CreateOptions(19).Seed);
    }
    private static void Validation()
    {
        var source = Original(); var draft = new SimulationSettings(source) { Density = 101 };
        bool failed = false; try { draft.CreateOptions(2); } catch (ArgumentOutOfRangeException) { failed = true; }
        Check.True(failed, "Invalid settings were accepted"); Check.Equal(37, source.InitialPopulationPercent);
        failed = false; try { draft.ApplyPreset(7); } catch (ArgumentOutOfRangeException) { failed = true; }
        Check.True(failed, "Invalid preset was accepted");
    }
}
