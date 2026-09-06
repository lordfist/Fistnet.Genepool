using Fistnet.Genepool.Control;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Control.Rules;
using Fistnet.Genepool.Dna;
using Fistnet.Genepool.Dna.Effects;
using Fistnet.Genepool.Dna.Elements;

namespace Fistnet.Genepool.Tests;

internal static class DiagnosticsTests
{
    public static IEnumerable<TestCase> Cases()
    {
        yield return new("diagnostics: reference state and random draws unchanged when enabled", "diagnostics", PassiveReference);
        yield return new("diagnostics: actual age batches agree with eight single seasons", "diagnostics", AgeBatchEquivalence);
        yield return new("diagnostics: reports and event samples are bounded and sessions reset independently", "diagnostics", BoundsAndReset);
        yield return new("diagnostics: attempted action and applied food effects remain distinct", "diagnostics", ActionsAndFood);
        yield return new("diagnostics: mixed, high-health and unknown deaths retain observed causes", "diagnostics", DeathAttribution);
        yield return new("diagnostics: actual child construction, placement and movement are counted", "diagnostics", BirthAndMovement);
        yield return new("diagnostics: pending food debits remain visible across movement and death", "diagnostics", PendingFood);
        yield return new("diagnostics: controlled history load preserves population under production scheduling", "diagnostics", WorkloadFixture);
    }

    private static (string Hash, long Draws, DiagnosticReport Report) Reference(bool observe, bool batch)
    {
        SimulationDiagnostics.Detach();
        Board.Reset(new SimulationRunOptions { Mode = SimulationMode.DeterministicReference, Seed = 11, InitialPopulationPercent = 10 });
        var session = observe ? new DiagnosticSession(32, 8) : null;
        if (observe) SimulationDiagnostics.Attach(session);
        try
        {
            for (int i = 0; i < (batch ? 2 : 16); i++)
                if (batch) Board.ExecuteOneAge(); else Board.ExecuteSingleSeason(true);
            ScenarioRunner.ValidateWorld();
            return (SimulationSnapshot.Capture().Sha256, Common.RandomSource.DrawCount, session?.Export());
        }
        finally { SimulationDiagnostics.Detach(); }
    }

    private static void PassiveReference()
    {
        var plain = Reference(false, false);
        var observed = Reference(true, false);
        Check.Equal(plain.Hash, observed.Hash, "Diagnostics changed complete reference state");
        Check.Equal(plain.Draws, observed.Draws, "Diagnostics consumed randomness");
        Check.True(observed.Report.Totals.GetValueOrDefault("actions.attempted.total") > 0, "Actions were not observed");
        Check.Equal(16L, observed.Report.TimingSamples.GetValueOrDefault("engine.season"));
        Check.Equal(8, observed.Report.Seasons.Length);
        Check.Equal(8L, observed.Report.SeasonReportsOmitted);
    }

    private static void AgeBatchEquivalence()
    {
        var singles = Reference(true, false);
        var ages = Reference(true, true);
        Check.Equal(singles.Hash, ages.Hash, "Batching changed full final state");
        Check.Equal(singles.Draws, ages.Draws, "Batching changed RNG draws");
        Check.Equal(2L, ages.Report.TimingSamples.GetValueOrDefault("statistics.full"));
        Check.Equal(14L, ages.Report.TimingSamples.GetValueOrDefault("statistics.population_only"));
    }

    private static void BoundsAndReset()
    {
        var session = new DiagnosticSession(maxEventSamples: 8, maxSeasonReports: 2);
        var organism = new Organism();
        for (int season = 1; season <= 5; season++)
        {
            session.BeginSeason(season);
            session.Count("fixture.count", season);
            session.Timing("fixture.ticks", season * 10);
            foreach (string kind in new[] { "death.fixture", "effect.fixture", "action.fixture", "fixture.event" })
                session.Event(kind, organism, 1, 2);
            session.CompleteSeason(season, 1);
        }
        var first = session.Export();
        Check.Equal(15L, first.Totals["fixture.count"]);
        Check.Equal(150L, first.TimingsTicks["fixture.ticks"]);
        Check.Equal(5L, first.TimingSamples["fixture.ticks"]);
        Check.Equal(8, first.Events.Length); Check.Equal(12L, first.EventSamplesOmitted);
        Check.Equal(2, first.Seasons.Length); Check.Equal(3L, first.SeasonReportsOmitted);
        Check.Equal(4, first.Seasons[0].Season); Check.Equal(5, first.Seasons[1].Season);
        session.Count("fixture.count", 1);
        Check.Equal(15L, first.Totals["fixture.count"], "Export aggregate remained live");
        SimulationDiagnostics.Attach(session);
        try
        {
            bool collision = false;
            try { SimulationDiagnostics.Attach(new DiagnosticSession()); }
            catch (InvalidOperationException) { collision = true; }
            Check.True(collision, "An active diagnostics session was silently replaced");
        }
        finally { Check.True(ReferenceEquals(session, SimulationDiagnostics.Detach()), "Detach returned wrong session"); }
        var fresh = new DiagnosticSession(0, 0);
        Check.Equal(0, fresh.Export().Totals.Count, "New session retained prior counts");
        fresh.BeginSeason(1); fresh.Event("discarded", organism, 1, 2); fresh.CompleteSeason(1, 1);
        Check.Equal(0, fresh.Export().Events.Length); Check.Equal(1L, fresh.Export().EventSamplesOmitted);
        Check.Equal(0, fresh.Export().Seasons.Length); Check.Equal(1L, fresh.Export().SeasonReportsOmitted);
    }

