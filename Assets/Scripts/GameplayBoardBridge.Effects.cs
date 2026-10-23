using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VocaloidTCG.BoardUI
{
    public sealed partial class GameplayBoardBridge
    {
        private static CardState Assistant(TileState t, int actor) => actor == 0 ? t.assist0 : t.assist1;
        private static void SetAssistant(TileState t, int actor, CardState c){ if(actor == 0) t.assist0 = c; else t.assist1 = c; }
        private IEnumerable<CardState> FieldCards(int actor) => state.tiles.SelectMany(t => new[] { Performer(t, actor), Assistant(t, actor) }).Where(c => c != null);
        private int TileOf(CardState c) => Array.FindIndex(state.tiles, t => t.side0 == c || t.side1 == c || t.assist0 == c || t.assist1 == c);
        private bool Adjacent(int a, int b) => a >= 0 && b >= 0 && a != b && Math.Abs(a % 5 - b % 5) <= 1 && Math.Abs(a / 5 - b / 5) <= 1;
        private static bool EffectsEnabled(CardState c) => c != null && c.data && !c.token && !c.silenced;
        private static bool HasEffect(CardState c, CardEffect effect) => EffectsEnabled(c) && c.data.effect == effect;
        private static bool StageShare(CardState c) => PerformerKeywords.Of(c).stageShare;
        private static bool Encore(CardState c) => PerformerKeywords.Of(c).encore;
        private static bool Nimble(CardState c) => PerformerKeywords.Of(c).nimble;
        private bool Contested(int index) => state.tiles[index].side0 != null && state.tiles[index].side1 != null;
        private bool Protected(CardState c, int actor) => c != null && c.ownerId != actor && c.protectedTurn == turnNumber;
        
        private int CardCost(CardState c){
            if(HasEffect(c, CardEffect.ScoreDiscount) && c.data.scoreThreshold > 0 && state.Side(c.ownerId).score >= c.data.scoreThreshold) return 0;
            int cost = (c.token ? c.tokenCost : c.data.cost) - c.createdDiscount;
            if(c.data.stageEffect && !c.token) cost += state.Side(c.ownerId).nextSpellTax - state.Side(c.ownerId).nextSpellDiscount;
            return Mathf.Max(0, cost);
        }

        private void ChangeInfluence(CardState c, int amount, bool round = false){
            CardInfluence.Change(c, amount, round);
        }

        private bool SpellTarget(CardState card, int tile){
            int actor = card.ownerId;
            if(IsExtendedEffect(card.data.effect)) return ExtendedSpellTarget(card,tile);
            if(TryRinSpellTarget(card, tile, out var rinLegal)) return rinLegal;
            var ally = Performer(state.tiles[tile], actor); var enemy = Performer(state.tiles[tile], 1 - actor);
            if(UsesPerformerTarget(card.data.effect)) return DirectSpellTargets(card,tile).Any();
            if(ally!=null && ally.rootedRound==state.roundNumber && (card.data.effect==CardEffect.MoveHome || card.data.effect==CardEffect.MoveToEnemy || card.data.effect==CardEffect.ReturnToCenter)) return false;
            
            switch(card.data.effect){
                case CardEffect.WeakenContested: return ally != null && enemy != null && !Protected(enemy, actor);
                case CardEffect.RemoveSmall: return enemy != null && enemy.currentInfluence <= 2 && !Protected(enemy, actor);
                case CardEffect.Remove: case CardEffect.Silence: return enemy != null && !Protected(enemy, actor);
                case CardEffect.BuffContested: return ally != null && enemy != null;
                case CardEffect.ReturnToCenter: return ally != null && (actor == 0 ? tile % 5 > 2 : tile % 5 < 2) && EmptyCenter(actor).Any();
                case CardEffect.MoveHome: return ally != null && Enumerable.Range(0,25).Any(i => Performer(state.tiles[i],actor)==null && (actor==0 ? i%5<=2 : i%5>=2));
                case CardEffect.MoveToEnemy: return ally != null && Enumerable.Range(0,25).Any(i => Performer(state.tiles[i],actor)==null && Performer(state.tiles[i],1-actor)!=null);
                case CardEffect.HighestInfluenceTile: return (ally != null || enemy != null) && FieldCards(actor).Any();
                case CardEffect.ReturnAndShare: return ally != null && state.Side(actor).hand.Count < MaxHandSize && state.tiles.Any(t => t.side0 != null || t.side1 != null);
                case CardEffect.FreePlayAndSearch: return state.Side(actor).hand.Any(c => c != card && c.data.performer && CardCost(c)<=2 && (!HasEffect(c,CardEffect.DiscardToPlay) || state.Side(actor).hand.Count>=3)) && Enumerable.Range(0,25).Any(i => Performer(state.tiles[i],actor)==null && CanPlace(actor,i%5,i/5));
                case CardEffect.WeakenCenter: case CardEffect.WeakenCenterAndScore: case CardEffect.BuffCenter:
                case CardEffect.HarmonizeVenue: case CardEffect.BuffAllAndScore: case CardEffect.MoveAllToCenter: return true;
                default: return ally != null;
            }
        }
        private IEnumerable<int> EmptyCenter(int actor) => Enumerable.Range(0,5).Select(y=>y*5+2).Where(i=>Performer(state.tiles[i],actor)==null);

        private void StartChoice(CardState source, int tile, EffectChoice choice, int amount = 0, int remaining = 1){
            var pending = new PendingCardEffect { actor = source.ownerId, source = source, effect = source.data.effect,
                sourceTile = tile, choice = choice, amount = amount, remaining = remaining };
            if(state.pendingEffect != null){ state.queuedEffects.Add(pending); return; }
            state.pendingEffect = pending;
            RefreshChoice();
        }
        
        private void RefreshChoice(){
            var p = state.pendingEffect; if(p == null) return;
            p.tiles.Clear(); p.cards.Clear();
            var side = state.Side(p.actor);
            if(p.choice == EffectChoice.Extended){
                RefreshExtendedChoice(p);
                if(p.tiles.Count==0 && p.cards.Count==0) EndChoice();
                if(clock) clock.EffectInputPending=state.pendingEffect!=null;
                return;
            }
            switch(p.choice){
                case EffectChoice.HandDiscard:
                    p.prompt = p.effect == CardEffect.DrawTwoDiscardDrawn ? "Discard one of the cards you just drew." : "Choose a card from your hand to discard.";
                    p.cards.AddRange(side.hand.Where(c => p.effect != CardEffect.DrawTwoDiscardDrawn || p.eligibleIds.Contains(c.instanceId))); break;
                case EffectChoice.SacrificeAlly:
                    p.prompt = "Choose an allied performer to remove from the stage.";
                    p.cards.AddRange(FieldCards(p.actor).Where(c => c != p.source)); break;
                case EffectChoice.RinEnemy:
                    p.prompt = p.effect == CardEffect.SacrificeRemoveEnemy ? "Choose an enemy performer to remove from the stage." : "You may give an enemy performer -3 influence.";
                    p.cards.AddRange(FieldCards(1-p.actor).Where(c => !Protected(c,p.actor))); break;
                case EffectChoice.StrongDeckPerformer:
                    p.prompt = "Choose a performer with at least 5 printed influence from your deck.";
                    p.cards.AddRange(decks[p.actor].Where(c => c.data.performer && c.data.influence >= 5)); break;
                case EffectChoice.HandPerformer:
                    p.prompt = "Choose a performer costing 2 or less to play for free.";
                    p.cards.AddRange(side.hand.Where(c=>c.data.performer && CardCost(c)<=2 && (!HasEffect(c,CardEffect.DiscardToPlay) || side.hand.Count>=2))); break;
                case EffectChoice.DeckPerformer:
                    p.prompt = "Choose a performer costing 3 or more from your deck.";
                    p.cards.AddRange(decks[p.actor].Where(c=>c.data.performer && c.data.cost>=3)); break;
                case EffectChoice.DiscardPerformer:
                    p.prompt = "Choose an allied performer from your discard pile.";
                    p.cards.AddRange(side.discard.Where(c=>c.data.performer && !c.token && (!HasEffect(c,CardEffect.DiscardToPlay) || side.hand.Count>0) && (p.effect == CardEffect.DrawAndRevive ? c.data.influence<=4 : RevivalCost(p.source,c)<=side.energy))); break;
                default:
                    p.prompt = p.choice == EffectChoice.TokenTile ? "Choose a highlighted tile to place the token." :
                        p.choice == EffectChoice.MoveTile ? "Choose a highlighted destination tile." :
                        p.choice == EffectChoice.Enemy ? "Choose an enemy performer." :
                        p.choice == EffectChoice.OccupiedTile ? "Choose an occupied tile to receive the influence." : "Choose a highlighted allied performer.";
                    for(int i=0;i<25;i++){
                        var ally=Performer(state.tiles[i],p.actor); var enemy=Performer(state.tiles[i],1-p.actor);
                        bool legal=false;
                        switch(p.choice){
                            case EffectChoice.Ally: legal=ally!=null; break;
                            case EffectChoice.Enemy: legal=enemy!=null; break;
                            case EffectChoice.AdjacentAlly: legal=ally!=null && Adjacent(p.sourceTile,i); break;
                            case EffectChoice.OccupiedTile: legal=ally!=null || enemy!=null; break;
                            case EffectChoice.TokenTile: legal=ally==null && TokenTileAllowed(p,i); break;
                            case EffectChoice.MoveAlly: legal=ally!=null && ally.rootedRound!=state.roundNumber && i%5!=2 && EmptyCenter(p.actor).Any(); break;
                            case EffectChoice.MoveTile:
                                var placing=side.hand.Concat(side.discard).FirstOrDefault(c=>c.instanceId==p.selectedId);
                                legal=ally==null || placing!=null && StageShare(placing) && Assistant(state.tiles[i],p.actor)==null;
                                if(p.effect==CardEffect.MoveToEnemy) legal &= enemy!=null;
                                else if(p.effect==CardEffect.MoveHome) legal &= p.actor==0 ? i%5<=2 : i%5>=2;
                                else if(p.effect==CardEffect.ReturnToCenter || p.effect==CardEffect.MoveAllToCenter) legal &= i%5==2;
                                else legal &= ally!=null || CanPlace(p.actor,i%5,i/5);
                                break;
                        }
                        if(legal) p.tiles.Add(i);
                    }
                    break;
            }
            p.optional = p.effect == CardEffect.MoveAllToCenter && p.choice == EffectChoice.MoveAlly ||
                p.effect == CardEffect.SacrificeDrawWeaken && p.choice == EffectChoice.RinEnemy;
            if(p.tiles.Count==0 && p.cards.Count==0) EndChoice();
            if(clock) clock.EffectInputPending = state.pendingEffect != null;
        }
        
        private int RevivalCost(CardState source, CardState card) => source.data.scoreThreshold>0 && state.Side(source.ownerId).score>=source.data.scoreThreshold ? 0 : card.data.cost;
        
        private void EndChoice(){
            var p=state.pendingEffect;
            state.pendingEffect=null;
            if(p!=null && p.effect==CardEffect.MoveAllToCenter)
                foreach(var c in FieldCards(1-p.actor).Where(c=>TileOf(c)%5==2 && !Protected(c,p.actor)).ToList()) ChangeInfluence(c,-1);
            if(p!=null && p.stage==1 && (p.effect==CardEffect.FreePlayAndSearch || p.effect==CardEffect.ReviveAbility || p.effect==CardEffect.DrawAndRevive)){
                var played=FieldCards(p.actor).FirstOrDefault(c=>c.instanceId==p.selectedId);
                if(played!=null) PerformerPlayed(played,TileOf(played));
            }
            if(clock) clock.EffectInputPending=state.pendingEffect!=null;
        }

        public bool CanChooseEffect(int actor, BoardAction action){
            var p=state?.pendingEffect;
            if(p==null || p.actor!=actor || IsPaused) return false;
            if(action.kind==BoardActionKind.FinishEffect) return p.optional;
            if(action.kind!=BoardActionKind.ChooseEffect) return false;
            return !string.IsNullOrEmpty(action.cardInstanceId) ? p.cards.Any(c=>c.instanceId==action.cardInstanceId) :
                InBounds(action.toColumn,action.toRow) && p.tiles.Contains(action.toRow*5+action.toColumn);
        }
        
        private bool ResolveChoice(int actor, BoardAction action){
            if(!CanChooseEffect(actor,action)) return false;
            var p=state.pendingEffect;
            if(action.kind==BoardActionKind.FinishEffect){ EndChoice(); Recompute(); Publish(); return true; }
            var chosen=p.cards.Find(c=>c.instanceId==action.cardInstanceId);
            if(p.choice==EffectChoice.Extended){
                ResolveExtendedChoice(p,chosen,action.toRow*5+action.toColumn);
                state.Side(actor).hiddenHandCount=state.Side(actor).hand.Count;
                Recompute(); if(IsPuzzle && state.pendingEffect==null) CheckPuzzle(false,false); Publish(); return true;
            }
            if(p.stage==2){
                if(chosen==null) return false;
                state.pendingEffect=null;
                PlayStageEffect(p.source,p.sourceTile,chosen);
                if(clock) clock.EffectInputPending=state.pendingEffect!=null;
                Recompute(); if(IsPuzzle && state.pendingEffect==null) CheckPuzzle(false,false); Publish(); return true;
            }
            
            int tile=action.toRow*5+action.toColumn;
            var ally=InBounds(action.toColumn,action.toRow) ? Performer(state.tiles[tile],actor) : null;
            switch(p.choice){
                case EffectChoice.HandDiscard:
                    state.Side(actor).hand.Remove(chosen); DiscardFromHand(chosen);
                    if(p.effect == CardEffect.DiscardThenDraw) DrawCards(actor,1);
                    EndChoice(); break;
                case EffectChoice.SacrificeAlly: ResolveRinSacrifice(p,chosen); break;
                case EffectChoice.RinEnemy:
                    if(p.effect == CardEffect.SacrificeRemoveEnemy) RemoveSingle(chosen);
                    else ChangeInfluence(chosen,-3);
                    EndChoice(); break;
                case EffectChoice.StrongDeckPerformer:
                    RemoveFromDeck(actor,chosen); GiveCard(actor,chosen); EndChoice(); break;
                case EffectChoice.HandPerformer: case EffectChoice.DiscardPerformer:
                    p.selectedId=chosen.instanceId; p.choice=EffectChoice.MoveTile; p.stage=1; RefreshChoice(); break;
                case EffectChoice.DeckPerformer:
                    RemoveFromDeck(actor,chosen); GiveCard(actor,chosen); EndChoice(); break;
                case EffectChoice.TokenTile:
                    CreateStageToken(p.source,tile,p.amount);
                    if(--p.remaining<=0) EndChoice(); else RefreshChoice(); break;
                case EffectChoice.MoveAlly: p.selectedId=ally.instanceId; p.choice=EffectChoice.MoveTile; RefreshChoice(); break;
                case EffectChoice.MoveTile:
                    var moving=FieldCards(actor).FirstOrDefault(c=>c.instanceId==p.selectedId);
                    if(moving!=null){
                        int from=TileOf(moving); MoveWithPartner(from,tile,actor);
                        if(p.effect==CardEffect.ReturnToCenter) ChangeInfluence(moving,2,true);
                        if(p.effect==CardEffect.MoveAllToCenter){ p.choice=EffectChoice.MoveAlly; RefreshChoice(); } else EndChoice();
                    }else{
                        var list=p.effect==CardEffect.FreePlayAndSearch ? state.Side(actor).hand : state.Side(actor).discard;
                        var revived=list.Find(c=>c.instanceId==p.selectedId);
                        if(revived==null) return false;
                        list.Remove(revived);
                        if(p.effect==CardEffect.ReviveAbility) state.Side(actor).energy-=RevivalCost(p.source,revived);
                        ResetReturnedCard(revived);
                        if(Performer(state.tiles[tile],actor)!=null) SetAssistant(state.tiles[tile],actor,revived); else SetPerformer(state.tiles[tile],actor,revived);
                        placedTurns[revived.instanceId]=turnNumber; revived.enteredRound=state.roundNumber;
                        if(p.effect==CardEffect.FreePlayAndSearch){ p.choice=EffectChoice.DeckPerformer; p.selectedId=revived.instanceId; RefreshChoice(); }
                        else EndChoice();
                    }
                    break;
                case EffectChoice.AdjacentAlly:
                    p.source.sharedTargetId=ally.instanceId; EndChoice(); break;
                case EffectChoice.Ally: ChangeInfluence(ally,1); EndChoice(); break;
                case EffectChoice.Enemy:
                    var enemy=Performer(state.tiles[tile],1-actor); ChangeInfluence(enemy,-2); enemy.rootedRound=state.roundNumber; EndChoice(); break;
                case EffectChoice.OccupiedTile:
                    (actor==0 ? state.roundTileBonus0 : state.roundTileBonus1)[tile]+=p.amount; EndChoice(); break;
            }
            state.Side(actor).hiddenHandCount=state.Side(actor).hand.Count;
            Recompute(); if(IsPuzzle && state.pendingEffect==null) CheckPuzzle(false,false); Publish(); return true;
        }

        private void MoveWithPartner(int from,int to,int actor){
            SetPerformer(state.tiles[to],actor,Performer(state.tiles[from],actor)); SetPerformer(state.tiles[from],actor,null);
            SetAssistant(state.tiles[to],actor,Assistant(state.tiles[from],actor)); SetAssistant(state.tiles[from],actor,null);
            foreach(var c in new[] {Performer(state.tiles[to],actor),Assistant(state.tiles[to],actor)})
                if(c!=null) PerformerEntered(c,to);
        }
        
        private static bool UsesPerformerTarget(CardEffect effect){
            return effect==CardEffect.None || effect==CardEffect.BuffRound || effect==CardEffect.BuffContested || effect==CardEffect.BuffAndDraw ||
                effect==CardEffect.ProtectTurn || effect==CardEffect.DoubleInfluence || effect==CardEffect.WeakenContested ||
                effect==CardEffect.RemoveSmall || effect==CardEffect.Remove || effect==CardEffect.Silence ||
                effect==CardEffect.WeakenAfterAllyLeft || effect==CardEffect.SacrificeWeakenAdjacent || effect==CardEffect.SacrificeDrawWeaken;
        }
        
        private IEnumerable<CardState> DirectSpellTargets(CardState spell,int tile){
            var effect=spell.data.effect;
            bool enemy=effect==CardEffect.WeakenContested || effect==CardEffect.RemoveSmall || effect==CardEffect.Remove || effect==CardEffect.Silence || effect==CardEffect.WeakenAfterAllyLeft;
            if((effect==CardEffect.BuffContested || effect==CardEffect.WeakenContested) && !Contested(tile)) return Enumerable.Empty<CardState>();
            int side=enemy ? 1-spell.ownerId : spell.ownerId;
            return new[] {Performer(state.tiles[tile],side),Assistant(state.tiles[tile],side)}
                .Where(c=>c!=null && !Protected(c,spell.ownerId) && (effect!=CardEffect.RemoveSmall || c.currentInfluence<=2));
        }
        
        private void RemoveSingle(CardState card){
            int tile=TileOf(card); if(tile<0) return;
            if(Assistant(state.tiles[tile],card.ownerId)==card){
                SetAssistant(state.tiles[tile],card.ownerId,null);
                placedTurns.Remove(card.instanceId);
                StageDeparture(card,tile);
            }else{
                // Removing one performer preserves its Stage Share partner.
                SetPerformer(state.tiles[tile],card.ownerId,Assistant(state.tiles[tile],card.ownerId));
                SetAssistant(state.tiles[tile],card.ownerId,null);
                placedTurns.Remove(card.instanceId);
                StageDeparture(card,tile);
            }
        }
        
        private void Discard(CardState c){
            if(c!=null && !c.token) state.Side(c.ownerId).discard.Add(c);
        }
        
        private void ResetReturnedCard(CardState c){
            CardInfluence.Reset(c); c.currentCost=CardCost(c);
            c.auraInfluence=c.roundInfluence=0; c.silenced=c.grantedEncore=false; c.sharedTargetId=null;
            c.rootedRound=c.protectedTurn=c.noScoreRound=c.activatedTurn=c.activatedRound=c.nimbleRound=-1;
            c.inheritedEncore=c.inheritedNimble=c.inheritedStageShare=false; c.inheritedHarmonize=c.inheritedSoloist=0;
            c.gainedKeywords=c.sharedKeywords=default; c.sharedProtectionLevel=0; c.soloProtectionCondition=false;
            c.protectedUntilRound=-1;
            c.activeEffects.Clear();
        }
        
        private void GiveCard(int actor,CardState c){
            bool overflow=state.Side(actor).hand.Count>=MaxHandSize;
            if(overflow) Discard(c); else state.Side(actor).hand.Add(c);
            drawPresentations.Enqueue(new DrawPresentation { card=c, discarded=overflow });
            state.Side(actor).hiddenHandCount=state.Side(actor).hand.Count;
        }
        
        private void RemoveFromDeck(int actor,CardState c){
            var keep=decks[actor].Where(x=>x!=c).ToArray(); decks[actor].Clear(); foreach(var x in keep) decks[actor].Enqueue(x);
            state.Side(actor).deckCount=decks[actor].Count; if(decks[actor].Count==0 && !IsPuzzle) deckExhausted=true;
        }
        
        private List<CardState> DrawFiltered(int actor,int count,bool performer){
            var drawn=decks[actor].Where(c=>c.data.performer==performer).Take(count).ToList();
            foreach(var c in drawn){
                RemoveFromDeck(actor,c);
                GiveCard(actor,c);
            }
            return drawn;
        }
    }
}
