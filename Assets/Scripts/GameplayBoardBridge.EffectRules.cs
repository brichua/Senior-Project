using System;
using System.Linq;
using UnityEngine;

namespace VocaloidTCG.BoardUI
{
    public sealed partial class GameplayBoardBridge
    {
        private int ScoreTile(int index,int actor){
            var t=state.tiles[index];
            return new[] {Performer(t,actor),Assistant(t,actor)}.Where(c=>c!=null && c.noScoreRound!=state.roundNumber).Sum(c=>c.currentInfluence) + (actor==0 ? state.roundTileBonus0 : state.roundTileBonus1)[index];
        }
        
        private void ContestDamage(int index,int actor,int damage){
            var bonus=actor==0 ? state.roundTileBonus0 : state.roundTileBonus1;
            int absorbed=Math.Min(damage,bonus[index]); bonus[index]-=absorbed; damage-=absorbed;
            foreach(var c in new[] {Performer(state.tiles[index],actor),Assistant(state.tiles[index],actor)}){
                if(c==null) continue; int hit=Math.Min(damage,c.currentInfluence); ChangeInfluence(c,-hit); damage-=hit;
            }
        }
        
        private void PerformerPlayed(CardState card,int tile){
            var previous=influenceSource; influenceSource=card;
            try { PerformerPlayedCore(card,tile); } finally { influenceSource=previous; }
        }
        private void PerformerPlayedCore(CardState card,int tile){
            int actor=card.ownerId;
            bool first=state.Side(actor).performersPlayed++==0;
            card.enteredRound=state.roundNumber;
            ExtendedPerformerPlayed(card,tile);
            card.visitedColumns.Add(tile%5);
            DuetPerformerPlayed(card,tile);
            if(card.token) return;
            if(state.Side(actor).nextPerformerInfluence != 0){
                ChangeInfluence(card,state.Side(actor).nextPerformerInfluence);
                state.Side(actor).nextPerformerInfluence=0;
            }
            foreach(var aura in FieldCards(actor).Where(c=>c!=card && HasEffect(c,CardEffect.NewAllyBonus)).ToArray()) ChangeInfluence(card,1);
            
            if(!EffectsEnabled(card)) return;
            switch(card.data.effect){
                case CardEffect.BackColumnTokens: StartChoice(card,tile,EffectChoice.TokenTile,0,2); break;
                case CardEffect.DiscardThenDraw: StartChoice(card,tile,EffectChoice.HandDiscard); break;
                case CardEffect.SacrificeDrawDiscard: case CardEffect.SacrificeRemoveEnemy:
                    StartChoice(card,tile,EffectChoice.SacrificeAlly); break;
                case CardEffect.ShareInfluence: case CardEffect.ShareHalfInfluence: StartChoice(card,tile,EffectChoice.AdjacentAlly); break;
                case CardEffect.CreateAdjacentToken: StartChoice(card,tile,EffectChoice.TokenTile,0); break;
                case CardEffect.CreateTwoTokens: StartChoice(card,tile,EffectChoice.TokenTile,1,2); break;
                case CardEffect.BuffAlly: StartChoice(card,tile,EffectChoice.Ally); break;
                case CardEffect.WeakenOpposingTile: ChangeInfluence(Performer(state.tiles[tile],1-actor),-2); break;
                case CardEffect.DiscountSpell: state.Side(actor).nextSpellDiscount++; break;
                case CardEffect.TaxSpell: state.Side(1-actor).nextSpellTax++; break;
                case CardEffect.FirstPerformerBonus: if(first) ChangeInfluence(card,1,true); break;
                case CardEffect.DiscardToPlay: StartChoice(card,tile,EffectChoice.HandDiscard); break;
                case CardEffect.DrawWithCenterAllies: if(FieldCards(actor).Count(c=>TileOf(c)%5==2)>=3) DrawCards(actor,1); break;
                case CardEffect.GlobalEncore:
                    foreach(var c in FieldCards(actor).ToArray()){
                        ChangeInfluence(c,Contested(TileOf(c)) ? 2 : 1); if(!c.token) c.grantedEncore=true;
                    }
                    break;
                case CardEffect.DrawAndInherit:
                    foreach(var drawn in DrawFiltered(actor,2,true)){
                        card.inheritedEncore |= Encore(drawn); card.inheritedStageShare |= StageShare(drawn); card.inheritedNimble |= Nimble(drawn);
                        card.inheritedHarmonize=Math.Max(card.inheritedHarmonize,drawn.data.harmonize); card.inheritedSoloist=Math.Max(card.inheritedSoloist,drawn.data.soloist);
                    }
                    break;
                case CardEffect.DrawAndRevive: StartChoice(card,tile,EffectChoice.DiscardPerformer); break;
            }
        }

