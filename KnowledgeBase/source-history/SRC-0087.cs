using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Fistnet.Genepool.Dna.Effects;
using Fistnet.Genepool.Dna.Elements;

namespace Fistnet.Genepool.Dna
{
    /// <summary>Opt-in observation. Attach, detach, reset and export only at quiescent boundaries.</summary>
    public static class SimulationDiagnostics
    {
        private static DiagnosticSession current;
        public static DiagnosticSession Current => Volatile.Read(ref current);

        public static void Attach(DiagnosticSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (Interlocked.CompareExchange(ref current, session, null) != null)
                throw new InvalidOperationException("Detach the existing diagnostic session first.");
        }

        public static DiagnosticSession Detach() => Interlocked.Exchange(ref current, null);
    }

    public readonly struct DiagnosticState
    {
        public int Health { get; }
        public int Reserves { get; }
        public int AvailableFood { get; }
        public int TakenFood { get; }
        public int Age { get; }
        public bool HasChild { get; }
        public bool IsDead { get; }
        public long DnaCode { get; }
        public int RequestedMove { get; }

        public DiagnosticState(Organism organism)
        {
            Health = organism.Health;
            Reserves = organism.FoodBalance;
            AvailableFood = organism.AvailableFood;
            TakenFood = organism.TakenFood;
            Age = organism.Age;
            HasChild = organism.HasChild;
            IsDead = organism.IsDead;
            DnaCode = organism.DnaCode;
            RequestedMove = organism.NextRequestedPosition.HasValue ? (int)organism.NextRequestedPosition.Value : -1;
        }
    }

    public sealed class DiagnosticEvent
    {
        public string Kind { get; set; }
        public int Season { get; set; }
        public long OrganismId { get; set; }
        public long ActorId { get; set; }
        public long ActionId { get; set; }
        public long TargetId { get; set; }
        public string Action { get; set; }
        public int GeneIndex { get; set; } = -1;
        public int X { get; set; } = -1;
        public int Y { get; set; } = -1;
        public string Details { get; set; }
        public DiagnosticState? Before { get; set; }
        public DiagnosticState? After { get; set; }
        public DiagnosticState? TargetBefore { get; set; }
        public DiagnosticContribution? FirstContributor { get; set; }
        public DiagnosticContribution? LastContributor { get; set; }
        public long ObservedContributorCount { get; set; }
        public long ContributorSamplesOmitted { get; set; }
    }

    public readonly struct DiagnosticContribution
    {
        public string Mechanism { get; }
        public string OriginKind { get; }
        public long ActorId { get; }
        public long ActionId { get; }
        public string Action { get; }
        public int GeneIndex { get; }
        public DiagnosticState Before { get; }
        public DiagnosticState After { get; }

        public DiagnosticContribution(string mechanism, string originKind, long actorId, long actionId, string action,
            int geneIndex, DiagnosticState before, DiagnosticState after)
        {
            Mechanism = mechanism; OriginKind = originKind; ActorId = actorId; ActionId = actionId; Action = action;
            GeneIndex = geneIndex; Before = before; After = after;
        }
    }

    public sealed class DiagnosticSeasonReport
    {
        public int Season { get; set; }
        public int Population { get; set; }
        public Dictionary<string, long> Counts { get; set; }
        public Dictionary<string, long> TimingsTicks { get; set; }
        public Dictionary<string, long> TimingSamples { get; set; }
    }

