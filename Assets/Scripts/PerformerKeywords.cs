using System;
using System.Collections.Generic;

namespace VocaloidTCG.BoardUI
{
    [Serializable]
    public struct PerformerKeywords
    {
        public bool stageShare, encore, nimble, regen;
        public int harmonize, soloist, protection;
        public int Count => (stageShare ? 1 : 0) + (encore ? 1 : 0) + (nimble ? 1 : 0) +
            (regen ? 1 : 0) + (harmonize > 0 ? 1 : 0) + (soloist > 0 ? 1 : 0) + (protection > 0 ? 1 : 0);

        public void Include(PerformerKeywords other){
            stageShare |= other.stageShare; encore |= other.encore; nimble |= other.nimble; regen |= other.regen;
            harmonize = Math.Max(harmonize, other.harmonize); soloist = Math.Max(soloist, other.soloist);
            protection = Math.Max(protection, other.protection);
        }

        public static PerformerKeywords Of(CardState c, bool shared = true){
            if(c == null || !c.data || c.silenced) return default;
            var k = c.token ? c.tokenKeywords : new PerformerKeywords {
                stageShare = c.data.stageShare || c.data.flags.Contains("stage share"),
                encore = c.data.encore || c.data.flags.Contains("encore"),
                nimble = c.data.nimble || c.data.flags.Contains("nimble"), regen = c.data.regen,
                harmonize = c.data.harmonize, soloist = c.data.soloist, protection = c.data.protectedInfluence
            };
            k.Include(c.gainedKeywords);
            k.Include(c.roundKeywords);
            if(c.shieldActive) k.protection=Math.Max(k.protection,c.protectionValue);
            k.Include(new PerformerKeywords { stageShare=c.inheritedStageShare, encore=c.inheritedEncore || c.grantedEncore,
                nimble=c.inheritedNimble, harmonize=c.inheritedHarmonize, soloist=c.inheritedSoloist });
            if(shared) k.Include(c.sharedKeywords);
            var removed=c.suppressedKeywords;
            if(removed.stageShare) k.stageShare=false; if(removed.encore) k.encore=false;
            if(removed.nimble) k.nimble=false; if(removed.regen) k.regen=false;
            if(removed.harmonize>0) k.harmonize=0; if(removed.soloist>0) k.soloist=0;
            if(removed.protection>0) k.protection=0;
            return k;
        }

        public override string ToString(){
            var names = new List<string>();
            if(stageShare) names.Add("Stage Share"); if(encore) names.Add("Encore");
            if(nimble) names.Add("Nimble"); if(regen) names.Add("Regen");
            if(harmonize>0) names.Add("Harmonize " + harmonize); if(soloist>0) names.Add("Soloist " + soloist);
            if(protection>0) names.Add("Protected " + protection);
            return string.Join(", ", names);
        }
    }
}