        private void PlayStageEffect(CardState card,int tile,CardState targetOverride=null){
            var previous=influenceSource; influenceSource=card;
            try { PlayStageEffectCore(card,tile,targetOverride); } finally { influenceSource=previous; }
        }
        private void PlayStageEffectCore(CardState card,int tile,CardState targetOverride=null){
            int actor=card.ownerId;
            if(IsDuetEffect(card.data.effect)){ PlayDuetSpell(card,tile,targetOverride); return; }
            if(IsExtendedEffect(card.data.effect)){ PlayExtendedSpell(card,tile,targetOverride); return; }
            if(UsesPerformerTarget(card.data.effect) && targetOverride==null){
                var targets=DirectSpellTargets(card,tile).ToList();
                if(targets.Count>1){
                    state.pendingEffect=new PendingCardEffect { actor=actor,source=card,effect=card.data.effect,sourceTile=tile,choice=EffectChoice.Ally,
                        stage=2,cards=targets,prompt="Choose which performer on this tile receives the effect." };
                    if(clock) clock.EffectInputPending=true;
                    return;
                }
                targetOverride=targets.FirstOrDefault();
            }
            
            var ally=targetOverride ?? Performer(state.tiles[tile],actor);
            switch(card.data.effect){
                case CardEffect.WeakenAfterAllyLeft:
                    ChangeInfluence(targetOverride,state.Side(actor).alliesLeftLastRound > 0 ? -3 : -2,true); break;
                case CardEffect.DrawTwoDiscardDrawn: DrawTwoThenChooseDiscard(card,tile); break;
                case CardEffect.OutsideCenterTokens: StartChoice(card,tile,EffectChoice.TokenTile,1,2); break;
                case CardEffect.LeaveDiscardScoreVenue: state.Side(actor).leaveDiscardScoreVenue=true; break;
                case CardEffect.SacrificeWeakenAdjacent: case CardEffect.SacrificeDrawWeaken:
                    ResolveRinSacrifice(new PendingCardEffect { actor=actor,source=card,effect=card.data.effect,sourceTile=tile },ally); break;
                case CardEffect.MoveHome: case CardEffect.MoveToEnemy: case CardEffect.ReturnToCenter:
                    StartChoice(card,tile,EffectChoice.MoveTile);
                    if(state.pendingEffect!=null) state.pendingEffect.selectedId=ally.instanceId;
                    break;
                case CardEffect.BuffRound: ChangeInfluence(ally,2,true); break;
                case CardEffect.BuffContested: ChangeInfluence(ally,3,true); break;
                case CardEffect.WeakenContested: ChangeInfluence(targetOverride,-2); break;
                case CardEffect.BuffAndDraw: ChangeInfluence(ally,1); DrawCards(actor,1); break;
                case CardEffect.ProtectTurn: ally.protectedTurn=turnNumber; break;
                case CardEffect.DoubleInfluence: ChangeInfluence(ally,ally.currentInfluence); break;
                case CardEffect.ReturnAndShare:
                    int amount=ally.currentInfluence;
                    SetPerformer(state.tiles[tile],actor,null); placedTurns.Remove(ally.instanceId);
                    var partner=Assistant(state.tiles[tile],actor); if(partner!=null){ SetAssistant(state.tiles[tile],actor,null); StageDeparture(partner,tile); }
                    StageDeparture(ally,tile,false);
                    ResetReturnedCard(ally); GiveCard(actor,ally); StartChoice(card,tile,EffectChoice.OccupiedTile,amount); break;
                case CardEffect.FreePlayAndSearch: StartChoice(card,tile,EffectChoice.HandPerformer); break;
                case CardEffect.HighestInfluenceTile:
                    (actor==0 ? state.roundTileBonus0 : state.roundTileBonus1)[tile]+=FieldCards(actor).Max(c=>c.currentInfluence);
                    (actor==0 ? state.tileDraw0 : state.tileDraw1)[tile]=true; break;
                case CardEffect.WeakenCenter:
                    foreach(var c in FieldCards(0).Concat(FieldCards(1)).Where(c=>TileOf(c)%5==2 && !Protected(c,actor)).ToArray()) ChangeInfluence(c,-1);
                    break;
                case CardEffect.RemoveSmall: case CardEffect.Remove: if(targetOverride!=null) RemoveSingle(targetOverride); break;
                case CardEffect.Silence:
                    var enemy=targetOverride; if(enemy==null) break; enemy.silenced=true; enemy.noScoreRound=state.roundNumber;
                    enemy.sharedTargetId=null; enemy.grantedEncore=false; break;
                case CardEffect.WeakenCenterAndScore:
                    foreach(var c in FieldCards(1-actor).Where(c=>TileOf(c)%5==2 && !Protected(c,actor)).ToArray()){
                        int before=c.currentInfluence; ChangeInfluence(c,-3);
                        if(before>0 && c.currentInfluence==0) state.Side(1-actor).score=Math.Max(0,state.Side(1-actor).score-1);
                    }
                    break;
                case CardEffect.BuffCenter:
                    bool threshold=card.data.scoreThreshold>0 && state.Side(actor).score>=card.data.scoreThreshold;
                    foreach(var c in FieldCards(actor).Where(c=>TileOf(c)%5==2).ToArray()) ChangeInfluence(c,threshold ? 2 : 1);
                    break;
                case CardEffect.HarmonizeVenue: state.Side(actor).harmonizeVenue=true; break;
                case CardEffect.BuffAllAndScore:
                    foreach(var c in FieldCards(actor).ToArray()){ ChangeInfluence(c,1); if(TileOf(c)%5==2) state.Side(actor).score++; }
                    break;
                case CardEffect.MoveAllToCenter: StartChoice(card,tile,EffectChoice.MoveAlly); break;
                default: ChangeInfluence(ally,card.data.stageInfluenceChange); break;
            }
            Discard(card);
        }

