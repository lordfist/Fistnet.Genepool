# R02 Part 2 implementation and verification contract

Seed Analyzer, 2026-09-06. Derived implementation contract under DEC-0014, written before Part 2 builds or simulations. The owner accepted Part 1 and authorized Part 2 followed by review. These implementation choices are Seed choices within that scope, not owner-selected ecological optima.

Source horizon: accepted Part 1 working bytes over local Git 2bb2b99d2f77774a3d2a0614c8dc7747349c5685, KB revision 25. Exact registered predecessors selected for edits are retained in KB/source-history because Part 1 is uncommitted. The earlier Part 1 report/results remain historical, unchanged.

## Mechanics

- Freeze the season's participating actors after refresh/setup. Each actor chooses once among genes evaluated against that gene's actual target. Self is object identity; an equal DNA sum is not Self. Out-of-bounds and blocked movement remain explicit.
- Select all decisions before resolving any. Resolve in seeded Fisher-Yates shuffled order. Recheck liveness and affordability at commit. Cost and benefit belong to one transaction. This declared ordering replaces incidental queue/parallel order and breaks trajectory comparability with Part 1; it is not a scheduler-performance claim.
- Gather into the declared beneficiary, including neighbor-directed gathering, from that beneficiary's cell before movement. Debit cell and credit reserves together, cap both, count actual cumulative transfers. No deferred next-season debit.
- Birth commits during ordered action resolution, with living eligible parents and a uniformly chosen currently free neighbor. Construct only after securing a cell; transfer two actor reserves into the child's initial two reserves, charge once and count one successful birth. No room means no child, charge or success. A parent may give birth then die in a later transaction. Newborns start acting next season. Self-parenting remains permitted.
- Preserve finite borders and opposite-direction fallback. Each original actor gets at most one movement attempt; its request expires on every path. Resolve death/removal before movement, then complete learning for the original cohort including removed actors.
- Preserve ninth-turn organism aging/fertility clock for this part. Add run-local scalar identity, parent IDs, generation and monotonic lifetime-seasons without ancestor references.
- Use wider health/resource arithmetic, exact affordability, and caps on cell food and reserves. Infection threshold is exactly 5/10, target-change threshold 4/10. Mutation changes at most two distinct slots; foreign infection does not protect the issuer's unrelated slot. Founder random generation enforces its existing maximum four genes of one type without unbounded retries.
- Preserve self-directed Kill under the damage-only control and the existing DNA-sum related-target Heal restriction. These are visible model limitations, not silently replaced mating/kin rules.

## Policies and reward

Default: repaired legacy selection, legacy overweight death at health >=50, damage-only attack11 with no feeding/cost. Optional policies are independently selectable through run options: unseen-first/10% exploration with random ties and64 external LRU contexts; capped health50; attack cost1 with up to5 actual victim reserves transferred only on that transaction's direct lethal non-Self hit, within attacker capacity. No nonlethal payout, dead-actor payout or duplicate victim payout. These candidate constants await comparative ecological evaluation.

Both learning policies use the same explicit outcome reward:

`0.15*gathered + 0.15*(transferred-spent) + 0.25*placedBirth + 0.35*(finalActorHealth-initialActorHealth) + 0.10*(initialActorAge-finalActorAge) - 1(if actor absent/dead)`.

Health and age are whole-turn associative terms: other actors can contribute to them. No additional damage/movement/external-relative bonus. Numeric weights carry forward, while broken proxy input semantics are deliberately replaced. Scores use finite arithmetic. Gene identity includes slot, code, action type, target and implementation; replacement/target change invalidates incompatible history. Both parents contribute only matching histories, independently copied. Optional context bounds apply after merging. Completed records contain values only; live target references are released after the turn.

## Verification and stop

Use existing VS2022/MSBuild and SDK9.0.315 without new dependencies. Build all five solution projects in Debug and Release; run the full applicable regression/mechanics/rendering/integration suite and focused action/learning tests. Preserve old characterization results as history and replace assertions of deliberately repaired bugs with stronger outcome/conservation guarantees.

Before ecological claims, only bounded diagnostic checks are allowed here: production seeds11/29/47/83 up to2048seasons with checkpoints128/512/2048,120s/process and10min batch maximum; reference seeds11/29 diagnostics on/off128seasons,60s/process and5min batch. Stop timed-out processes and report partial results. Do not tune constants from these observations, use reserved holdouts101/211, or declare ecological success from population collapse. New hashes establish new default repeatability/passivity, not equality with Part1's changed model.

Keep one Part2 current result/report/build record; disposable test/build scratch cleanup. No Git writes, installations, Parts3-4, UI redesign, accepted model, or active trial after completion. Present Part2 changes, tests, remaining limitations and review pointers, then stop for the owner.

## Pre-baseline integration clarification

Selected old gene objects still execute if another action replaces their slot after selection; their learning credit is discarded as stale. Eligibility filtering means unavailable gather/heal genes wait for context changes or mutation, rather than running their previous failed-target retarget branches. The exact40% predicate remains for actual retarget calls (including eligible infection chance failure and direct callers), not a claim that every prior trigger survives. Both changes are explicit model semantics before the new baseline. The old manual positive-effect entry point remains a standalone fixture helper; Board resolution always supplies its actual source-cell callback.
