using Fistnet.Genepool.Control;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Dna;
using Fistnet.Genepool.Dna.Elements;

namespace Fistnet.Genepool.Tests;

internal static class SeasonObservationTests
{
    public static IEnumerable<TestCase> Cases()
    {
        yield return new("season observations: all actors are retained beyond the legacy marker limit", "season-observation", WholeCohort);
        yield return new("season observations: published batches are immutable and survive selection acknowledgments", "season-observation", DetachedBatches);
        yield return new("season observations: new seasons and reset cannot expose stale or partial batches", "season-observation", BoundaryAndReset);
        yield return new("season observations: moves, blocked attempts and fallback destinations report actual outcomes", "season-observation", MovementOutcomes);
        yield return new("season observations: mutations retain the performed action and detached result", "season-observation", MutationOutcome);
        yield return new("season observations: waits and deaths are retained with unknown resource deltas labeled", "season-observation", WaitAndDeath);
        yield return new("season observations: newborns enter the observation cohort next season", "season-observation", NewbornBoundary);
        yield return new("season observations: birth location and child identity are distinct from the chosen partner", "season-observation", PartnerBirthLocation);
        yield return new("season observations: observing every actor leaves deterministic state and RNG unchanged", "season-observation", PassiveObservation);
    }

    private static void Reset() => Board.Reset(new SimulationRunOptions
        { Mode = SimulationMode.DeterministicReference, Seed = 79, InitialPopulationPercent = 0 });

    private static ViewFrame Frame(ViewCollector collector, long acknowledged = 0) =>
        collector.Capture(1, acknowledged, "Paused", false, null, 0, 0, 20);

    private static FixtureOrganism Actor(DnaTypes type, TargetTypes target = TargetTypes.Self)
    {
        var actor = new FixtureOrganism(); actor.SetState(age: 1);
        actor.SetGenes((me, slot) =>
        {
            IDnaElement gene = type switch
            {
                DnaTypes.GenerateFood => new CreateFoodDnaElement(me, slot),
                DnaTypes.Evolve => new EvolveDnaElement(me, slot),
                DnaTypes.CombineDna => new CombineDnaElement(me, slot),
                _ => new MoveDnaElement(me, slot)
            };
            if (type != DnaTypes.Evolve) Check.Property(gene, "Target", target);
            if (type == DnaTypes.Move) Check.Property(gene, "DnaCode", 0);
            return gene;
        });
        return actor;
    }

    private static void WholeCohort()
    {
        Reset();
        var identities = new HashSet<long>();
        for (int y = 10; y < 19; y++)
            for (int x = 10; x < 19; x++)
            {
                var actor = Actor(DnaTypes.GenerateFood);
                Board.BoardElement[x, y].AddOccupant(actor); identities.Add(actor.Id);
            }
        using var collector = new ViewCollector();
        collector.BeginSeason(); Board.ExecuteSingleSeason(false);
        var frame = Frame(collector);
        Check.Equal(81, frame.SeasonActions.Count, "The feed dropped actors after the old 64-marker cutoff");
        Check.True(identities.SetEquals(frame.SeasonActions.Select(a => a.OrganismId)), "Observed cohort changed identities");
        Check.Equal(81, frame.SeasonActions.Select(a => a.OrganismId).Distinct().Count(), "One actor was counted twice");
        Check.True(frame.SeasonActions.All(a => a.Action.Season == frame.Season && a.Action.FoodGathered == 3),
            "The completed cohort lost actual transfers or mixed seasons");
        Check.Equal(81L, frame.ActionCounts.Single(a => a.Name == "Gather local food").Count);
        Check.True(frame.Selected == null, "Whole-board observation selected an organism");
    }

    private static void DetachedBatches()
    {
        Reset(); var actor = Actor(DnaTypes.Move, TargetTypes.MiddleRight);
        Board.BoardElement[10, 10].AddOccupant(actor);
        using var collector = new ViewCollector();
        collector.BeginSeason(); Board.ExecuteSingleSeason(false);
        var first = Frame(collector); var firstAction = first.SeasonActions.Single();
        bool refused = false;
        try { ((IList<ViewObservedAction>)first.SeasonActions)[0] = firstAction with { OrganismId = -1 }; }
        catch (NotSupportedException) { refused = true; }
        Check.True(refused, "A published batch exposed writable storage");
        collector.Select(actor.Id);
        var selected = Frame(collector, 1);
        Check.True(first.SeasonActions.SequenceEqual(selected.SeasonActions), "Selection erased the completed batch");
        Check.True(!ReferenceEquals(first.SeasonActions, selected.SeasonActions), "Capture reused the collection instead of a fresh copy");
        collector.Select(null);
        Check.True(first.SeasonActions.SequenceEqual(Frame(collector, 2).SeasonActions), "Clearing selection erased observations");
        collector.BeginSeason(); Board.ExecuteSingleSeason(false);
        var second = Frame(collector);
        Check.Equal(1, firstAction.Action.Season); Check.Equal(11, firstAction.Action.DestinationX);
        Check.Equal(2, second.SeasonActions.Single().Action.Season); Check.Equal(12, second.SeasonActions.Single().Action.DestinationX);
        Check.Equal(firstAction, first.SeasonActions.Single(), "A later season changed a retained observation");
    }