    public sealed class DiagnosticReport
    {
        public string Schema { get; set; } = "genepool-diagnostics-v1";
        public long StopwatchFrequency { get; set; }
        public Dictionary<string, long> Totals { get; set; }
        public Dictionary<string, long> TimingsTicks { get; set; }
        public Dictionary<string, long> TimingSamples { get; set; }
        public DiagnosticSeasonReport[] Seasons { get; set; }
        public DiagnosticEvent[] Events { get; set; }
        public long EventSamplesOmitted { get; set; }
        public long SeasonReportsOmitted { get; set; }
        public Dictionary<string, long> EventSamplesOmittedByCategory { get; set; }
        public string MetricSemantics { get; set; } = "Timing values are Stopwatch ticks. engine.season includes enabled internal hooks. total.season includes engine, observer and census but excludes BeginSeason/CompleteSeason collector bookkeeping; harness call-wall timing is fully inclusive. dna.decision sums concurrent per-call elapsed times including waits and may exceed action-phase wall time. dna.parent_history_copy is nested within effects. Nested/concurrent method times cannot be added to phase totals or interpreted as CPU percentages. Counts are additive: census.max_* in a season is an observed maximum, while its run total sums those observations. Invocation, effect application, scalar state change, movement and birth placement are distinct observations, not interchangeable completion claims.";
        public string EventSamplePolicy { get; set; } = "Total event capacity is split into four reserved pools: death, effect/vitality, action/choice, and other. Remainder slots go to earlier pools. Each pool retains its first events; omitted counts are explicit. This is diagnostic sampling, not an unbiased event-frequency sample.";
        public string Attribution { get; set; } = "Events are bounded samples. Death labels describe observed same-season contributors, not exclusive causal credit. Each retained death also preserves first/last scalar contributor samples and an explicit omitted count, independently of the effect-event pool. Contributor samples cover health transitions, reserve-cost/overflow inputs and direct starvation/age-limit mechanisms. Actor/action IDs are zero when not observed; the death event itself does not assign an exclusive killer. Production scheduling can respond to observation overhead.";
    }

    /// <summary>
    /// All IDs, provenance and counters live here, outside the reflected domain graph.
    /// Counts/timings are atomic; bounded queues avoid an event archive and hot global locks.
    /// Diagnostic callbacks never request random values or change simulation state.
    /// </summary>
    public sealed class DiagnosticSession
    {
        private sealed class Counter { public long Value; }
        private sealed class Identity { public long Value; }
        private sealed class VitalityLedger
        {
            public int Season = -1;
            public int Contributors;
            public long ObservedCount;
            public DiagnosticContribution? First;
            public DiagnosticContribution? Last;
        }
        private sealed class Origin
        {
            public long ActorId;
            public long ActionId;
            public string Kind;
            public string Action;
            public int GeneIndex;
            public int Queued;
            public int Applied;
            public int Changed;
        }

        [Flags]
        private enum Contributor { None = 0, Damage = 1, Healing = 2, Starvation = 4, AgeLimit = 8, ArithmeticWrap = 16, SelfAttack = 32 }

        [ThreadStatic] private static DiagnosticSession actionSession;
        [ThreadStatic] private static Origin actionOrigin;
        private readonly ConcurrentDictionary<string, Counter> totals = new ConcurrentDictionary<string, Counter>();
        private readonly ConcurrentDictionary<string, Counter> timings = new ConcurrentDictionary<string, Counter>();
        private readonly ConcurrentDictionary<string, Counter> timingSamples = new ConcurrentDictionary<string, Counter>();
        private ConcurrentDictionary<string, Counter> seasonCounts = new ConcurrentDictionary<string, Counter>();
        private ConcurrentDictionary<string, Counter> seasonTimings = new ConcurrentDictionary<string, Counter>();
        private ConcurrentDictionary<string, Counter> seasonTimingSamples = new ConcurrentDictionary<string, Counter>();
        private readonly ConcurrentQueue<DiagnosticEvent> events = new ConcurrentQueue<DiagnosticEvent>();
        private readonly ConcurrentQueue<DiagnosticSeasonReport> seasons = new ConcurrentQueue<DiagnosticSeasonReport>();
        private readonly ConditionalWeakTable<Organism, Identity> identities = new ConditionalWeakTable<Organism, Identity>();
        private readonly ConditionalWeakTable<IDnaEffect, Origin> effectOrigins = new ConditionalWeakTable<IDnaEffect, Origin>();
        private readonly ConditionalWeakTable<Organism, VitalityLedger> vitality = new ConditionalWeakTable<Organism, VitalityLedger>();
        private readonly int maxEventSamples;
        private readonly int maxSeasonReports;
        private long nextIdentity;
        private long nextAction;
        private long sampleAttempts;
        private readonly long[] categoryAttempts = new long[4];
        private long seasonReportsOmitted;
        private int currentSeason;
        private bool seasonActive;