    private static void EmptyReference()
    {
        SimulationDiagnostics.Detach();
        Board.Reset(new SimulationRunOptions { Mode = SimulationMode.DeterministicReference, Seed = 29, InitialPopulationPercent = 0 }, (_, _) => null);
    }

    private static DiagnosticReport Observe(Action action)
    {
        var session = new DiagnosticSession();
        SimulationDiagnostics.Attach(session);
        session.BeginSeason(1);
        try { action(); session.CompleteSeason(1, Board.BoardElement.Cast<BoardSquare>().Count(s => s.IsOccupied)); return session.Export(); }
        finally { SimulationDiagnostics.Detach(); }
    }

    private static void ActionsAndFood()
    {
        EmptyReference();
        var organism = new FixtureOrganism();
        organism.SetGenes((owner, index) => new EatDnaElement(owner, index));
        Board.BoardElement[5, 5].AddOccupant(organism);
        var report = Observe(() =>
        {
            organism.Activate();
            organism.ExecuteNextDnaSequence(null); // Eat is attempted but its precondition rejects null.
            organism.ExecuteEffectStack();
            organism.SetAvailableFood(3);
            organism.AddStackedEffect(new ChangeFoodEffect(organism, 2, 0));
            organism.AddStackedEffect(new ChangeFoodEffect(organism, 2, 1));
            organism.ExecuteEffectStack();
            Check.Equal((sbyte)8, organism.FoodBalance);
            Check.Equal((byte)1, organism.TakenFood, "Characterized second uptake overwrites deferred debit");
            new ExecuteOgranismSetup().Execute(Board.BoardElement[5, 5]);
            Check.Equal((byte)2, Board.BoardElement[5, 5].FoodRemaining, "Characterized delayed debit changed");
        });
        Check.Equal(1L, report.Totals["actions.attempted.Eat"]);
        Check.Equal(1L, report.Totals["actions.no_effect_queued.Eat"]);
        Check.Equal(2L, report.Totals["effects.applied.FoodChange"]);
        Check.Equal(4L, report.Totals["food.requested_uptake"]);
        Check.Equal(3L, report.Totals["food.actual_uptake"]);
        Check.Equal(1L, report.Totals["food.taken_overwrite"]);
        Check.Equal(1L, report.Totals["food.debit_requested"]);
        Check.Equal(1L, report.Totals["food.debit_applied"]);
    }