    private static void BoundaryAndReset()
    {
        Reset(); var actor = Actor(DnaTypes.Move);
        Board.BoardElement[10, 10].AddOccupant(actor);
        using var collector = new ViewCollector();
        Check.Equal(0, Frame(collector).SeasonActions.Count);
        bool checkedPartial = false;
        void DuringCompletion(Organism _, ActionDecision __, bool ___, int ____, int _____)
        {
            Check.Equal(0, Frame(collector).SeasonActions.Count, "A partially completed cohort escaped before the season advanced");
            checkedPartial = true;
        }
        Board.SeasonActorCompleted += DuringCompletion;
        try { collector.BeginSeason(); Board.ExecuteSingleSeason(false); }
        finally { Board.SeasonActorCompleted -= DuringCompletion; }
        Check.True(checkedPartial, "Fixture never inspected the actor-completion boundary");
        var completed = Frame(collector); Check.Equal(1, completed.SeasonActions.Count);
        collector.BeginSeason(); Check.Equal(0, Frame(collector).SeasonActions.Count, "New-season capture returned the prior batch");
        Board.BoardElement[10, 10].RemoveOccupant(); Board.ExecuteSingleSeason(false);
        Check.Equal(0, Frame(collector).SeasonActions.Count, "Empty completed season retained an old actor");
        collector.Reset(); Reset();
        Check.Equal(0, Frame(collector).SeasonActions.Count, "Reset retained a previous run's action");
        Check.Equal(1, completed.SeasonActions.Count, "Reset changed a published frame");
    }

    private static void MovementOutcomes()
    {
        Reset();
        var mover = Actor(DnaTypes.Move, TargetTypes.MiddleRight);
        var blocked = Actor(DnaTypes.Move, TargetTypes.MiddleRight);
        var blocker = Actor(DnaTypes.Move);
        var fallback = Actor(DnaTypes.Move, TargetTypes.TopLeft);
        Board.BoardElement[10, 10].AddOccupant(mover);
        Board.BoardElement[20, 20].AddOccupant(blocked); Board.BoardElement[21, 20].AddOccupant(blocker);
        Board.BoardElement[0, 0].AddOccupant(fallback);
        using var collector = new ViewCollector(); collector.Select(mover.Id); Frame(collector);
        collector.BeginSeason(); Board.ExecuteSingleSeason(false);
        var frame = Frame(collector);
        var move = frame.SeasonActions.Single(a => a.OrganismId == mover.Id).Action;
        var failure = frame.SeasonActions.Single(a => a.OrganismId == blocked.Id).Action;
        var reflected = frame.SeasonActions.Single(a => a.OrganismId == fallback.Id).Action;
        Check.Equal((10, 10, 11, 10, 11, 10), (move.SourceX, move.SourceY, move.DestinationX, move.DestinationY, move.TargetX, move.TargetY));
        Check.True(move.Moved, "Actual move was hidden"); Check.Equal(move, frame.Selected.Trace.Single());
        Check.True(!failure.Moved, "Blocked attempt appeared successful"); Check.Equal("movement.blocked", failure.Result);
        Check.Equal((20, 20, 21, 20), (failure.DestinationX, failure.DestinationY, failure.TargetX, failure.TargetY));
        Check.True(reflected.Moved, "Boundary fallback lost its actual movement");
        Check.Equal((0, 0, 1, 1, -1, -1), (reflected.SourceX, reflected.SourceY, reflected.DestinationX, reflected.DestinationY,
            reflected.TargetX, reflected.TargetY));
        Check.Equal("TopLeft", reflected.Target, "Fallback replaced the chosen direction with an invented choice");
    }

    private static void MutationOutcome()
    {
        Reset(); var actor = Actor(DnaTypes.Evolve); Board.BoardElement[10, 10].AddOccupant(actor);
        using var collector = new ViewCollector();
        collector.BeginSeason(); Board.ExecuteSingleSeason(false);
        var action = Frame(collector).SeasonActions.Single().Action;
        Check.Equal("Mutate", action.Action); Check.True(actor.LastOutcome.Mutations > 0, "Fixture produced no mutations");
        Check.Equal(actor.LastOutcome.Mutations, action.Mutations, "Actual changed-gene count was lost");
        Check.Equal((10, 10), (action.TargetX, action.TargetY));
        int detachedCount = action.Mutations;
        actor.LastOutcome.Mutations = 99; // Only the test's mutable outcome changes.
        Check.Equal(detachedCount, action.Mutations, "Frame retained the live outcome object");
    }

