using System;

namespace VocaloidTCG.BoardUI
{
    public static class CardInfluence
    {
        public static void Reset(CardState card){
            card.protectionValue=card.token ? card.tokenKeywords.protection : card.data.performer ? card.data.protectedInfluence : 0;
            card.shieldActive = card.protectionValue > 0;
            card.currentInfluence = Math.Max(0, card.shieldActive ? card.protectionValue : card.token ? card.tokenInfluence : card.data.influence);
        }

        public static bool BreakShield(CardState card){
            if(card == null || !card.shieldActive) return false;
            card.shieldActive = false;
            card.protectionValue = 0;
            card.currentInfluence = Math.Max(0, card.token ? card.tokenInfluence : card.data.influence);
            card.protectedUntilRound = -1;
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
            if(card == null || card.silenced || card.shieldActive || !PerformerKeywords.Of(card).regen) return;
            if(card.currentInfluence < (card.token ? card.tokenInfluence : card.data.influence)) card.currentInfluence++;
        }

        public static void Protect(CardState card, int amount, int untilRound = -1){
            if(card == null || amount <= 0) return;
            card.protectionValue = Math.Max(card.protectionValue,amount);
            if(card.shieldActive){
                card.currentInfluence = Math.Max(card.currentInfluence, amount + card.auraInfluence);
                if(untilRound < 0) card.protectedUntilRound = -1;
            }else{
                card.shieldActive = true;
                card.currentInfluence = amount + card.auraInfluence;
                card.roundInfluence = 0;
                card.protectedUntilRound = untilRound;
            }
        }
    }
}