        public DiagnosticSession(int maxEventSamples = 128, int maxSeasonReports = 256)
        {
            if (maxEventSamples < 0) throw new ArgumentOutOfRangeException(nameof(maxEventSamples));
            if (maxSeasonReports < 0) throw new ArgumentOutOfRangeException(nameof(maxSeasonReports));
            this.maxEventSamples = maxEventSamples;
            this.maxSeasonReports = maxSeasonReports;
        }

        public bool CanSampleEvents
        {
            get
            {
                for (int category = 0; category < 4; category++)
                    if (Interlocked.Read(ref categoryAttempts[category]) < CategoryLimit(category)) return true;
                return false;
            }
        }
        public bool CanSampleEvent(string kind)
        {
            int category = Category(kind);
            return Interlocked.Read(ref categoryAttempts[category]) < CategoryLimit(category);
        }

        private int CategoryLimit(int category) => maxEventSamples / 4 + (category < maxEventSamples % 4 ? 1 : 0);
        private static int Category(string kind) => kind.StartsWith("death", StringComparison.Ordinal)
            ? 0 : kind.StartsWith("effect", StringComparison.Ordinal) || kind.StartsWith("vitality", StringComparison.Ordinal) ? 1
            : kind.StartsWith("action", StringComparison.Ordinal) || kind.StartsWith("choice", StringComparison.Ordinal) ? 2 : 3;

        public long OrganismId(Organism organism)
        {
            if (organism == null) return 0;
            if (identities.TryGetValue(organism, out Identity identity)) return identity.Value;
            return identities.GetValue(organism, _ => new Identity { Value = Interlocked.Increment(ref nextIdentity) }).Value;
        }

        private static void Add(ConcurrentDictionary<string, Counter> values, string name, long amount)
        {
            Interlocked.Add(ref values.GetOrAdd(name, _ => new Counter()).Value, amount);
        }

        public void Count(string name, long amount = 1)
        {
            Add(totals, name, amount);
            if (seasonActive) Add(seasonCounts, name, amount);
        }

        public void Timing(string name, long elapsedTicks)
        {
            if (elapsedTicks < 0) throw new ArgumentOutOfRangeException(nameof(elapsedTicks));
            Add(timings, name, elapsedTicks);
            Add(timingSamples, name, 1);
            if (seasonActive)
            {
                Add(seasonTimings, name, elapsedTicks);
                Add(seasonTimingSamples, name, 1);
            }
        }

        public void BeginSeason(int season)
        {
            if (seasonActive) throw new InvalidOperationException("A diagnostic season is already active.");
            currentSeason = season;
            seasonCounts = new ConcurrentDictionary<string, Counter>();
            seasonTimings = new ConcurrentDictionary<string, Counter>();
            seasonTimingSamples = new ConcurrentDictionary<string, Counter>();
            seasonActive = true;
        }

        public DiagnosticSeasonReport CompleteSeason(int season, int population)
        {
            if (!seasonActive) throw new InvalidOperationException("No diagnostic season is active.");
            seasonActive = false;
            var report = new DiagnosticSeasonReport { Season = season, Population = population,
                Counts = Copy(seasonCounts), TimingsTicks = Copy(seasonTimings), TimingSamples = Copy(seasonTimingSamples) };
            if (maxSeasonReports == 0) Interlocked.Increment(ref seasonReportsOmitted);
            else
            {
                seasons.Enqueue(report);
                while (seasons.Count > maxSeasonReports && seasons.TryDequeue(out _)) Interlocked.Increment(ref seasonReportsOmitted);
            }
            return report;
        }

