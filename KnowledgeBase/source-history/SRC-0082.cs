using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Fistnet.Genepool.Dna.Effects;

namespace Fistnet.Genepool.Dna.Elements.Brain
{
    public class StrategyNetwork
    {
        #region Private properties.

        private Organism _me;
        private Dictionary<long, Dictionary<byte, float>> mesh;
        private OrganismSnapshot targetPreExecuteEffects;
        private OrganismSnapshot mePreExecuteEffects;

        #endregion Private properties.

        // Computed only; diagnostic observation adds no reflected instance state.
        // Read at a quiescent boundary, never while a brain is being updated.
        public int LearningContextCount => this.mesh.Count;
        public int LearningEntryCount => this.mesh.Sum(item => item.Value.Count);

        public void EvaluateResult(byte chosenDnaElement, Organism target)
        {
            OrganismSnapshot meSnapshot = this._me.CreateSnapshot();

            long dnaCode = 0;
            if (targetPreExecuteEffects != null)
            {
                dnaCode = targetPreExecuteEffects.DnaCode;
            }
            if (!this.mesh.ContainsKey(dnaCode))
            {
                this.mesh.Add(dnaCode, new Dictionary<byte, float>());
            }

            if (!this.mesh[dnaCode].ContainsKey(chosenDnaElement))
            {
                this.mesh[dnaCode].Add(chosenDnaElement, 0);
            }

            if (target == null && targetPreExecuteEffects == null)
            {
                this.mesh[dnaCode][chosenDnaElement] += OrganismEvaluation.EvaluateEmptySpace(meSnapshot, mePreExecuteEffects);
            }
            else if (target == null && targetPreExecuteEffects != null && targetPreExecuteEffects.DnaCode == this._me.DnaCode)
            {
                this.mesh[dnaCode][chosenDnaElement] += OrganismEvaluation.SELF_KILL_PENALTY;
            }
            else if (target == null && targetPreExecuteEffects != null && targetPreExecuteEffects.DnaCode != this._me.DnaCode)
            {
                float initState = OrganismEvaluation.EvaluateSnapshot(mePreExecuteEffects, targetPreExecuteEffects);
                float resultState = OrganismEvaluation.EvaluateEmptySpace(meSnapshot, mePreExecuteEffects);

                this.mesh[dnaCode][chosenDnaElement] += resultState - initState;
            }
            else
            {
                OrganismSnapshot targetSnapshot = target.CreateSnapshot();

                float initState = OrganismEvaluation.EvaluateSnapshot(mePreExecuteEffects, targetPreExecuteEffects);
                float resultState = OrganismEvaluation.EvaluateSnapshot(meSnapshot, targetSnapshot);

                this.mesh[dnaCode][chosenDnaElement] += resultState - initState;
            }
        }

        public IDnaElement ChooseOutput(Organism target, out byte chosenItem)
        {
            var diagnostics = SimulationDiagnostics.Current;
            long started = diagnostics == null ? 0 : Stopwatch.GetTimestamp();
            try
            {
                this.mePreExecuteEffects = this._me.CreateSnapshot();
                this.targetPreExecuteEffects = target?.CreateSnapshot();

                long dnaCode = 0;
                if (target != null)
                {
                    dnaCode = target.DnaCode;
                }

                if (this.mesh.TryGetValue(dnaCode, out var knownHistory) && knownHistory.Count > 0)
                {
                    Dictionary<byte, float> historyData = this.mesh[dnaCode];
                    if (historyData.Values.Max() < 0)
                    {
                        diagnostics?.Count("choices.random.all_negative");
                        IDnaElement randomElement = DnaElementFactory.ChooseRandomDnaElement(this._me, out chosenItem);
                        return randomElement;
                    }

                    chosenItem = Common.IsReferenceMode
                        ? historyData.OrderByDescending(key => key.Value).ThenBy(key => key.Key).First().Key
                        : historyData.OrderByDescending(key => key.Value).First().Key;
                    diagnostics?.Count("choices.greedy");
                    return this._me.DnaSequence[chosenItem];
                }
                else
                {
                    diagnostics?.Count("choices.random.no_history");
                    IDnaElement randomElement = DnaElementFactory.ChooseRandomDnaElement(this._me, out chosenItem);
                    return randomElement;
                }
            }
            finally
            {
                diagnostics?.Timing("dna.decision", Stopwatch.GetTimestamp() - started);
            }
        }

        public void LearnFromParent(Organism parent)
        {
            if (parent == null) return;
            var diagnostics = SimulationDiagnostics.Current;
            long started = diagnostics == null ? 0 : Stopwatch.GetTimestamp();
            long contextsVisited = 0, entriesVisited = 0, matchingEntries = 0;
            try
            {
                foreach (var item in parent.Brain.mesh)
                {
                    if (diagnostics != null) contextsVisited++;
                    foreach (var entry in item.Value)
                    {
                        if (diagnostics != null) entriesVisited++;
                        byte sequenceIndex = entry.Key;
                        if (sequenceIndex >= this._me.DnaSequence.Count || sequenceIndex >= parent.DnaSequence.Count) continue;
                        var childGene = this._me.DnaSequence[sequenceIndex];
                        var parentGene = parent.DnaSequence[sequenceIndex];
                        if (childGene.DnaCode == parentGene.DnaCode && childGene.DnaType == parentGene.DnaType
                            && childGene.Target == parentGene.Target && childGene.DnaSequenceIndex == parentGene.DnaSequenceIndex)
                        {
                            if (diagnostics != null) matchingEntries++;
                            if (!this.mesh.TryGetValue(item.Key, out var values))
                                this.mesh[item.Key] = values = new Dictionary<byte, float>();
                            // Called once per parent during birth: two contributions receive equal weight.
                            values[sequenceIndex] = values.TryGetValue(sequenceIndex, out float previous)
                                ? previous * .5f + entry.Value * .5f : entry.Value;
                        }
                    }
                }
            }
            finally
            {
                if (diagnostics != null)
                {
                    long elapsed = Stopwatch.GetTimestamp() - started;
                    diagnostics.Timing("dna.parent_history_copy", elapsed);
                    diagnostics.Count("learning.copy.contexts_visited", contextsVisited);
                    diagnostics.Count("learning.copy.entries_visited", entriesVisited);
                    diagnostics.Count("learning.copy.matching_entries", matchingEntries);
                }
            }
        }

        public StrategyNetwork(Organism me)
        {
            this._me = me;
            this.mesh = new Dictionary<long, Dictionary<byte, float>>();
        }
    }
}
