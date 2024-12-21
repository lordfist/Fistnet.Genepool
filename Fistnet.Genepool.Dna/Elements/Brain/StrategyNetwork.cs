using System;
using System.Collections.Generic;
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
            this.mePreExecuteEffects = this._me.CreateSnapshot();

            long dnaCode = 0;
            if (target != null)
            {
                dnaCode = target.DnaCode;
                this.targetPreExecuteEffects = target.CreateSnapshot();
            }

            if (this.mesh.Count > 0 && this.mesh.ContainsKey(dnaCode))
            {
                Dictionary<byte, float> historyData = this.mesh[dnaCode];
                if (historyData.Values.Max() < 0)
                {
                    IDnaElement randomElement = DnaElementFactory.ChooseRandomDnaElement(this._me, out chosenItem);
                    return randomElement;
                }

                chosenItem = historyData.OrderByDescending(key => key.Value).First().Key;
                return this._me.DnaSequence[chosenItem];
            }
            else
            {
                IDnaElement randomElement = DnaElementFactory.ChooseRandomDnaElement(this._me, out chosenItem);
                return randomElement;
            }
        }

        public void LearnFromParent(Organism parent)
        {
            foreach (var item in this.mesh.Keys)
            {
                foreach (byte sequenceIndex in this.mesh[item].Keys)
                {
                    if (this._me.DnaSequence[sequenceIndex].DnaCode == parent.DnaSequence[sequenceIndex].DnaCode)
                    {
                        if (!this.mesh.ContainsKey(item))
                            this.mesh.Add(item, new Dictionary<byte, float>());

                        this.mesh[item].Add(sequenceIndex, this.mesh[item][sequenceIndex]);
                    }
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