        private static Dictionary<string, long> Copy(ConcurrentDictionary<string, Counter> values) =>
            values.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => Interlocked.Read(ref pair.Value.Value));

        public DiagnosticReport Export() => new DiagnosticReport
        {
            StopwatchFrequency = Stopwatch.Frequency,
            Totals = Copy(totals), TimingsTicks = Copy(timings), TimingSamples = Copy(timingSamples),
            Seasons = seasons.ToArray(), Events = events.ToArray(),
            EventSamplesOmitted = Math.Max(0, Interlocked.Read(ref sampleAttempts) - events.Count),
            SeasonReportsOmitted = Interlocked.Read(ref seasonReportsOmitted),
            EventSamplesOmittedByCategory = new Dictionary<string, long>
            {
                ["death"] = Math.Max(0, Interlocked.Read(ref categoryAttempts[0]) - CategoryLimit(0)),
                ["effect_vitality"] = Math.Max(0, Interlocked.Read(ref categoryAttempts[1]) - CategoryLimit(1)),
                ["action_choice"] = Math.Max(0, Interlocked.Read(ref categoryAttempts[2]) - CategoryLimit(2)),
                ["other"] = Math.Max(0, Interlocked.Read(ref categoryAttempts[3]) - CategoryLimit(3))
            }
        };

        public void Event(string kind, Organism organism, int x, int y, string details = null)
        {
            Sample(kind, organism, null, details, null, null, x, y);
        }

        private void Sample(string kind, Organism organism, Origin origin, string details,
            DiagnosticState? before, DiagnosticState? after, int x = -1, int y = -1,
            long targetId = 0, DiagnosticState? targetBefore = null,
            DiagnosticContribution? firstContributor = null, DiagnosticContribution? lastContributor = null,
            long contributorCount = 0)
        {
            Interlocked.Increment(ref sampleAttempts);
            int category = Category(kind);
            if (Interlocked.Increment(ref categoryAttempts[category]) > CategoryLimit(category)) return;
            events.Enqueue(new DiagnosticEvent { Kind = kind, Season = currentSeason,
                OrganismId = OrganismId(organism), ActorId = origin?.ActorId ?? 0,
                ActionId = origin?.ActionId ?? 0, Action = origin?.Action, GeneIndex = origin?.GeneIndex ?? -1,
                Details = details, Before = before, After = after, X = x, Y = y, TargetId = targetId, TargetBefore = targetBefore,
                FirstContributor = firstContributor, LastContributor = lastContributor, ObservedContributorCount = contributorCount,
                ContributorSamplesOmitted = Math.Max(0, contributorCount - 2) });
        }

        public IDisposable BeginAction(Organism actor, IDnaElement gene, Organism target)
        {
            var origin = new Origin { ActorId = OrganismId(actor), ActionId = Interlocked.Increment(ref nextAction),
                Kind = "action", Action = gene.DnaType.ToString(), GeneIndex = gene.DnaSequenceIndex };
            Count("actions.attempted.total");
            Count("actions.attempted." + origin.Action);
            bool sample = CanSampleEvent("action.invoked");
            Sample("action.invoked", actor, origin, sample ? "Declared target=" + gene.Target : null,
                sample ? new DiagnosticState(actor) : (DiagnosticState?)null, null,
                targetId: sample ? OrganismId(target) : 0,
                targetBefore: sample && target != null ? new DiagnosticState(target) : (DiagnosticState?)null);
            return new ActionScope(this, origin, true);
        }

        public IDisposable BeginSystemEffect(Organism actor, string mechanism)
        {
            return new ActionScope(this, new Origin { ActorId = OrganismId(actor), Kind = mechanism, GeneIndex = -1 }, false);
        }

