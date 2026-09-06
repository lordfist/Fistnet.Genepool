using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Fistnet.Genepool.Dna.Elements.Brain
{
    public class StrategyNetwork
    {
        private readonly Organism me;
        private readonly Dictionary<string, Dictionary<byte, float>> mesh = new(StringComparer.Ordinal);
        private readonly Dictionary<byte, GeneIdentity> identities = new();
        private readonly Dictionary<byte, long> versions = new();
        private readonly Dictionary<string, long> recency = new(StringComparer.Ordinal);
        private long clock;
        private ActionDecision pending;
        private long pendingVersion;
        private SimulationPolicy policy;

        // Read diagnostics at quiescent boundaries. No caller receives mutable history.
        public int LearningContextCount => mesh.Count;
        public int LearningEntryCount => mesh.Sum(item => item.Value.Count);
        public int ExternalLearningContextCount => mesh.Keys.Count(IsExternal);

        public StrategyNetwork(Organism organism)
        {
            me = organism ?? throw new ArgumentNullException(nameof(organism));
            SynchronizeGenes();
        }

        private static bool IsExternal(string key) => key.StartsWith("E:", StringComparison.Ordinal);
        private long Version(byte slot) => versions.TryGetValue(slot, out long value) ? value : 0;

        // Reconcile direct gene replacement as well as explicit mutation notifications.
        public void SynchronizeGenes()
        {
            for (byte slot = 0; slot < me.DnaSequence.Count; slot++)
            {
                if (identities.TryGetValue(slot, out var identity))
                {
                    if (!identity.Matches(me.DnaSequence[slot])) InvalidateGene(slot);
                }
                else identities[slot] = new GeneIdentity(me.DnaSequence[slot]);
            }
            foreach (byte slot in identities.Keys.Where(s => s >= me.DnaSequence.Count).ToArray())
                InvalidateGene(slot);
        }

        public void InvalidateGene(byte slot)
        {
            foreach (var context in mesh.ToArray())
            {
                context.Value.Remove(slot);
                if (context.Value.Count == 0) { mesh.Remove(context.Key); recency.Remove(context.Key); }
            }
            versions[slot] = checked(Version(slot) + 1);
            if (slot < me.DnaSequence.Count) identities[slot] = new GeneIdentity(me.DnaSequence[slot]);
            else identities.Remove(slot);
            SimulationDiagnostics.Current?.Count("learning.invalidated_slots");
        }

        private bool IsCurrent(ActionCandidate candidate) => candidate != null
            && candidate.Identity.Slot < me.DnaSequence.Count
            && ReferenceEquals(candidate.Gene.Me, me)
            && ReferenceEquals(me.DnaSequence[candidate.Identity.Slot], candidate.Gene)
            && candidate.Identity.Matches(candidate.Gene);

        private void UsePolicy(SimulationPolicy settings)
        {
            settings ??= Common.Policy;
            if (!ReferenceEquals(policy, settings)) { settings.Validate(); policy = settings; }
            EnforceLimit();
        }

        public ActionDecision Choose(IReadOnlyList<ActionCandidate> candidates, SimulationPolicy settings)
        {
            if (pending != null) throw new InvalidOperationException("Complete the selected action before choosing again.");
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            var diagnostics = SimulationDiagnostics.Current;
            long started = diagnostics == null ? 0 : Stopwatch.GetTimestamp();
            try
            {
                SynchronizeGenes();
                UsePolicy(settings);
                var slots = new HashSet<byte>();
                foreach (var candidate in candidates)
                    if (!IsCurrent(candidate) || !slots.Add(candidate.Identity.Slot))
                        throw new ArgumentException("Candidates must contain distinct current genes owned by this actor.", nameof(candidates));
                var eligible = candidates.Where(c => c.Eligible).OrderBy(c => c.Identity.Slot).ToList();
                if (eligible.Count == 0) return null;
                var known = new List<(ActionCandidate Candidate, float Score)>();
                var unseen = new List<ActionCandidate>();
                foreach (var candidate in eligible)
                {
                    if (mesh.TryGetValue(candidate.Context, out var scores) && scores.TryGetValue(candidate.Identity.Slot, out float score))
                    {
                        if (!float.IsFinite(score)) throw new InvalidOperationException("Stored learning score must be finite.");
                        known.Add((candidate, score));
                    }
                    else unseen.Add(candidate);
                }

                ActionCandidate selected;
                if (policy.Learning == LearningPolicy.BoundedExploratory && unseen.Count > 0)
                {
                    selected = RandomChoice(unseen);
                    diagnostics?.Count("choices.random.unseen");
                }
                else if (policy.Learning == LearningPolicy.BoundedExploratory
                    && policy.ExplorationPercent > 0 && Common.RandomSource.Next(100) < policy.ExplorationPercent)
                {
                    selected = RandomChoice(eligible);
                    diagnostics?.Count("choices.random.exploration");
                }
                else if (known.Count == 0 || (policy.Learning == LearningPolicy.RepairedLegacy && known.Max(x => x.Score) < 0))
                {
                    selected = RandomChoice(eligible);
                    diagnostics?.Count(known.Count == 0 ? "choices.random.no_history" : "choices.random.all_negative");
                }
                else
                {
                    float best = known.Max(x => x.Score);
                    var tied = known.Where(x => x.Score == best).Select(x => x.Candidate).ToList();
                    selected = policy.Learning == LearningPolicy.BoundedExploratory ? RandomChoice(tied) : tied[0];
                    diagnostics?.Count("choices.greedy");
                }
                // Probing candidates does not create contexts or refresh their LRU age.
                if (mesh.ContainsKey(selected.Context)) Touch(selected.Context);
                pending = new ActionDecision(selected);
                pendingVersion = Version(selected.Identity.Slot);
                return pending;
            }
            finally { diagnostics?.Timing("dna.decision", Stopwatch.GetTimestamp() - started); }
        }

        private static ActionCandidate RandomChoice(List<ActionCandidate> candidates) =>
            candidates[candidates.Count == 1 ? 0 : Common.RandomSource.Next(candidates.Count)];

        public void Complete(ActionDecision decision, ActionOutcome outcome)
        {
            if (decision == null || !ReferenceEquals(decision, pending) || decision.Completed)
                throw new InvalidOperationException("Only the current decision may be completed, exactly once.");
            if (outcome == null) throw new ArgumentNullException(nameof(outcome));
            float reward = OrganismEvaluation.EvaluateCompleted(decision, outcome);
            SynchronizeGenes();
            var candidate = decision.Candidate;
            outcome.Reward = reward;
            if (!IsCurrent(candidate) || pendingVersion != Version(candidate.Identity.Slot))
            {
                outcome.StaleCreditDiscarded = true;
                SimulationDiagnostics.Current?.Count("learning.stale_credit_discarded");
            }
            else
            {
                if (!mesh.TryGetValue(candidate.Context, out var scores))
                    mesh[candidate.Context] = scores = new Dictionary<byte, float>();
                scores.TryGetValue(candidate.Identity.Slot, out float previous);
                scores[candidate.Identity.Slot] = OrganismEvaluation.FiniteScore((double)previous + reward);
                Touch(candidate.Context);
                EnforceLimit();
            }
            decision.Completed = true;
            pending = null;
        }

        private void Touch(string context)
        {
            if (IsExternal(context)) recency[context] = checked(++clock);
        }

        private void EnforceLimit()
        {
            if (policy?.Learning != LearningPolicy.BoundedExploratory) return;
            while (ExternalLearningContextCount > policy.ExternalContextLimit)
            {
                string oldest = mesh.Keys.Where(IsExternal)
                    .OrderBy(key => recency.TryGetValue(key, out long age) ? age : 0)
                    .ThenBy(key => key, StringComparer.Ordinal).First();
                mesh.Remove(oldest);
                recency.Remove(oldest);
                SimulationDiagnostics.Current?.Count("learning.contexts_evicted");
            }
        }

        private bool MatchesParent(Organism parent, byte slot) => slot < me.DnaSequence.Count
            && slot < parent.DnaSequence.Count && new GeneIdentity(parent.DnaSequence[slot]).Matches(me.DnaSequence[slot]);

        // Newborn initialization: each matching parent contributes once with equal
        // weight; dictionaries are independent. For bounded inheritance, parent
        // clocks are incomparable, so alternate their newest compatible contexts.
        public void LearnFromParents(Organism first, Organism second)
        {
            if (pending != null) throw new InvalidOperationException("Cannot replace history with a pending decision.");
            SynchronizeGenes();
            UsePolicy(Common.Policy);
            var diagnostics = SimulationDiagnostics.Current;
            long started = diagnostics == null ? 0 : Stopwatch.GetTimestamp();
            long contextsVisited = 0, entriesVisited = 0, matchingEntries = 0;
            try
            {
                var totals = new Dictionary<string, Dictionary<byte, double>>(StringComparer.Ordinal);
                var counts = new Dictionary<string, Dictionary<byte, int>>(StringComparer.Ordinal);
                var orders = new List<string[]>();
                foreach (var parent in new[] { first, second })
                {
                    if (parent == null) { orders.Add(Array.Empty<string>()); continue; }
                    parent.Brain.SynchronizeGenes();
                    var contributed = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var context in parent.Brain.mesh)
                    {
                        contextsVisited++;
                        foreach (var entry in context.Value)
                        {
                            entriesVisited++;
                            if (!MatchesParent(parent, entry.Key)) continue;
                            if (!float.IsFinite(entry.Value)) throw new InvalidOperationException("Inherited score must be finite.");
                            matchingEntries++;
                            if (!totals.TryGetValue(context.Key, out var values))
                            {
                                totals[context.Key] = values = new Dictionary<byte, double>();
                                counts[context.Key] = new Dictionary<byte, int>();
                            }
                            values.TryGetValue(entry.Key, out double sum);
                            counts[context.Key].TryGetValue(entry.Key, out int count);
                            values[entry.Key] = sum + entry.Value;
                            counts[context.Key][entry.Key] = count + 1;
                            if (IsExternal(context.Key)) contributed.Add(context.Key);
                        }
                    }
                    orders.Add(contributed.OrderByDescending(key => parent.Brain.recency.TryGetValue(key, out long age) ? age : 0)
                        .ThenBy(key => key, StringComparer.Ordinal).ToArray());
                }
                int capacity = policy.Learning == LearningPolicy.BoundedExploratory ? policy.ExternalContextLimit : int.MaxValue;
                var newest = new List<string>();
                var included = new HashSet<string>(StringComparer.Ordinal);
                int length = orders.Count == 0 ? 0 : orders.Max(order => order.Length);
                for (int index = 0; index < length && newest.Count < capacity; index++)
                    foreach (var order in orders)
                        if (newest.Count < capacity && index < order.Length && included.Add(order[index]))
                            newest.Add(order[index]);

                mesh.Clear(); recency.Clear(); clock = 0;
                foreach (var context in totals)
                {
                    if (IsExternal(context.Key) && !included.Contains(context.Key)) continue;
                    mesh[context.Key] = context.Value.ToDictionary(entry => entry.Key,
                        entry => OrganismEvaluation.FiniteScore(entry.Value / counts[context.Key][entry.Key]));
                }
                for (int index = newest.Count - 1; index >= 0; index--) Touch(newest[index]);
            }
            finally
            {
                diagnostics?.Timing("dna.parent_history_copy", Stopwatch.GetTimestamp() - started);
                diagnostics?.Count("learning.copy.contexts_visited", contextsVisited);
                diagnostics?.Count("learning.copy.entries_visited", entriesVisited);
                diagnostics?.Count("learning.copy.matching_entries", matchingEntries);
            }
        }

        // Compatibility for narrow direct callers. Real births use LearnFromParents,
        // so two-parent bounded union selection never depends on incremental pruning.
        public void LearnFromParent(Organism parent)
        {
            if (parent == null) return;
            if (pending != null) throw new InvalidOperationException("Cannot import history with a pending decision.");
            SynchronizeGenes(); parent.Brain.SynchronizeGenes(); UsePolicy(Common.Policy);
            foreach (var context in parent.Brain.mesh)
                foreach (var entry in context.Value)
                {
                    if (!MatchesParent(parent, entry.Key)) continue;
                    if (!mesh.TryGetValue(context.Key, out var values))
                        mesh[context.Key] = values = new Dictionary<byte, float>();
                    values[entry.Key] = OrganismEvaluation.FiniteScore(values.TryGetValue(entry.Key, out float old)
                        ? ((double)old + entry.Value) / 2 : entry.Value);
                    Touch(context.Key);
                }
            EnforceLimit();
        }

        // Compatibility fixtures cannot resolve a board target. The production
        // board supplies one separately resolved context per candidate through Choose.
        public IDnaElement ChooseOutput(Organism target, out byte chosenItem)
        {
            var decision = Choose(me.DnaSequence.Select(gene => new ActionCandidate(gene, target)).ToList(), Common.Policy);
            chosenItem = decision.Candidate.Identity.Slot;
            return decision.Candidate.Gene;
        }

        public void EvaluateResult(byte chosenDnaElement, Organism target)
        {
            if (pending == null || pending.Candidate.Identity.Slot != chosenDnaElement
                || !ReferenceEquals(pending.Candidate.Target, target))
                throw new InvalidOperationException("Direct evaluation must match its selected gene and target.");
            Complete(pending, new ActionOutcome
            {
                Status = "legacy_direct_fixture", Attempted = true, ActorPresent = !me.IsDead,
                TargetPresent = target != null && !target.IsDead, ActorAfter = me.CreateSnapshot(), TargetAfter = target?.CreateSnapshot()
            });
        }
    }
}