        public static bool HasActivatedAbility(CardState c){
            if(c==null || c.token || !c.data || !c.data.performer) return false;
            switch(c.data.effect){
                case CardEffect.ColumnMoveAbility: case CardEffect.WeakenAndRoot: case CardEffect.ReviveAbility:
                case CardEffect.EgoRenegade: case CardEffect.DyingBreath: return true;
                default: return false;
            }
        }

        public bool CanActivateEffect(CardState c){
            return c!=null && CanAct(c.ownerId) && state.pendingEffect==null && TileOf(c)>=0 &&
                (HasEffect(c,CardEffect.WeakenAndRoot) ? c.activatedRound!=state.roundNumber : c.activatedTurn!=turnNumber) &&
                (CanActivateDuet(c) || HasEffect(c,CardEffect.ColumnMoveAbility) && FieldCards(c.ownerId).Any(a=>Enumerable.Range(0,25).Any(i=>i%5==TileOf(a)%5 && CanRelocate(a,i))) ||
                HasEffect(c,CardEffect.WeakenAndRoot) && FieldCards(1-c.ownerId).Any() ||
                HasEffect(c,CardEffect.ReviveAbility) && state.phase==RoundPhase.Preparation && state.Side(c.ownerId).discard.Any(d=>d.data.performer && RevivalCost(c,d)<=state.Side(c.ownerId).energy) && Enumerable.Range(0,25).Any(i=>Performer(state.tiles[i],c.ownerId)==null && CanPlace(c.ownerId,i%5,i/5)));
        }
        private bool ActivateEffect(int actor,BoardAction action){
            var c=FieldCards(actor).FirstOrDefault(x=>x.instanceId==action.cardInstanceId);
            if(!CanActivateEffect(c)) return false;
            c.activatedTurn=turnNumber; turnHadAction=true; state.consecutivePasses=0;
            if(CanActivateDuet(c)){
                c.activatedRound=state.roundNumber; StartChoice(c,TileOf(c),EffectChoice.Duet); Publish(); return true;
            }
            if(HasEffect(c,CardEffect.WeakenAndRoot)) c.activatedRound=state.roundNumber;
            StartChoice(c,TileOf(c),HasEffect(c,CardEffect.ColumnMoveAbility) ? EffectChoice.Extended : HasEffect(c,CardEffect.WeakenAndRoot) ? EffectChoice.Enemy : EffectChoice.DiscardPerformer);
            Publish(); return true;
        }