    private static void DeathAttribution()
    {
        EmptyReference();
        var mixed = new FixtureOrganism(); mixed.SetState(health: 10, food: 0);
        var high = new FixtureOrganism(); high.SetState(health: 49, food: 5);
        var unknown = new FixtureOrganism(); unknown.SetState(health: 0, food: 5);
        Board.BoardElement[1, 1].AddOccupant(mixed);
        Board.BoardElement[2, 2].AddOccupant(high);
        Board.BoardElement[3, 3].AddOccupant(unknown);
        var report = Observe(() =>
        {
            mixed.AddStackedEffect(new ChangeHealthEffect(mixed, -9, 0));
            mixed.AddStackedEffect(new ChangeFoodEffect(mixed, -2, 0));
            mixed.ExecuteEffectStack();
            Check.Equal((sbyte)-1, mixed.Health);
            high.AddStackedEffect(new ChangeHealthEffect(high, 1, 0));
            high.ExecuteEffectStack();
            Check.Equal((sbyte)50, high.Health);
            var rule = new ExecuteOrganismDeadRule();
            rule.Execute(Board.BoardElement[1, 1]); rule.Execute(Board.BoardElement[2, 2]); rule.Execute(Board.BoardElement[3, 3]);
            rule.Execute(Board.BoardElement[1, 1]); // Already removed: never count it twice.
        });
        Check.Equal(3L, report.Totals["deaths.total"]);
        Check.Equal(2L, report.Totals["deaths.threshold.nonpositive_health"]);
        Check.Equal(1L, report.Totals["deaths.threshold.high_health"]);
        Check.Equal(1L, report.Totals["deaths.attribution.mixed"]);
        Check.Equal(1L, report.Totals["deaths.attribution.unknown"]);
        Check.Equal(1L, report.Totals["deaths.contributor.Damage"]);
        Check.Equal(1L, report.Totals["deaths.contributor.Starvation"]);
        Check.Equal(1L, report.Totals["deaths.contributor.Healing"]);
        Check.True(!Board.BoardElement[1, 1].IsOccupied && !Board.BoardElement[2, 2].IsOccupied && !Board.BoardElement[3, 3].IsOccupied,
            "Death observation prevented actual removal");
        var mixedDeath = report.Events.Single(e => e.Kind == "death.removed" && e.X == 1 && e.Y == 1);
        Check.Equal(3L, mixedDeath.ObservedContributorCount);
        Check.Equal(1L, mixedDeath.ContributorSamplesOmitted);
        Check.Equal("health.effect", mixedDeath.FirstContributor.Value.Mechanism);
        Check.Equal("starvation", mixedDeath.LastContributor.Value.Mechanism);
        Check.Equal(10, mixedDeath.FirstContributor.Value.Before.Health);
        Check.Equal(-1, mixedDeath.LastContributor.Value.After.Health);
        Check.Equal(0L, mixedDeath.FirstContributor.Value.ActorId, "Unscoped manual effect acquired a fabricated actor");
        DeathProvenanceAfterSampleSaturation();
    }

    private static void DeathProvenanceAfterSampleSaturation()
    {
        EmptyReference();
        var attacker = new FixtureOrganism();
        attacker.SetGenes((owner, index) => new KillDnaElement(owner, index));
        var victim = new Organism();
        Board.BoardElement[4, 4].AddOccupant(attacker);
        Board.BoardElement[5, 5].AddOccupant(victim);
        var session = new DiagnosticSession(8, 1);
        SimulationDiagnostics.Attach(session);
        session.BeginSeason(1);
        try
        {
            session.Event("effect.fixture", null, 0, 0);
            session.Event("effect.fixture", null, 0, 0); // Fill the ordinary effect sample pool first.
            attacker.Activate();
            attacker.ExecuteNextDnaSequence(victim);
            victim.ExecuteEffectStack();
            new ExecuteOrganismDeadRule().Execute(Board.BoardElement[5, 5]);
            session.CompleteSeason(1, 1);
            var report = session.Export();
            Check.True(!report.Events.Any(e => e.Kind == "effect.applied"), "Fixture failed to fill effect sample pool");
            var death = report.Events.Single(e => e.Kind == "death.removed");
            Check.Equal(session.OrganismId(victim), death.OrganismId);
            Check.Equal(session.OrganismId(attacker), death.FirstContributor.Value.ActorId);
            Check.True(death.FirstContributor.Value.ActionId > 0, "Known actual action origin was lost");
            Check.Equal("Kill", death.FirstContributor.Value.Action);
            Check.Equal(10, death.FirstContributor.Value.Before.Health);
            Check.Equal(-1, death.FirstContributor.Value.After.Health);
            Check.Equal(0L, death.ActorId, "Death record attributed an exclusive killer");
            Check.True(report.EventSamplesOmitted > 0, "Missing sample omission accounting");
        }
        finally { SimulationDiagnostics.Detach(); }
    }