        private sealed class ActionScope : IDisposable
        {
            private readonly DiagnosticSession session;
            private readonly Origin origin;
            private readonly DiagnosticSession previousSession;
            private readonly Origin previousOrigin;
            private readonly bool isAction;
            public ActionScope(DiagnosticSession session, Origin origin, bool isAction)
            {
                this.session = session; this.origin = origin; this.isAction = isAction;
                previousSession = actionSession; previousOrigin = actionOrigin;
                actionSession = session; actionOrigin = origin;
            }
            public void Dispose()
            {
                actionSession = previousSession; actionOrigin = previousOrigin;
                if (isAction && origin.Queued == 0)
                {
                    session.Count("actions.no_effect_queued.total");
                    session.Count("actions.no_effect_queued." + origin.Action);
                }
            }
        }

        public void EffectQueued(Organism recipient, IDnaEffect effect)
        {
            var origin = actionSession == this ? actionOrigin : null;
            if (origin != null)
            {
                origin.Queued++;
                effectOrigins.GetValue(effect, _ => origin);
            }
            Count("effects.queued.total");
            Count("effects.queued." + effect.Effect);
        }

        public void EffectApplied(Organism recipient, IDnaEffect effect, DiagnosticState before)
        {
            effectOrigins.TryGetValue(effect, out Origin origin);
            DiagnosticState after = new DiagnosticState(recipient);
            Count("effects.applied.total");
            Count("effects.applied." + effect.Effect);
            if (origin?.Action != null)
            {
                if (Interlocked.Exchange(ref origin.Applied, 1) == 0)
                {
                    Count("actions.with_applied_effect.total");
                    Count("actions.with_applied_effect." + origin.Action);
                }
                if (ScalarChanged(before, after) && Interlocked.Exchange(ref origin.Changed, 1) == 0)
                {
                    Count("actions.with_scalar_state_change.total");
                    Count("actions.with_scalar_state_change." + origin.Action);
                }
            }
            if (effect.Effect == EffectTypes.HealthChange)
            {
                int change = (sbyte)effect.Value;
                if (change != 0) AddContributor(recipient, change < 0 ? Contributor.Damage : Contributor.Healing);
                if (before.Health + change != after.Health) AddContributor(recipient, Contributor.ArithmeticWrap);
                if (origin?.Action == nameof(DnaTypes.Kill) && origin.ActorId == OrganismId(recipient))
                    AddContributor(recipient, Contributor.SelfAttack);
                Count(change < 0 ? "health.requested_damage" : "health.requested_healing", Math.Abs(change));
                if (change != 0) RememberContribution(recipient, "health.effect", origin, before, after);
            }
            else if (effect.Effect == EffectTypes.FoodChange)
            {
                int requested = (sbyte)effect.Value;
                int uptake = before.AvailableFood - after.AvailableFood;
                Count("food.requested_uptake", Math.Max(0, requested));
                Count("food.actual_uptake", Math.Max(0, uptake));
                Count("food.requested_reserve_cost", Math.Max(0, -requested));
                Count("food.reserve_delta", after.Reserves - before.Reserves);
                bool wrapped = before.Reserves + (requested > 0 ? uptake : requested) != after.Reserves;
                if (wrapped)
                {
                    Count("food.arithmetic_wrap");
                    AddContributor(recipient, Contributor.ArithmeticWrap);
                }
                if (requested < 0 || wrapped)
                    RememberContribution(recipient, wrapped ? "food.arithmetic_wrap" : "food.reserve_cost", origin, before, after);
                if (requested > 0 && before.TakenFood > 0) Count("food.taken_overwrite");
            }
            Sample("effect.applied", recipient, origin, CanSampleEvent("effect.applied")
                ? effect.Effect + "; origin=" + (origin?.Kind ?? "unknown") : null, before, after);
            effectOrigins.Remove(effect);
        }

        private static bool ScalarChanged(DiagnosticState before, DiagnosticState after) =>
            before.Health != after.Health || before.Reserves != after.Reserves || before.AvailableFood != after.AvailableFood ||
            before.TakenFood != after.TakenFood || before.Age != after.Age || before.HasChild != after.HasChild ||
            before.DnaCode != after.DnaCode || before.RequestedMove != after.RequestedMove;