        private void RecomputeEffects(){
            var all=FieldCards(0).Concat(FieldCards(1)).ToArray();
            var previousKeywordSources=all.ToDictionary(c=>c,c=>c.keywordAuraSources.ToArray());
            foreach(var c in all) c.keywordAuraSources.Clear();
            foreach(var c in all){ c.currentInfluence-=c.auraInfluence; c.auraInfluence=0; }
            RefreshPositionKeywords(all);
            foreach(var c in all){
                int tile=TileOf(c), actor=c.ownerId, bonus=DuetAura(c,all,previousKeywordSources[c]);
                int adjacent=all.Count(other=>other.ownerId==actor && Adjacent(tile,TileOf(other)));
                int friends=all.Count(other=>other.ownerId==actor && Adjacent(tile,TileOf(other)));
                if(!c.silenced){
                    var keywords=PerformerKeywords.Of(c);
                    int harmonize=keywords.harmonize;
                    if(state.Side(actor).harmonizeVenue && (actor==0 ? tile%5>=2 : tile%5<=2)) harmonize=Math.Max(1,harmonize);
                    if(adjacent>=2) bonus+=harmonize;
                    if(tile%5==2 && all.Count(other=>other.ownerId==actor && TileOf(other)%5==2)==1) bonus+=keywords.soloist;
                    switch(c.token ? CardEffect.None : c.data.effect){
                        case CardEffect.OpponentSideBonus: if(OpponentSide(actor,tile)) bonus+=2; break;
                        case CardEffect.AdjacentSelfBonus: if(friends>0) bonus+=2; break;
                        case CardEffect.TwoAdjacentBonus: if(adjacent>=2) bonus+=3; break;
                        case CardEffect.HighEnergyBonus: if(state.Side(actor).energy>=6) bonus+=2; break;
                        case CardEffect.SoloCenterBonus: if(tile%5==2 && all.Count(other=>TileOf(other)%5==2)==1) bonus+=3; break;
                        case CardEffect.ContestedPenalty: if(Contested(tile)) bonus--; break;
                        case CardEffect.PreviousRoundLeaveBonus: if(state.Side(actor).alliesLeftLastRound>0) bonus++; break;
                    }
                }
                foreach(var aura in all){
                    int from=TileOf(aura);
                    if(!EffectsEnabled(aura)) continue;
                    int previousBonus=bonus;
                    switch(aura.data.effect){
                        case CardEffect.KeywordInfluence: bonus+=(aura.ownerId==actor ? 1 : -1)*PerformerKeywords.Of(c).Count; break;
                        case CardEffect.PerformanceColumnsAura:
                            if(aura.ownerId==actor && (state.phase==RoundPhase.Performance || state.phase==RoundPhase.EndRound) && (tile%5==2 || OpponentSide(actor,tile))) bonus+=OccupiedColumns(actor);
                            break;
                        case CardEffect.AdjacentAura: if(aura.ownerId==actor && Adjacent(from,tile)) bonus++; break;
                        case CardEffect.GlobalAura: if(aura.ownerId==actor) bonus+=2; break;
                        case CardEffect.ContestedAdjacentAura: if(aura.ownerId==actor && Adjacent(from,tile) && Contested(tile)) bonus++; break;
                        case CardEffect.ContestedAlliesAura: if(aura.ownerId==actor && Contested(tile)) bonus++; break;
                        case CardEffect.WeakenEnemyCenter: if(aura.ownerId!=actor && tile%5==2) bonus--; break;
                        case CardEffect.WeakenFront: if(aura.ownerId!=actor && tile/5==from/5 && tile%5==from%5+(aura.ownerId==0 ? 1 : -1)) bonus--; break;
                    }
                    if(bonus>previousBonus) TrackKeywordAura(c,aura,previousKeywordSources[c]);
                }
                c.auraInfluence=bonus;
            }
            
            var shared=all.Where(c=>HasEffect(c,CardEffect.ShareInfluence)||HasEffect(c,CardEffect.ShareHalfInfluence))
                .Select(c=>new { source=c, amount=Math.Max(0,c.currentInfluence+c.auraInfluence) / (c.data.effect==CardEffect.ShareHalfInfluence ? 2 : 1) }).ToArray();
            foreach(var link in shared){
                var target=all.FirstOrDefault(c=>c.ownerId==link.source.ownerId && c.instanceId==link.source.sharedTargetId);
                if(target!=null && Adjacent(TileOf(target),TileOf(link.source))){
                    target.auraInfluence+=link.amount;
                    if(link.amount>0) TrackKeywordAura(target,link.source,previousKeywordSources[target]);
                }
            }
            
            foreach(var c in all){
                c.auraInfluence=Math.Max(-c.currentInfluence,c.auraInfluence); c.currentInfluence+=c.auraInfluence;
                if(c.currentInfluence <= 0) CardInfluence.BreakShield(c);
                c.activeEffects.Clear();
                if(c.shieldActive) c.activeEffects.Add("Protected: " + c.currentInfluence + " shield influence; " + (c.token ? c.tokenInfluence : c.data.influence) + " original influence underneath.");
                if(c.token) c.activeEffects.Add("Token: " + CardCost(c) + " cost; no card abilities.");
                if(PerformerKeywords.Of(c).Count>0) c.activeEffects.Add("Keywords: "+PerformerKeywords.Of(c)+".");
                if(c.silenced) c.activeEffects.Add("Silenced: no effects or keywords.");
                if(EffectsEnabled(c)){
                    if(c.grantedEncore || c.inheritedEncore) c.activeEffects.Add("Gained Encore.");
                    if(c.inheritedHarmonize>0) c.activeEffects.Add("Gained Harmonize "+c.inheritedHarmonize+".");
                    if(c.inheritedSoloist>0) c.activeEffects.Add("Gained Soloist "+c.inheritedSoloist+".");
                    if(c.inheritedNimble) c.activeEffects.Add("Gained Nimble.");
                    if(c.inheritedStageShare) c.activeEffects.Add("Gained Stage Share.");
                }
                if(c.roundInfluence!=0) c.activeEffects.Add("Round influence: " + c.roundInfluence.ToString("+0;-0"));
                if(c.auraInfluence!=0) c.activeEffects.Add("Ongoing influence: " + c.auraInfluence.ToString("+0;-0"));
                if(c.rootedRound==state.roundNumber) c.activeEffects.Add("Cannot move this round.");
                if(c.noScoreRound==state.roundNumber) c.activeEffects.Add("Cannot score this round.");
                if(c.protectedTurn==turnNumber) c.activeEffects.Add("Protected from opponent stage effects this turn.");
            }
            foreach(var c in state.side0.hand.Concat(state.side1.hand)) c.currentCost=CardCost(c);
            RefreshRinProgress();
        }

