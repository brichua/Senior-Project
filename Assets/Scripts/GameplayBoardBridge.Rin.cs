using System.Linq;
using UnityEngine;

namespace VocaloidTCG.BoardUI
{
    public sealed partial class GameplayBoardBridge
    {
        private int DiscardPerformers(int actor, CardState except = null) =>
            state.Side(actor).discard.Count(c => c != except && c.data && c.data.performer && !c.token);

        private void QueueRinChoice(CardState source, int tile, EffectChoice choice, int amount = 0){
            state.queuedEffects.Add(new PendingCardEffect { actor=source.ownerId, source=source,
                effect=source.data.effect, sourceTile=tile, choice=choice, amount=amount, remaining=1 });
        }

        private void BeginQueuedEffect(){
            while(state.pendingEffect == null && state.queuedEffects.Count > 0){
                state.pendingEffect=state.queuedEffects[0];
                state.queuedEffects.RemoveAt(0);
                RefreshChoice();
            }
            if(clock) clock.EffectInputPending=state.pendingEffect != null;
        }

        
        private void DiscardFromHand(CardState card){
            Discard(card);
            if(card != null && card.data.performer && !card.token && state.Side(card.ownerId).leaveDiscardScoreVenue)
                state.Side(card.ownerId).score++;
        }

        private void StageDeparture(CardState card, int tile, bool toDiscard = true){
            if(card == null) return;
            var side=state.Side(card.ownerId);
            side.alliesLeftThisRound++;
            if(toDiscard) Discard(card);
            if(side.leaveDiscardScoreVenue) side.score++;

            foreach(var other in FieldCards(0).Concat(FieldCards(1)).ToArray())
                if(other != card && HasEffect(other,CardEffect.AdjacentLeaveBonus) && Adjacent(tile,TileOf(other)))
                    ChangeInfluence(other,1);

            if(!EffectsEnabled(card)) return;
            switch(card.data.effect){
                case CardEffect.LeaveSearchPerformer:
                    if(DiscardPerformers(card.ownerId,card)>=3) QueueRinChoice(card,tile,EffectChoice.StrongDeckPerformer);
                    break;
                case CardEffect.LeaveDelayedToken:
                    state.delayedTokens.Add(new DelayedStageToken { source=card,tile=tile,round=state.roundNumber+1 }); break;
                case CardEffect.LeaveNextPerformerBonus: side.nextPerformerInfluence+=2; break;
            }
        }

        private bool TokenTileAllowed(PendingCardEffect p, int tile){
            switch(p.effect){
                case CardEffect.CreateAdjacentToken: case CardEffect.ContestedWinToken: return Adjacent(p.sourceTile,tile);
                case CardEffect.BackColumnTokens: return tile%5==(p.actor==0 ? 0 : 4);
                case CardEffect.OutsideCenterTokens: return tile%5!=2;
                default: return CanPlace(p.actor,tile%5,tile/5);
            }
        }

        private void CreateStageToken(CardState source, int tile, int influence){
            int actor=source.ownerId;
            if(Performer(state.tiles[tile],actor)!=null) return;
            var token=MakeCard(source.data,actor);
            token.token=true; token.tokenInfluence=influence; token.currentInfluence=influence;
            token.hasInfluence=true; token.shieldActive=false; token.currentCost=0;
            token.enteredRound=state.roundNumber;
            SetPerformer(state.tiles[tile],actor,token); placedTurns[token.instanceId]=turnNumber;
        }

        private bool TryRinSpellTarget(CardState card, int tile, out bool legal){
            legal=true;
            switch(card.data.effect){
                case CardEffect.DrawTwoDiscardDrawn: case CardEffect.LeaveDiscardScoreVenue: return true;
                case CardEffect.OutsideCenterTokens:
                    legal=Enumerable.Range(0,25).Any(i => i%5!=2 && Performer(state.tiles[i],card.ownerId)==null); return true;
                default: return false;
            }
        }

        private void DrawTwoThenChooseDiscard(CardState source, int tile){
            var before=state.Side(source.ownerId).hand.Select(c => c.instanceId).ToArray();
            DrawCards(source.ownerId,2);
            var drawn=state.Side(source.ownerId).hand.Where(c => !before.Contains(c.instanceId)).Select(c => c.instanceId).ToList();
            if(drawn.Count==0) return;
            // Save the eligible IDs before refreshing choices; only cards that reached the hand qualify.
            state.pendingEffect=new PendingCardEffect { actor=source.ownerId, source=source, effect=source.data.effect,
                sourceTile=tile,choice=EffectChoice.HandDiscard,remaining=1,eligibleIds=drawn };
            RefreshChoice();
        }

        private void ResolveRinSacrifice(PendingCardEffect p, CardState ally){
            if(ally==null || TileOf(ally)<0) { EndChoice(); return; }
            int tile=TileOf(ally);
            RemoveSingle(ally);
            switch(p.effect){
                case CardEffect.SacrificeDrawDiscard:
                    DrawCards(p.actor,2); p.choice=EffectChoice.HandDiscard; RefreshChoice(); break;
                case CardEffect.SacrificeRemoveEnemy:
                    p.choice=EffectChoice.RinEnemy; RefreshChoice(); break;
                case CardEffect.SacrificeWeakenAdjacent:
                    foreach(var enemy in FieldCards(1-p.actor).Where(c => Adjacent(tile,TileOf(c)) && !Protected(c,p.actor)).ToArray())
                        ChangeInfluence(enemy,-2);
                    break;
                case CardEffect.SacrificeDrawWeaken:
                    DrawCards(p.actor,2);
                    if(DiscardPerformers(p.actor)>=5) StartChoice(p.source,tile,EffectChoice.RinEnemy);
                    break;
            }
        }

        private void StartRinRound(){
            StartMeikoRound();
            for(int actor=0;actor<2;actor++){
                var side=state.Side(actor);
                side.alliesLeftLastRound=side.alliesLeftThisRound; side.alliesLeftThisRound=0;
            }
            foreach(var pending in state.delayedTokens.Where(t => t.round<=state.roundNumber).ToArray()){
                CreateStageToken(pending.source,pending.tile,1);
                state.delayedTokens.Remove(pending);
            }
            Recompute();
        }

        private void RefreshRinProgress(){
            for(int actor=0;actor<2;actor++){
                var side=state.Side(actor);
                foreach(var c in side.hand.Concat(side.discard).Concat(FieldCards(actor)).Concat(decks[actor])){
                    c.effectProgress=null;
                    if(c.token || !c.data) continue;
                    switch(c.data.effect){
                        case CardEffect.LeaveSearchPerformer: c.effectProgress="("+DiscardPerformers(actor,c)+"/3)"; break;
                        case CardEffect.SacrificeDrawWeaken: c.effectProgress="("+DiscardPerformers(actor)+"/5)"; break;
                        case CardEffect.PreviousRoundLeaveBonus: case CardEffect.WeakenAfterAllyLeft:
                            c.effectProgress="("+side.alliesLeftLastRound+"/1)"; break;
                    }
                }
            }
        }
    }
}
