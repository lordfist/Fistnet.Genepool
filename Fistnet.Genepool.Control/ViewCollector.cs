using System;
using System.Collections.Generic;
using System.Linq;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Dna;

namespace Fistnet.Genepool.Control
{
    // Runs exclusively on the board owner thread. It keeps aggregate counters,
    // sampled chart points, one selected organism's last sixteen actions, and a
    // single completed season's observations. No observation changes the board.
    internal sealed class ViewCollector : IDisposable
    {
        private readonly long[] actions = new long[8];
        private readonly Queue<ViewAction> trace = new();
        private readonly Queue<ViewHistory> history = new();
        private readonly List<ViewMarker> markers = new(64);
        private readonly List<ViewObservedAction> seasonActions = new();
        private readonly Dictionary<long, (long ChildId, int X, int Y)> observedBirths = new();
        private int observedSeason = -1;
        private long births, deaths, actionCount;
        private long? selectedId;
        private ViewOrganism selected;
        private int childrenObserved;
        internal ViewCollector()
        {
            Board.SeasonActorCompleted += ActorCompleted;
            Board.BirthCompleted += Born;
        }
        public void Dispose()
        {
            Board.SeasonActorCompleted -= ActorCompleted;
            Board.BirthCompleted -= Born;
        }
        internal void Reset()
        {
            Array.Clear(actions); trace.Clear(); history.Clear(); markers.Clear(); seasonActions.Clear();
            observedBirths.Clear();
            observedSeason = -1;
            births = deaths = actionCount = 0; selectedId = null; selected = null; childrenObserved = 0;
        }
        internal void Select(long? id)
        {
            if (selectedId == id) return;
            selectedId = id; selected = null; childrenObserved = 0; trace.Clear();
        }
        internal void BeginSeason()
        {
            markers.Clear(); seasonActions.Clear(); observedBirths.Clear();
            observedSeason = Board.Season + 1;
        }
        private void EnsureObservationSeason()
        {
            if (observedSeason == Board.Season + 1) return;
            seasonActions.Clear(); observedBirths.Clear(); observedSeason = Board.Season + 1;
        }
        private void Marker(int x, int y, string kind)
        { if (markers.Count < 64) markers.Add(new ViewMarker(x, y, kind)); }
        private void Born(int x, int y)
        {
            EnsureObservationSeason();
            births++; Marker(x, y, "birth");
            Organism child = Board.BoardElement[x, y].Occupant;
            // CommitBirth identifies its acting parent as Parent1Id. Capture
            // placement now; later occupancy or the mate's cell is not evidence.
            if (child != null) observedBirths[child.Parent1Id] = (child.Id, x, y);
            // Either parent counts; self-parenting is one observed child, not two.
            if (child != null && (child.Parent1Id == selectedId || child.Parent2Id == selectedId)) childrenObserved++;
        }
        private void ActorCompleted(Organism actor, ActionDecision decision, bool present, int x, int y)
        {
            EnsureObservationSeason();
            if (!present) { deaths++; Marker(x, y, "death"); }
            if (decision?.Outcome.Committed == true)
            { actions[(byte)decision.Candidate.Identity.Type]++; actionCount++; }
            ActionOutcome outcome = decision?.Outcome;
            int destinationX = outcome?.Moved == true ? outcome.DestinationX : x;
            int destinationY = outcome?.Moved == true ? outcome.DestinationY : y;
            var before = decision?.Candidate.ActorBefore;
            bool isSelected = actor.Id == selectedId;
            // A no-choice season has no candidate snapshot. A selected prior
            // state is the existing trace fallback; unobserved deltas stay labeled.
            ViewOrganism previous = isSelected ? selected : null;
            bool birthLocated = outcome?.BirthPlaced == true && observedBirths.ContainsKey(actor.Id);
            var birth = birthLocated ? observedBirths[actor.Id] : (ChildId: 0L, X: -1, Y: -1);
            var action = new ViewAction(Board.Season + 1, decision == null ? -1 : decision.Candidate.Identity.Slot,
                decision == null ? "Wait" : ActionName(decision.Candidate.Identity.Type),
                decision == null ? "None" : decision.Candidate.Identity.Target.ToString(),
                (outcome?.Status ?? "no DNA action (aging or no eligible gene)") + (present ? "" : "; died this season"),
                actor.FoodBalance - (before?.FoodBalance ?? previous?.Food ?? actor.FoodBalance),
                actor.Health - (before?.Health ?? previous?.Health ?? actor.Health), outcome?.Reward ?? 0,
                x, y, destinationX, destinationY, Board.RunOptions.Policy.Learning.ToString(),
                decision?.ChoiceLabel ?? "No DNA choice (aging or no eligible gene)",
                outcome?.FoodGathered ?? 0, outcome?.FoodSpent ?? 0, outcome?.FoodTransferred ?? 0,
                outcome?.Damage ?? 0, outcome?.Healing ?? 0, outcome?.BirthPlaced ?? false, outcome?.Moved ?? false,
                decision?.Candidate.TargetX ?? -1, decision?.Candidate.TargetY ?? -1,
                outcome?.Mutations ?? 0, before != null || previous != null,
                birth.X, birth.Y, birth.ChildId);
            seasonActions.Add(new ViewObservedAction(actor.Id, action));
            if (!isSelected) return;
            if (decision?.Outcome.Committed == true) Marker(x, y, "action");
            trace.Enqueue(action);
            while (trace.Count > 16) trace.Dequeue();
            selected = OrganismView(actor, destinationX, destinationY, present,
                Board.BoardElement[destinationX, destinationY].FoodRemaining);
        }
        internal static string ActionName(DnaTypes type) => type switch
        {
            DnaTypes.Eat => "Eat reserves", DnaTypes.Move => "Move", DnaTypes.GenerateFood => "Gather local food",
            DnaTypes.Kill => "Attack", DnaTypes.CombineDna => "Reproduce", DnaTypes.Evolve => "Mutate",
            DnaTypes.Heal => "Heal", DnaTypes.Infect => "Infect", _ => type.ToString()
        };
        private ViewOrganism OrganismView(Organism actor, int x, int y, bool alive, int localFood) =>
            new(actor.Id, actor.Parent1Id, actor.Parent2Id, actor.Generation, x, y, alive, actor.Health,
                actor.FoodBalance, localFood, actor.Age, actor.SequenceAge, actor.LifetimeSeasons,
                childrenObserved, PatternKey(PatternCode(actor)),
                Array.AsReadOnly(actor.DnaSequence.Select(g => new ViewGene(g.DnaSequenceIndex,
                    ActionName(g.DnaType), g.Target.ToString(), g.DnaCode)).ToArray()),
                Array.AsReadOnly(trace.ToArray()));
        private static int PatternCode(Organism actor)
        {
            int code = 0;
            foreach (var gene in actor.DnaSequence) code = (code << 3) | (byte)gene.DnaType;
            return code;
        }
        private static string PatternKey(int code)
        {
            char[] chars = new char[15];
            for (int i = 0; i < 8; i++)
            { chars[i * 2] = (char)('0' + ((code >> ((7 - i) * 3)) & 7)); if (i < 7) chars[i * 2 + 1] = ','; }
            return new string(chars);
        }
        private static string PatternDescription(int code) => string.Join(" → ",
            Enumerable.Range(0, 8).Select(i => ActionName((DnaTypes)((code >> ((7 - i) * 3)) & 7))));

