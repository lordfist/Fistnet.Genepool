"""Read-only audit and compact summary of the current, bounded Part 4 results.

Reads the current JSON and prospective contract only. Writes stdout, creates no
files, starts no children, and never selects a candidate or launches confirmation.
"""
import hashlib
import json
import math
from pathlib import Path
import statistics

KB = Path(__file__).resolve().parents[1]
RESULT = KB / "r02_part4_results.json"
CONTRACT = KB / "R02_PART4_CONTRACT.md"
CANDIDATES = ("default", "constrained", "regrowth2", "capped")
SEEDS = (11, 29, 47, 83)
ASSEMBLIES = {"Fistnet.Genepool." + name for name in ("Tests", "Control", "Dna", "Visualization", "App")}


def close(left, right):
    return isinstance(left, (int, float)) and isinstance(right, (int, float)) and math.isclose(left, right, rel_tol=1e-9, abs_tol=1e-9)


def finite_nonnegative(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value) and value >= 0


def screen(criteria, complete, requested, observe=True):
    if not observe:
        return "not_measured"
    if not criteria:
        return "inconclusive_horizon"
    if not criteria.get("PopulationAccountingValid"):
        return "invalid_accounting"
    if criteria.get("PopulationCriterionSatisfied") is False or criteria.get("DominationCriterionSatisfied") is False or criteria.get("CompletedActivityWindowsSatisfied") is False:
        return "observed_failure"
    if not complete or requested != 2048 or criteria.get("CompletedSeasons") != 2048:
        return "inconclusive_horizon"
    return "passed" if criteria.get("PopulationCriterionSatisfied") is True and criteria.get("CompletedActivityWindowsSatisfied") is True else "undefined_criterion"