    private static void BirthAndMovement()
    {
        EmptyReference();
        var parent = new Organism(); var other = new Organism();
        Board.BoardElement[10, 10].AddOccupant(parent);
        var report = Observe(() =>
        {
            parent.AddStackedEffect(new BirthEffect(parent, other, 0));
            parent.ExecuteEffectStack();
            Organism child = parent.Child;
            Check.True(child != null, "No actual child was constructed");
            new ExecuteOrganismEffectsRule().Execute(Board.BoardElement[10, 10]);
            Check.True(ReferenceEquals(child, Board.BoardElement[9, 9].Occupant), "Child placement changed");
            Check.True(!parent.HasChild, "Placed child remained pending");
            Check.True(Board.BoardElement[10, 10].TryMove(TargetTypes.MiddleRight), "Move to empty neighbor failed");
            Check.True(!Board.BoardElement[11, 10].TryMove(TargetTypes.Self), "Occupied target accepted");
            Check.True(ReferenceEquals(parent, Board.BoardElement[11, 10].Occupant), "Move lost parent identity");
        });
        Check.Equal(1L, report.Totals["births.created"]);
        Check.Equal(1L, report.Totals["birth.placement_attempted"]);
        Check.Equal(1L, report.Totals["birth.placed"]);
        Check.Equal(1L, report.Totals["birth.placed.TopLeft"]);
        Check.Equal(1L, report.Totals["effects.applied.Birth"]);
        Check.Equal(2L, report.Totals["movement.attempted"]);
        Check.Equal(1L, report.Totals["movement.committed"]);
        Check.Equal(1L, report.Totals["movement.committed.MiddleRight"]);
        Check.Equal(1L, report.Totals["movement.blocked.occupied"]);
        Check.True(report.Events.Any(e => e.Kind == "birth.created"), "Missing sampled construction event");
    }

    private static void WorkloadFixture()
    {
        SimulationDiagnostics.Detach();
        DiagnosticScenarios.SetupFixture(10, 32);
        var organisms = Board.BoardElement.Cast<BoardSquare>().Where(s => s.IsOccupied).Select(s => s.Occupant).ToArray();
        Check.Equal(1000, organisms.Length);
        Check.True(organisms.All(o => o.Brain.LearningContextCount == 34 && o.Brain.LearningEntryCount == 272), "Wrong declared history load");
        var session = new DiagnosticSession(0, 2);
        SimulationDiagnostics.Attach(session);
        try
        {
            Board.ExecuteOneAge(); Board.ExecuteOneAge();
            ScenarioRunner.ValidateWorld();
            Check.Equal(1000, Board.BoardOrganismCount, "Controlled population changed");
            Check.True(organisms.All(o => o.Health == 10 && !o.IsDead), "No-op workload altered health");
            Check.Equal(0L, session.Export().Totals.GetValueOrDefault("deaths.total"));
            Check.Equal(0L, session.Export().Totals.GetValueOrDefault("births.created"));
        }
        finally
        {
            SimulationDiagnostics.Detach();
            Board.Reset(new SimulationRunOptions { Seed = 29, InitialPopulationPercent = 0 });
        }
    }

    private static void PendingFood()
    {
        EmptyReference();
        var moving = new Organism(); var dying = new Organism();
        Board.BoardElement[20, 20].AddOccupant(moving);
        Board.BoardElement[30, 30].AddOccupant(dying);
        var report = Observe(() =>
        {
            foreach (var organism in new[] { moving, dying })
            {
                organism.SetAvailableFood(3);
                organism.AddStackedEffect(new ChangeFoodEffect(organism, 2, 0));
                organism.ExecuteEffectStack();
                Check.Equal((byte)2, organism.TakenFood);
            }
            Check.True(Board.BoardElement[20, 20].TryMove(TargetTypes.MiddleRight), "Fixture move failed");
            new ExecuteOgranismSetup().Execute(Board.BoardElement[21, 20]);
            Check.Equal((byte)3, Board.BoardElement[20, 20].FoodRemaining, "Current deferred debit unexpectedly reached original cell");
            Check.Equal((byte)1, Board.BoardElement[21, 20].FoodRemaining, "Current deferred debit did not follow occupant");
            dying.AddStackedEffect(new ChangeHealthEffect(dying, -11, 0));
            dying.ExecuteEffectStack();
            new ExecuteOrganismDeadRule().Execute(Board.BoardElement[30, 30]);
            Check.True(!Board.BoardElement[30, 30].IsOccupied, "Dead occupant remained");
            Check.Equal((byte)10, Board.BoardElement[30, 30].FoodRemaining, "Current death return behavior changed");
        });
        Check.Equal(4L, report.Totals["food.actual_uptake"]);
        Check.Equal(2L, report.Totals["food.debit_applied"]);
        Check.Equal(2L, report.Totals["food.pending_debit_at_death"]);
        Check.Equal(7L, report.Totals["food.death_return_requested"]);
        Check.Equal(7L, report.Totals["food.death_return_applied"]);
        Check.Equal(1L, report.Totals["movement.committed"]);
        Check.Equal(1L, report.Totals["deaths.total"]);
    }
}
