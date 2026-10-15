using System;

namespace VocaloidTCG.BoardUI
{
    public static class CardInfluence
    {
        public static void Reset(CardState card){
            card.shieldActive = !card.token && card.data.performer && card.data.shielded > 0;
            card.currentInfluence = Math.Max(0, card.token ? card.tokenInfluence :
                card.shieldActive ? card.data.shielded : card.data.influence);
        }

        public static bool BreakShield(CardState card){
            if(card == null || !card.shieldActive) return false;
            card.shieldActive = false;
            card.currentInfluence = Math.Max(0, card.data.influence);
            card.auraInfluence = card.roundInfluence = 0;
            return true;
        }

        public static void Change(CardState card, int amount, bool round = false){
            if(card == null) return;
            int actual = Math.Max(-card.currentInfluence, amount);
            card.currentInfluence += actual;
            if(round) card.roundInfluence += actual;
            if(card.currentInfluence <= 0) BreakShield(card);
        }

        public static void Regenerate(CardState card){
           if(card == null || card.token || card.silenced || card.shieldActive || !card.data.regen) return;
            if(card.currentInfluence < card.data.influence) card.currentInfluence++;
        }
    }
}