def summarize_run(stage, batch, run):
    errors, omissions = [], []
    arguments = run.get("arguments", [])
    records = run.get("records", [])
    finals = [r for r in records if r.get("kind") == "final"]
    final = finals[-1] if finals else {}
    report = final.get("ecology") or {}
    criteria_row = next((r for r in reversed(records) if r.get("criteria")), {})
    criteria = criteria_row.get("criteria") or {}
    metric_row = next((r for r in reversed(records) if r.get("metrics")), {})
    latest = report.get("Latest") or metric_row.get("metrics") or {}
    windows = report.get("ActivityWindows", [])
    trajectory = report.get("Trajectory", [])
    completed = final.get("completedSeasons")
    requested = final.get("requestedSeasons", int(arguments[2]) if len(arguments) == 6 else None)
    initial = report.get("InitialPopulation", criteria.get("InitialPopulation"))
    def check(condition, text):
        if not condition:
            errors.append(text)
    if len(arguments) != 6:
        errors.append("Ecological command identity is incomplete")
    if len(finals) != 1 or not records or final != records[-1]:
        omissions.append("Exactly one final terminal row is unavailable; unflushed state is unknown")
    if final.get("complete"):
        check(completed == requested, "Terminal completion flag disagrees with requested horizon")
    if not report:
        omissions.append("Final ecological report absent; retained metrics and cumulative criteria describe only their labeled evidence seasons")
    if not latest:
        omissions.append("No detached population sample available")
    complete = bool(final.get("complete") and completed == requested == 2048 and run.get("exit_code") == 0 and not run.get("incomplete"))
    if not complete:
        omissions.append("Requested 2048-season horizon is incomplete; observed-prefix success cannot pass it")
    expected_screen = screen(criteria, bool(final.get("complete")), requested)
    # Interrupted final rows may repeat an older detached criteria snapshot. Its
    # own season controls the screen; the scalar completed count is not that state.
    check(run.get("ecological_screen", expected_screen) == expected_screen, "Wrapper screen differs from retained criteria/horizon")
    if final:
        check(final.get("ecologicalScreen") == expected_screen, "Final screen differs from retained criteria/horizon")

    if report:
        end = report["CompletedSeasons"]
        check(end == completed == latest.get("Season"), "Final report, terminal count and latest season differ")
        check(final.get("metrics") == latest, "Final metrics differ from report Latest")
        check(report.get("PopulationAccountingValid") == criteria.get("PopulationAccountingValid"), "Report/criteria accounting flags differ")
        for key in ("InitialPopulation", "MinimumPopulation", "PeakPopulation", "FirstCollapseSeason", "FirstExtinctionSeason",
                    "FirstDominationSeason", "LongestSamePatternDominationStreak", "PopulationCriterionSatisfied",
                    "DominationCriterionSatisfied", "CompletedActivityWindowsSatisfied", "IncompleteActivityWindowSeasons"):
            check(report.get(key) == criteria.get(key), "Report/criteria mismatch: " + key)
        expected_seasons = sorted(set(range(0, end + 1, 8)) | {end})
        check([s.get("Season") for s in trajectory] == expected_seasons, "Sampled trajectory does not match stated every-eighth/final coverage")
        check(bool(trajectory) and trajectory[-1] == latest, "Last trajectory sample differs from Latest")
        for sample in trajectory:
            season = sample["Season"]
            accounting = initial + sample["Births"] - sample["Deaths"] == sample["Population"]
            check(accounting, f"Visible season {season}: population accounting fails")
            check(sample.get("PopulationAccountingValid") == accounting, f"Visible season {season}: accounting flag disagrees")
            check(sample["InitialCohortLiving"] <= initial and sample["InitialCohortLiving"] <= sample["Population"], f"Visible season {season}: initial-cohort count invalid")
            check(sample["FoundersLiving"] + sample["DescendantsLiving"] == sample["Population"], f"Visible season {season}: generation grouping fails")
            check(close(sample["InitialPopulationFraction"], sample["Population"] / initial) if initial else sample["InitialPopulationFraction"] is None,
                  f"Visible season {season}: initial-population fraction differs")
            check(close(sample["LargestPatternFraction"], sample["LargestPatternCount"] / sample["Population"] if sample["Population"] else 0),
                  f"Visible season {season}: largest-pattern fraction differs")
        if trajectory:
            check(report["MinimumPopulation"] <= min(s["Population"] for s in trajectory), "Minimum population exceeds a visible sample")
            check(report["PeakPopulation"] >= max(s["Population"] for s in trajectory), "Peak population is below a visible sample")
            for field, sample_field, season_field in (
                ("MaximumLargestPatternFraction", "LargestPatternFraction", "MaximumLargestPatternSeason"),
                ("MaximumPeakDrawdownFraction", "PeakDrawdownFraction", "MaximumPeakDrawdownSeason")):
                maximum = report.get(field)
                values = [s[sample_field] for s in trajectory if s.get(sample_field) is not None]
                check(maximum is not None and (not values or maximum + 1e-12 >= max(values)), field + " is below a visible sample or absent")
                check(0 <= report.get(season_field, -1) <= end, season_field + " is outside the observed horizon")
                matching = [s for s in trajectory if s["Season"] == report.get(season_field)]
                if matching:
                    check(close(maximum, matching[0].get(sample_field)), field + " disagrees at its visible maximum season")
            check(report["LongestSamePatternDominationStreak"] >= max(s["SamePatternDominationStreak"] for s in trajectory), "Longest domination streak is below a visible streak")
        check(report["FounderDeaths"] + report["DescendantDeaths"] == latest["Deaths"], "Death grouping differs from removals")
        check(initial - report["InitialCohortDeaths"] == latest["InitialCohortLiving"], "Initial cohort survivor accounting fails")
        collapse = report.get("FirstCollapseSeason")
        check(report["PopulationCriterionSatisfied"] == (None if not initial else report["MinimumPopulation"] * 10 >= initial), "Population criterion disagrees with reported minimum")
        check((collapse is None) == (report["PopulationCriterionSatisfied"] is not False), "Collapse event/criterion disagree")
        check(report["DominationCriterionSatisfied"] == (report.get("FirstDominationSeason") is None), "Domination event/criterion disagree")
        check((report["LongestSamePatternDominationStreak"] >= 128) == (report.get("FirstDominationSeason") is not None), "Domination streak/event disagree")
        for key in ("FirstCollapseSeason", "FirstExtinctionSeason", "FirstDominationSeason"):
            if report.get(key) is not None:
                check(0 <= report[key] <= end, key + " is outside observed coverage")
        expected_windows = max(0, end - 128) // 128
        check(len(windows) == expected_windows, "Activity window count differs from completed coverage")
        check(report["IncompleteActivityWindowSeasons"] == max(0, end - 128) % 128, "Partial activity window length differs")
        for index, window in enumerate(windows):
            check(window["FirstSeason"] == 129 + 128 * index and window["LastSeason"] == 256 + 128 * index, "Activity windows are not consecutive prescribed intervals")
            check(window["EffectiveActionTypeCount"] == len(set(window["EffectiveActionTypes"])), "Activity action count differs from unique types")
            check(window["HasBirthAndVariedActions"] == (window["Births"] >= 1 and window["EffectiveActionTypeCount"] >= 2), "Activity window criterion differs from its observations")
        activity = None if not windows else all(w["HasBirthAndVariedActions"] for w in windows)
        check(report["CompletedActivityWindowsSatisfied"] == activity, "Reported activity criterion differs from windows")
        check(sum(w["Births"] for w in windows) <= latest["Births"], "Complete-window births exceed total births")
        previous = None
        for record in records:
            current = record.get("criteria")
            if not current:
                continue
            if previous:
                check(current["CompletedSeasons"] >= previous["CompletedSeasons"], "Cumulative evidence season regressed")
                for key in ("FirstCollapseSeason", "FirstExtinctionSeason", "FirstDominationSeason"):
                    if previous.get(key) is not None:
                        check(current.get(key) == previous[key], "Previously flushed event disappeared or changed: " + key)
                for key in ("PopulationCriterionSatisfied", "DominationCriterionSatisfied", "CompletedActivityWindowsSatisfied", "PopulationAccountingValid"):
                    if previous.get(key) is False:
                        check(current.get(key) is False, "Previously flushed failure disappeared: " + key)
            check(record.get("criteriaEvidenceSeason") == current["CompletedSeasons"], "Criteria evidence locator differs from detached snapshot")
            check(record.get("ecologicalScreen") == screen(current, bool(record.get("complete")), requested), "Emitted row screen differs from its criteria")
            previous = current

    timing = final.get("timing") or {}
    timing_fields = ("engineCallSeconds", "collectorSeconds", "collectorCensusSeconds", "initialCollectorSeconds",
                     "engineExcludingCollectorSeconds", "validationSeconds", "exportPreparationSeconds", "totalSeconds")
    timing_complete = all(finite_nonnegative(timing.get(k)) for k in timing_fields)
    inside_collector = timing["collectorSeconds"] - timing["initialCollectorSeconds"] if timing_complete else None
    if timing_complete:
        check(close(timing.get("engineExcludingCollectorSeconds"), max(0, timing["engineCallSeconds"] - inside_collector)), "Engine-minus-observer timing arithmetic differs")
        check(0 <= timing["collectorCensusSeconds"] <= timing["collectorSeconds"], "Census/collector timing hierarchy fails")
    else:
        omissions.append("Complete finite engine/observer/export timing is unavailable")
    selection_missing = []
    if not report or report.get("CompletedSeasons") != 2048:
        selection_missing.append("complete final 2048-season ecological report")
    if len(windows) != 15 or any(not finite_nonnegative(w.get("Births")) or not finite_nonnegative(w.get("EffectiveActionTypeCount")) for w in windows):
        selection_missing.append("all 15 populated activity windows")
    if any(not finite_nonnegative(report.get(k)) or report.get(k, 2) > 1 for k in ("MaximumLargestPatternFraction", "MaximumPeakDrawdownFraction")):
        selection_missing.append("finite exact pattern/drawdown extrema")
    if not finite_nonnegative(run.get("wall_seconds")) or not timing_complete:
        selection_missing.append("populated finite elapsed/engine/observer timing")
    selection_evidence_complete = complete and not selection_missing
    if complete and selection_missing:
        omissions.append("Pass-labelled full-horizon selection lacks: " + "; ".join(selection_missing))
    omissions.append("Trajectory contains season 0/every eighth/final only; checks audit exported extrema and flags without independently reconstructing unseen seasons")
    row = {"stage": stage, "kind": batch.get("kind"), "seed": int(arguments[1]) if len(arguments) == 6 else None,
           "candidate": arguments[4] if len(arguments) == 6 else None, "completedSeasons": completed,
           "criteriaEvidenceSeason": criteria.get("CompletedSeasons"), "sampleEvidenceSeason": latest.get("Season"),
           "requestedSeasons": requested, "completeHorizon": complete, "screen": expected_screen,
           "selectionEvidenceComplete": selection_evidence_complete,
           "exitCode": run.get("exit_code"), "reason": run.get("incomplete") or final.get("reason"),
           "population": {"initial": initial, "minimum": report.get("MinimumPopulation", criteria.get("MinimumPopulation")),
                          "finalObserved": latest.get("Population"), "peak": report.get("PeakPopulation", criteria.get("PeakPopulation")),
                          "minimumInitialFraction": report.get("MinimumPopulation", criteria.get("MinimumPopulation")) / initial if initial else None},
           "turnover": {k: latest.get(k) for k in ("Births", "Deaths", "InitialCohortLiving", "DescendantsLiving", "MaximumLivingGeneration", "HighestGenerationObserved", "MeanLivingGeneration")},
           "diversity": {k: report.get(k) for k in ("MaximumLargestPatternFraction", "MaximumLargestPatternSeason", "MaximumPeakDrawdownFraction", "MaximumPeakDrawdownSeason", "FirstCollapseSeason", "FirstDominationSeason")},
           "windows": {"complete": len(windows) if report else None, "minimumBirths": min((w["Births"] for w in windows), default=None),
                       "minimumEffectiveActionTypes": min((w["EffectiveActionTypeCount"] for w in windows), default=None),
                       "failed": [w["FirstSeason"] for w in windows if not w["HasBirthAndVariedActions"]],
                       "incompleteSeasons": report.get("IncompleteActivityWindowSeasons")},
           "effects": {k: latest.get(k) for k in ("AttemptedActions", "CommittedActions", "EffectiveActions", "Moves", "GatheringActions", "DamagingAttacks", "PredationTransfers", "FoodGathered", "FoodSpent", "FoodTransferred", "BoardFood", "OrganismReserves")},
           "actions": report.get("Actions"), "memory": final.get("memory"),
           "timing": {**{k: timing.get(k) for k in ("engineCallSeconds", "collectorSeconds", "collectorCensusSeconds", "initialCollectorSeconds", "engineExcludingCollectorSeconds", "validationSeconds", "exportPreparationSeconds", "totalSeconds")},
                      "outerWallSeconds": run.get("wall_seconds"), "collectorWithinEnginePercent": inside_collector / timing["engineCallSeconds"] * 100 if timing_complete and timing["engineCallSeconds"] else None},
           "auditErrors": errors, "omissions": omissions}
    return row