        private void EndRoundEffects(){
            foreach(var c in FieldCards(0).Concat(FieldCards(1)).ToArray()){
                bool restore=HasEffect(c,CardEffect.RestoreCenter) && TileOf(c)%5==2 || state.Side(c.ownerId).harmonizeVenue && c.currentInfluence>=3;
                c.currentInfluence=Math.Max(0,c.currentInfluence-c.auraInfluence-c.roundInfluence); c.auraInfluence=c.roundInfluence=0;
                if(restore && c.currentInfluence<c.data.influence) c.currentInfluence++;
                if(c.shieldActive && c.currentInfluence <= 0) CardInfluence.BreakShield(c);
                if(c.protectedUntilRound==state.roundNumber) CardInfluence.BreakShield(c);
                int age=state.roundNumber-c.enteredRound+1;
                if(HasEffect(c,CardEffect.DelayedEnergy) && age==2) state.Side(c.ownerId).nextRoundEnergyBonus++;
                if(HasEffect(c,CardEffect.DelayedSpell) && age==1) DrawFiltered(c.ownerId,1,false);
            }
            
            ClearDuetRound();
            Array.Clear(state.roundTileBonus0,0,25); Array.Clear(state.roundTileBonus1,0,25);
            Array.Clear(state.tileDraw0,0,25); Array.Clear(state.tileDraw1,0,25);
            state.side0.performersPlayed=state.side1.performersPlayed=0;
            Recompute();
        }
    }
}