        private void AddContributor(Organism organism, Contributor contributor)
        {
            var ledger = GetLedger(organism);
            Interlocked.Or(ref ledger.Contributors, (int)contributor);
        }

        private VitalityLedger GetLedger(Organism organism)
        {
            var ledger = vitality.GetValue(organism, _ => new VitalityLedger());
            if (ledger.Season != currentSeason)
            {
                ledger.Season = currentSeason; ledger.Contributors = 0; ledger.ObservedCount = 0;
                ledger.First = null; ledger.Last = null;
            }
            return ledger;
        }

        private void RememberContribution(Organism organism, string mechanism, Origin origin,
            DiagnosticState before, DiagnosticState after)
        {
            var ledger = GetLedger(organism);
            var contribution = new DiagnosticContribution(mechanism, origin?.Kind ?? "unattributed_or_direct_mechanism", origin?.ActorId ?? 0, origin?.ActionId ?? 0,
                origin?.Action, origin?.GeneIndex ?? -1, before, after);
            if (ledger.ObservedCount == 0) ledger.First = contribution;
            ledger.Last = contribution;
            ledger.ObservedCount++;
        }

        public void Starvation(Organism organism, DiagnosticState before)
        {
            AddContributor(organism, Contributor.Starvation);
            if (before.Health + before.Reserves != organism.Health) AddContributor(organism, Contributor.ArithmeticWrap);
            Count("starvation.events");
            Count("starvation.requested_health_loss", -before.Reserves);
            RememberContribution(organism, "starvation", null, before, new DiagnosticState(organism));
            Sample("vitality.starvation", organism, null, null, before, new DiagnosticState(organism));
        }

        public void AgeLimit(Organism organism, DiagnosticState before)
        {
            AddContributor(organism, Contributor.AgeLimit);
            Count("age_limit.events");
            RememberContribution(organism, "age_limit", null, before, new DiagnosticState(organism));
            Sample("vitality.age_limit", organism, null, null, before, new DiagnosticState(organism));
        }

        public void RecordDeath(Organism organism, int x, int y)
        {
            Count("deaths.total");
            Contributor contributors = Contributor.None;
            DiagnosticContribution? firstContributor = null, lastContributor = null;
            long contributorCount = 0;
            if (vitality.TryGetValue(organism, out var ledger) && ledger.Season == currentSeason)
            {
                contributors = (Contributor)ledger.Contributors;
                firstContributor = ledger.First; lastContributor = ledger.Last; contributorCount = ledger.ObservedCount;
            }
            string threshold = organism.Health <= 0 ? "nonpositive_health" : organism.Health >= Organism.OVERWEIGHT_DEATH ? "high_health" : "unknown";
            Count("deaths.threshold." + threshold);
            int causalClasses = 0;
            foreach (Contributor flag in new[] { Contributor.Damage, Contributor.Healing, Contributor.Starvation, Contributor.AgeLimit, Contributor.ArithmeticWrap })
                if ((contributors & flag) != 0) { Count("deaths.contributor." + flag); causalClasses++; }
            if ((contributors & Contributor.SelfAttack) != 0) Count("deaths.self_attack_observed");
            Count(causalClasses == 0 ? "deaths.attribution.unknown" : causalClasses > 1 ? "deaths.attribution.mixed" : "deaths.attribution.single_observed_class");
            Sample("death.removed", organism, null, CanSampleEvent("death.removed")
                ? "Threshold=" + threshold + "; observed contributors=" + contributors : null, new DiagnosticState(organism), null, x, y,
                firstContributor: firstContributor, lastContributor: lastContributor, contributorCount: contributorCount);
            vitality.Remove(organism);
        }

        public void ChildCreated(Organism parent, Organism otherParent, Organism child)
        {
            Count("births.created");
            Sample("birth.created", child, null, CanSampleEvent("birth.created")
                ? "Parents=" + OrganismId(parent) + "," + OrganismId(otherParent) : null, null, new DiagnosticState(child));
        }
    }
}