def summarize(data, current_contract_sha=None):
    errors, omissions, rows, batches = [], [], [], []
    identities, contracts, suites = [], {}, []
    for stage, batch in data.get("checks", {}).items():
        relevant = batch.get("kind") in ("comparison", "reserved-confirmation", "reference-passivity") or batch.get("kind") == "suite" and "final" in stage.lower()
        if relevant:
            contracts[stage] = batch.get("contract_sha256")
            if not batch.get("contract_sha256"):
                omissions.append("Missing prospective contract identity: " + stage)
        batches.append({"stage": stage, "kind": batch.get("kind"), "dispatchComplete": bool(batch.get("complete")),
                        "wrapperOk": batch.get("ok"), "completedProcessRows": len(batch.get("runs", [])),
                        "pending": batch.get("pending"), "unstarted": batch.get("unstarted", [])})
        for run in batch.get("runs", []):
            records = run.get("records", [])
            if relevant and batch.get("configuration") == "Release":
                maps = [r["assemblySha256"] for r in records if r.get("assemblySha256")]
                if len(maps) != 1 or set(maps[0]) != ASSEMBLIES:
                    omissions.append("Exactly five loaded Release assembly identities unavailable: " + run.get("name", stage))
                else:
                    identities.append((stage, run.get("name", stage), maps[0]))
                if batch.get("kind") == "suite":
                    last = records[-1] if records else {}
                    suites.append({"stage": stage, "exitCode": run.get("exit_code"), "total": last.get("total"), "passed": last.get("passed"), "failed": last.get("failed")})
            if batch.get("kind") in ("comparison", "reserved-confirmation"):
                if batch.get("configuration") != "Release":
                    errors.append("Comparison/confirmation does not use Release: " + stage)
                try:
                    rows.append(summarize_run(stage, batch, run))
                except (KeyError, TypeError, ValueError, ZeroDivisionError) as error:
                    errors.append(f"Cannot fully summarize {run.get('name', stage)}: {type(error).__name__}: {error}")
    contract_values = set(contracts.values())
    same_contract = bool(contracts) and None not in contract_values and len(contract_values) == 1
    if contracts and not same_contract:
        errors.append("Relevant stages do not share one prospective contract identity")
    if current_contract_sha is not None and contract_values and contract_values != {current_contract_sha}:
        errors.append("Current prospective contract differs from recorded stage identities")
    baseline = next((m for stage, _, m in reversed(identities) if "final" in stage.lower()), None)
    if baseline is None:
        omissions.append("A final Release suite assembly baseline is unavailable")
    mismatches = [name for _, name, identity in identities if baseline is not None and identity != baseline]
    if mismatches:
        errors.append("Release assembly identities differ from final suite: " + ", ".join(mismatches))
    identity_ok = baseline is not None and not mismatches and not any("assembly identit" in item for item in omissions)
    eligibility = []
    for candidate in CANDIDATES:
        candidate_rows = [r for r in rows if r["kind"] == "comparison" and r["candidate"] == candidate]
        counts = {seed: sum(r["seed"] == seed for r in candidate_rows) for seed in SEEDS}
        missing = [seed for seed, count in counts.items() if count == 0]
        duplicate = [seed for seed, count in counts.items() if count > 1]
        unexpected = [r["seed"] for r in candidate_rows if r["seed"] not in SEEDS]
        valid = len(candidate_rows) == 4 and not missing and not duplicate and not unexpected and all(
            r["completeHorizon"] and r["screen"] == "passed" and r["selectionEvidenceComplete"] and not r["auditErrors"] for r in candidate_rows)
        eligible = bool(valid and same_contract and identity_ok and not errors)
        rank_values = {"minimumWindowBirths": min((r["windows"]["minimumBirths"] for r in candidate_rows), default=None),
                       "maximumExactPatternShare": max((r["diversity"]["MaximumLargestPatternFraction"] for r in candidate_rows), default=None),
                       "medianElapsedSeconds": statistics.median(r["timing"]["outerWallSeconds"] for r in candidate_rows)} if eligible else None
        eligibility.append({"candidate": candidate, "eligibleForSelection": eligible, "missingSeeds": missing,
                            "duplicateSeeds": duplicate, "unexpectedSeeds": unexpected,
                            "incompleteOrFailedSeeds": [r["seed"] for r in candidate_rows if not r["completeHorizon"] or r["screen"] != "passed" or not r["selectionEvidenceComplete"] or r["auditErrors"]],
                            "prospectiveRankingValues": rank_values})
    ranked = sorted((e for e in eligibility if e["eligibleForSelection"]), key=lambda e: (
        -e["prospectiveRankingValues"]["minimumWindowBirths"], e["prospectiveRankingValues"]["maximumExactPatternShare"],
        e["prospectiveRankingValues"]["medianElapsedSeconds"]))
    return {"actor": "Genepool Analyzer", "schema": "genepool.part4-summary.v1", "rows": rows, "batches": batches,
            "audit": {"errors": errors, "rowErrorCount": sum(len(r["auditErrors"]) for r in rows), "omissions": omissions,
                      "sameContract": same_contract, "contractSha256": next(iter(contract_values)) if same_contract else None,
                      "sameFiveReleaseAssemblies": identity_ok, "releaseIdentityRowsChecked": len(identities),
                      "releaseAssemblyBaseline": baseline, "finalReleaseSuites": suites,
                      "scope": "Audits exported identities and arithmetic only; does not read binaries, replay worlds, reconstruct unsampled seasons, or imply ecological horizon completion from dispatchComplete."},
            "eligibility": eligibility, "prospectiveRanking": [e["candidate"] for e in ranked],
            "selection": "No automatic selection or confirmation. Coordinator records a decision before any reserved seed dispatch.",
            "memoryInterpretation": "allocatedBytes is cumulative process allocation, not peak or resident memory."}


def main():
    raw = RESULT.read_bytes()
    result = summarize(json.loads(raw), hashlib.sha256(CONTRACT.read_bytes()).hexdigest().upper())
    result["inputSha256"] = hashlib.sha256(raw).hexdigest().upper()
    print(json.dumps(result, separators=(",", ":")))
    return 1 if result["audit"]["errors"] or result["audit"]["rowErrorCount"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