    private static void WaitAndDeath()
    {
        Reset(); var actor = Actor(DnaTypes.Move); actor.SetState(age: Organism.MAX_AGE);
        Board.BoardElement[10, 10].AddOccupant(actor);
        using var collector = new ViewCollector(); collector.BeginSeason(); Board.ExecuteSingleSeason(false);
        var frame = Frame(collector); var action = frame.SeasonActions.Single().Action;
        Check.Equal("Wait", action.Action); Check.Equal(-1, action.Slot);
        Check.True(action.Result.Contains("died"), "Removed no-choice actor disappeared from its completed cohort");
        Check.True(!action.ResourceChangesObserved, "Unknown before-state was presented as an observed zero change");
        Check.Equal((0, 0, 0), (action.FoodChange, action.HealthChange, action.Mutations));
        Check.Equal((-1, -1, 0L), (action.BirthX, action.BirthY, action.BirthOrganismId));
        Check.True(!action.Moved && !action.BirthPlaced, "Wait gained an invented effect");
        Check.Equal(0, frame.Population); Check.Equal(1L, frame.Deaths);
    }

    private static void NewbornBoundary()
    {
        Reset(); var parent = Actor(DnaTypes.CombineDna); Board.BoardElement[10, 10].AddOccupant(parent);
        using var collector = new ViewCollector(); collector.BeginSeason(); Board.ExecuteSingleSeason(false);
        var birthFrame = Frame(collector);
        Check.Equal(2, birthFrame.Population); Check.Equal(1, birthFrame.SeasonActions.Count);
        Check.Equal(parent.Id, birthFrame.SeasonActions.Single().OrganismId);
        var birth = birthFrame.SeasonActions.Single().Action;
        Check.True(birth.BirthPlaced, "Successful birth outcome missing");
        var childCell = birthFrame.Cells.Single(c => c.OrganismId.HasValue && c.OrganismId != parent.Id);
        long childId = childCell.OrganismId.Value;
        Check.Equal((childCell.X, childCell.Y, childId), (birth.BirthX, birth.BirthY, birth.BirthOrganismId));
        Check.Equal((10, 10), (birth.TargetX, birth.TargetY), "Self reproduction changed its actual partner locator");
        Check.True((birth.BirthX, birth.BirthY) != (birth.TargetX, birth.TargetY), "Birth was placed on its parent");
        collector.BeginSeason(); Board.ExecuteSingleSeason(false);
        Check.True(Frame(collector).SeasonActions.Any(a => a.OrganismId == childId), "Newborn failed to enter the next cohort");
        Check.Equal(childId, birth.BirthOrganismId, "Later season changed retained child identity");
    }

    private static void PartnerBirthLocation()
    {
        Reset(); var parent = Actor(DnaTypes.CombineDna, TargetTypes.MiddleRight); var mate = Actor(DnaTypes.Move);
        Board.BoardElement[10, 10].AddOccupant(parent); Board.BoardElement[11, 10].AddOccupant(mate);
        using var collector = new ViewCollector(); collector.BeginSeason(); Board.ExecuteSingleSeason(false);
        var frame = Frame(collector);
        var birth = frame.SeasonActions.Single(a => a.OrganismId == parent.Id).Action;
        var partner = frame.SeasonActions.Single(a => a.OrganismId == mate.Id).Action;
        var child = frame.Cells.Single(c => c.OrganismId.HasValue && c.OrganismId != parent.Id && c.OrganismId != mate.Id);
        Check.True(birth.BirthPlaced, "Fixture did not reproduce with its partner");
        Check.Equal((11, 10), (birth.TargetX, birth.TargetY), "Chosen mate locator was replaced by child position");
        Check.Equal((child.X, child.Y, child.OrganismId.Value), (birth.BirthX, birth.BirthY, birth.BirthOrganismId));
        Check.True((birth.BirthX, birth.BirthY) != (birth.TargetX, birth.TargetY), "Child cue was placed on the occupied partner cell");
        Check.True(!partner.BirthPlaced, "Second parent acquired the initiator's action");
        Check.Equal((-1, -1, 0L), (partner.BirthX, partner.BirthY, partner.BirthOrganismId));
        collector.Select(mate.Id); Check.Equal(birth, Frame(collector).SeasonActions.Single(a => a.OrganismId == parent.Id).Action);
        collector.Reset(); Reset(); Check.Equal(child.OrganismId.Value, birth.BirthOrganismId, "Reset mutated a detached birth observation");
    }

    private static void PassiveObservation()
    {
        (string Hash, string Random, long Draws) Run(bool observe)
        {
            Board.Reset(new SimulationRunOptions
                { Mode = SimulationMode.DeterministicReference, Seed = 29, InitialPopulationPercent = 2 });
            using var collector = observe ? new ViewCollector() : null;
            for (int i = 0; i < 12; i++)
            {
                collector?.BeginSeason(); Board.ExecuteSingleSeason(false);
                if (collector != null)
                {
                    var frame = Frame(collector);
                    Check.True(frame.SeasonActions.Count > 64, "Passivity fixture did not exercise a whole populated cohort");
                    Check.True(frame.SeasonActions.All(a => a.Action.Season == frame.Season), "Feed mixed completion seasons");
                    collector.Select(i % 2 == 0 ? frame.SeasonActions[0].OrganismId : null);
                    Frame(collector, i + 1);
                }
            }
            return (SimulationSnapshot.Capture().Sha256, Common.RandomSource.State, Common.RandomSource.DrawCount);
        }
        Check.Equal(Run(false), Run(true), "Whole-cohort observation changed deterministic state or random draws");
    }
}
