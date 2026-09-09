using System;
using System.Collections.Generic;

namespace Fistnet.Genepool.Control
{
    // All values crossing the worker/UI boundary are detached. Collections are
    // read-only wrappers over private fresh arrays, never the mutable game board.
    public readonly record struct ViewCell(int X, int Y, int Food, long? OrganismId, int ColorArgb, string PatternKey);
    public sealed record ViewCount(string Name, long Count);
    public sealed record ViewPattern(string Key, string Description, int Count);
    public sealed record ViewGene(int Slot, string Name, string Target, int Code);
    public sealed record ViewMarker(int X, int Y, string Kind);
    public sealed record ViewHistory(int Season, int Population, long LocalFood, long StoredFood, long Births, long Deaths, long ActionCount);
    public sealed record ViewAction(int Season, int Slot, string Action, string Target, string Result,
        int FoodChange, int HealthChange, float Reward, int SourceX, int SourceY, int DestinationX, int DestinationY, string Policy, string ChoiceLabel,
        int FoodGathered = 0, int FoodSpent = 0, int FoodTransferred = 0, int Damage = 0, int Healing = 0,
        bool BirthPlaced = false, bool Moved = false, int TargetX = -1, int TargetY = -1,
        int Mutations = 0, bool ResourceChangesObserved = true,
        int BirthX = -1, int BirthY = -1, long BirthOrganismId = 0);
    public sealed record ViewObservedAction(long OrganismId, ViewAction Action);
    public sealed record ViewOrganism(long Id, long Parent1Id, long Parent2Id, int Generation, int X, int Y,
        bool IsAlive, int Health, int Food, int LocalFood, int Age, int SequenceAge, long LifetimeSeasons,
        int ChildrenObserved, string Pattern, IReadOnlyList<ViewGene> Genes, IReadOnlyList<ViewAction> Trace);
    public sealed record ViewFrame(long RunId, long AcknowledgedCommand, int Season, int Age, int Width, int Height,
        string Status, bool IsRunning, string Fault, double ActualSeasonsPerSecond, double LastSeasonMilliseconds,
        double? TargetSeasonsPerSecond, int Population, long LocalFood, long StoredFood, long Births, long Deaths,
        IReadOnlyList<ViewCell> Cells, IReadOnlyList<ViewPattern> Patterns, IReadOnlyList<ViewCount> GeneCounts,
        IReadOnlyList<ViewCount> ActionCounts, IReadOnlyList<ViewHistory> History, IReadOnlyList<ViewMarker> Markers,
        ViewOrganism Selected, SimulationRunOptions Options)
    {
        // One complete season's cohort, including failed attempts and waits. The
        // runner may skip displaying seasons; a frame never joins several batches.
        public IReadOnlyList<ViewObservedAction> SeasonActions { get; init; } = Array.Empty<ViewObservedAction>();
    }
}