        internal ViewFrame Capture(long runId, long acknowledged, string status, bool running, string fault,
            double rate, double lastMilliseconds, double? target)
        {
            var cells = new ViewCell[Board.BOARD_SIZE * Board.BOARD_SIZE];
            var patterns = new Dictionary<int, (string Key, int Count)>();
            var genes = new long[8];
            int population = 0;
            long localFood = 0, storedFood = 0;
            for (int y = 0; y < Board.BOARD_SIZE; y++)
                for (int x = 0; x < Board.BOARD_SIZE; x++)
                {
                    BoardSquare square = Board.BoardElement[x, y];
                    Organism actor = square.Occupant;
                    localFood += square.FoodRemaining;
                    int color = 0; string key = null;
                    if (actor != null)
                    {
                        population++; storedFood += actor.FoodBalance;
                        int code = PatternCode(actor);
                        color = unchecked((int)0xFF000000) | code;
                        if (!patterns.TryGetValue(code, out var pattern)) pattern = (PatternKey(code), 0);
                        key = pattern.Key; patterns[code] = (key, pattern.Count + 1);
                        foreach (var gene in actor.DnaSequence) genes[(byte)gene.DnaType]++;
                        if (actor.Id == selectedId) selected = OrganismView(actor, x, y, true, square.FoodRemaining);
                    }
                    cells[y * Board.BOARD_SIZE + x] = new ViewCell(x, y, square.FoodRemaining, actor?.Id, color, key);
                }
            if (history.Count == 0 || history.Last().Season != Board.Season)
            {
                history.Enqueue(new ViewHistory(Board.Season, population, localFood, storedFood, births, deaths, actionCount));
                while (history.Count > 240) history.Dequeue();
            }
            var settings = Board.RunOptions;
            // RandomSource is mutable engine state and never crosses into a frame.
            var options = new SimulationRunOptions { Seed = settings.Seed, Mode = settings.Mode,
                InitialPopulationPercent = settings.InitialPopulationPercent, InitialCellFood = settings.InitialCellFood,
                FoodRegrowthPerAge = settings.FoodRegrowthPerAge, InitialOrganismFood = settings.InitialOrganismFood,
                FounderRepertoire = settings.FounderRepertoire,
                CellExecution = settings.CellExecution, Policy = settings.Policy };
            return new ViewFrame(runId, acknowledged, Board.Season, Board.Age, Board.BOARD_SIZE, Board.BOARD_SIZE,
                status, running, fault, rate, lastMilliseconds, target, population, localFood, storedFood, births, deaths,
                Array.AsReadOnly(cells), Array.AsReadOnly(patterns.OrderByDescending(p => p.Value.Count).ThenBy(p => p.Key)
                    .Take(12).Select(p => new ViewPattern(p.Value.Key, PatternDescription(p.Key), p.Value.Count)).ToArray()),
                Array.AsReadOnly(Enumerable.Range(0, 8).Select(i => new ViewCount(ActionName((DnaTypes)i), genes[i])).ToArray()),
                Array.AsReadOnly(Enumerable.Range(0, 8).Select(i => new ViewCount(ActionName((DnaTypes)i), actions[i])).ToArray()),
                Array.AsReadOnly(history.ToArray()), Array.AsReadOnly(markers.ToArray()), selected, options)
            {
                // Capture only at a completed boundary. Selection/status captures
                // retain the batch, while each frame owns a fresh immutable copy.
                SeasonActions = observedSeason == Board.Season
                    ? Array.AsReadOnly(seasonActions.ToArray()) : Array.Empty<ViewObservedAction>()
            };
        }
    }
}
