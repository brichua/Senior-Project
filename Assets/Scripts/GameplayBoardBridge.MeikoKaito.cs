using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VocaloidTCG.BoardUI
{
    public sealed partial class GameplayBoardBridge
    {
        private static bool IsExtendedEffect(CardEffect effect) => effect >= CardEffect.RevealDeckKeywords;
        private bool OpponentSide(int actor,int tile) => actor==0 ? tile%5>2 : tile%5<2;
        private int OccupiedColumns(int actor) => FieldCards(actor).Select(c=>TileOf(c)%5).Distinct().Count();
        private IEnumerable<CardData> CreationPool => deckCatalog ? deckCatalog.cards.Where(c=>c && !c.vip) :
            player0Cards.Concat(player1Cards).Where(c=>c && !c.vip).Distinct();

        private CardState CreateRandomCard(int actor,Func<CardData,bool> predicate,int discount=0){
            var pool=CreationPool.Where(predicate).ToArray();
            if(pool.Length==0) return null;
            var c=MakeCard(pool[UnityEngine.Random.Range(0,pool.Length)],actor);
            MarkCreated(c,discount); GiveCard(actor,c); return c;
        }
        private void MarkCreated(CardState c,int discount=0){
            c.createdCard=true;
            c.createdDiscount=discount+FieldCards(c.ownerId).Count(x=>HasEffect(x,CardEffect.CreatedCardDiscount));
            c.currentCost=CardCost(c);
        }
        private CardState NewToken(CardState source,int influence,int cost=0){
            var c=MakeCard(source.data,source.ownerId);
            c.token=true; c.tokenInfluence=influence; c.tokenCost=cost;
            c.currentInfluence=influence; c.shieldActive=false; c.hasInfluence=true;
            MarkCreated(c); return c;
        }
        private void GainKeywords(CardState card,PerformerKeywords keywords){
            int previous=PerformerKeywords.Of(card).protection;
            card.gainedKeywords.Include(keywords);
            if(keywords.protection>previous) CardInfluence.Protect(card,keywords.protection);
        }
        private PerformerKeywords RandomKeyword(){
            switch(UnityEngine.Random.Range(0,7)){
                case 0:return new PerformerKeywords { stageShare=true };
                case 1:return new PerformerKeywords { encore=true };
                case 2:return new PerformerKeywords { nimble=true };
                case 3:return new PerformerKeywords { regen=true };
                case 4:return new PerformerKeywords { harmonize=1 };
                case 5:return new PerformerKeywords { soloist=1 };
                default:return new PerformerKeywords { protection=1 };
            }
        }
        private void Reveal(IEnumerable<CardState> cards,string message){
            state.revealedCards=cards.ToList(); state.revealMessage=message;
        }

        private void ExtendedPerformerPlayed(CardState card,int tile){
            int actor=card.ownerId;
            foreach(int column in state.Side(actor).nextPerformerColumns.Where(x=>x==tile%5).ToArray()){
                ChangeInfluence(card,1); state.Side(actor).nextPerformerColumns.Remove(column);
            }
            if(PerformerKeywords.Of(card).Count>0)
                foreach(var other in FieldCards(actor).Where(c=>c!=card && HasEffect(c,CardEffect.KeywordPlayBonus)).ToArray()) ChangeInfluence(other,1);
            PerformerEntered(card,tile);
            if(!EffectsEnabled(card)) return;
            switch(card.data.effect){
                case CardEffect.RevealDeckKeywords:
                    var top=decks[actor].Take(3).ToArray();
                    Reveal(top,"Revealed from the top of the deck (not drawn)");
                    foreach(var c in top.Where(c=>c.data.performer || c.token)) GainKeywords(card,PerformerKeywords.Of(c));
                    break;
                case CardEffect.CreateFirstClasses:
                    CreateRandomCard(actor,c=>c.cardClass && (c.cardClass.identity==CharacterClass.HatsuneMiku || c.cardClass.identity==CharacterClass.KagamineLen || c.cardClass.identity==CharacterClass.MegurineLuka)); break;
                case CardEffect.CreateLastClasses:
                    CreateRandomCard(actor,c=>c.cardClass && (c.cardClass.identity==CharacterClass.KagamineRin || c.cardClass.identity==CharacterClass.MEIKO || c.cardClass.identity==CharacterClass.KAITO)); break;
                case CardEffect.CreateKeywordToken:
                    var token=NewToken(card,2,2); token.tokenKeywords=RandomKeyword();
                    if(token.tokenKeywords.protection>0) CardInfluence.Protect(token,token.tokenKeywords.protection);
                    GiveCard(actor,token); break;
                case CardEffect.RevealHandKeywords: case CardEffect.OtherColumnToken:
                    StartChoice(card,tile,EffectChoice.Extended); break;
                case CardEffect.NextColumnBonus:
                    state.Side(actor).nextPerformerColumns.Add(tile%5+(actor==0 ? 1 : -1)); break;
                case CardEffect.ThreeColumnsDraw: if(OccupiedColumns(actor)>=3) DrawCards(actor,1); break;
            }
        }

        private void PerformerEntered(CardState card,int tile){
            if(HasEffect(card,CardEffect.DisplaceContested) && Contested(tile)){
                StartChoice(card,tile,EffectChoice.Extended); return;
            }
            bool across=HasEffect(card,CardEffect.CrossColumnGreeting);
            if(!across && !HasEffect(card,CardEffect.AllyGreeting)) return;
            var neighbors=FieldCards(0).Concat(FieldCards(1)).Where(c=>c!=card && Adjacent(tile,TileOf(c)) &&
                (across ? TileOf(c)%5!=tile%5 : c.ownerId==card.ownerId)).ToArray();
            foreach(var other in neighbors){ ChangeInfluence(card,1); ChangeInfluence(other,1); }
        }

        
        private bool CanRelocate(CardState c,int tile){
            if(c==null || tile<0 || tile>=25 || TileOf(c)==tile || c.rootedRound==state.roundNumber) return false;
            return Performer(state.tiles[tile],c.ownerId)==null || StageShare(c) && Assistant(state.tiles[tile],c.ownerId)==null;
        }
        private void Detach(CardState c){
            int from=TileOf(c); if(from<0) return;
            var t=state.tiles[from];
            if(Assistant(t,c.ownerId)==c) SetAssistant(t,c.ownerId,null);
            else { SetPerformer(t,c.ownerId,Assistant(t,c.ownerId)); SetAssistant(t,c.ownerId,null); }
        }
        private void PutPerformer(CardState c,int tile){
            if(Performer(state.tiles[tile],c.ownerId)==null) SetPerformer(state.tiles[tile],c.ownerId,c);
            else SetAssistant(state.tiles[tile],c.ownerId,c);
        }
        private void Relocate(CardState c,int tile){
            Detach(c); PutPerformer(c,tile); PerformerEntered(c,tile);
        }
        private IEnumerable<int> AdjacentDestinations(CardState c) => Enumerable.Range(0,25).Where(i=>Adjacent(TileOf(c),i) && CanRelocate(c,i));
        private IEnumerable<CardState> AtTile(int tile) => new[] {state.tiles[tile].side0,state.tiles[tile].assist0,state.tiles[tile].side1,state.tiles[tile].assist1}.Where(c=>c!=null);
        private bool CanSwap(CardState a,CardState b){
            if(a==b || a==null || b==null || a.ownerId!=b.ownerId || TileOf(a)==TileOf(b) || a.rootedRound==state.roundNumber || b.rootedRound==state.roundNumber) return false;
            return (AtTile(TileOf(a)).Count(c=>c.ownerId==a.ownerId)<2 || StageShare(b) || AtTile(TileOf(a)).Any(c=>c!=a && c.ownerId==a.ownerId && StageShare(c))) &&
                (AtTile(TileOf(b)).Count(c=>c.ownerId==b.ownerId)<2 || StageShare(a) || AtTile(TileOf(b)).Any(c=>c!=b && c.ownerId==b.ownerId && StageShare(c)));
        }

        private bool ExtendedSpellTarget(CardState spell,int tile){
            int actor=spell.ownerId; var allies=FieldCards(actor).ToArray();
            switch(spell.data.effect){
                case CardEffect.PositionKeywordVenue:return true;
                case CardEffect.StrongestColumns:return allies.Length>0;
                case CardEffect.DiscardAndCombine:return state.Side(actor).hand.Any(c=>c!=spell && (c.token || c.data.performer)) && Enumerable.Range(0,25).Any(i=>CanPlace(actor,i%5,i/5) && Performer(state.tiles[i],actor)==null);
                case CardEffect.RowInfluence:return AtTile(tile).Any(c=>!Protected(c,actor));
                case CardEffect.FormationBonus:return allies.Any(a=>allies.Any(b=>a!=b && (TileOf(a)/5==TileOf(b)/5 || TileOf(a)%5==TileOf(b)%5)));
                default:return ExtendedInitialTargets(spell,tile).Any();
            }
        }
        private IEnumerable<CardState> ExtendedInitialTargets(CardState spell,int tile){
            int actor=spell.ownerId;
            return AtTile(tile).Where(c=>{
                switch(spell.data.effect){
                    case CardEffect.GrantProtected:return c.ownerId==actor;
                    case CardEffect.ReturnAndCopy:return c.currentInfluence<=3 && !Protected(c,actor);
                    case CardEffect.MoveAndAdjust:return !Protected(c,actor) && AdjacentDestinations(c).Any();
                    case CardEffect.MoveAndCreate:return c.ownerId==actor && AdjacentDestinations(c).Any();
                    case CardEffect.ShareAdjacentKeywords:return c.ownerId==actor && FieldCards(actor).Any(o=>Adjacent(tile,TileOf(o)));
                    case CardEffect.SwapAllies:return c.ownerId==actor && FieldCards(actor).Any(o=>CanSwap(c,o));
                    default:return false;
                }
            });
        }
        private void PlayExtendedSpell(CardState card,int tile){
            int actor=card.ownerId;
            switch(card.data.effect){
                case CardEffect.PositionKeywordVenue:state.Side(actor).positionKeywordVenue=true; break;
                case CardEffect.StrongestColumns:
                    foreach(var group in FieldCards(actor).GroupBy(c=>TileOf(c)%5)){
                        var c=group.OrderByDescending(x=>x.currentInfluence).ThenBy(x=>x.instanceId,StringComparer.Ordinal).First();
                        ChangeInfluence(c,2); GainKeywords(c,RandomKeyword());
                    }
                    break;
                default:StartChoice(card,tile,EffectChoice.Extended); break;
            }
            Discard(card);
        }

        private void RefreshExtendedChoice(PendingCardEffect p){
            var allies=FieldCards(p.actor).ToArray();
            switch(p.effect){
                case CardEffect.RevealHandKeywords:
                    p.prompt="Reveal a performer in your hand to gain its keywords.";
                    p.cards.AddRange(state.Side(p.actor).hand.Where(c=>c.token || c.data.performer)); break;
                case CardEffect.DiscardAndCombine:
                    if(p.stage==0){
                        p.prompt="Discard a performer from your hand.";
                        p.cards.AddRange(state.Side(p.actor).hand.Where(c=>c.token || c.data.performer));
                    }else{
                        p.prompt="Place the 3-influence token with both performers' keywords.";
                        p.tiles.AddRange(Enumerable.Range(0,25).Where(i=>CanPlace(p.actor,i%5,i/5) &&
                            (Performer(state.tiles[i],p.actor)==null || StageShare(p.created) && Assistant(state.tiles[i],p.actor)==null)));
                    }
                    break;
                case CardEffect.OtherColumnToken:
                    p.prompt="Place a 1-influence Nimble token in another column on your side.";
                    p.tiles.AddRange(Enumerable.Range(0,25).Where(i=>i%5!=p.sourceTile%5 && (p.actor==0 ? i%5<2 : i%5>2) && Performer(state.tiles[i],p.actor)==null)); break;
                case CardEffect.DisplaceContested:
                    p.prompt="Move the opposing performers to an empty tile in an adjacent row.";
                    if(Contested(p.sourceTile)) p.tiles.AddRange(Enumerable.Range(0,25).Where(i=>i%5==p.sourceTile%5 && Math.Abs(i/5-p.sourceTile/5)==1 && !AtTile(i).Any()));
                    break;
                case CardEffect.ColumnMoveAbility:
                    p.prompt=p.selected==null ? "Choose an ally to move within its column." : "Choose an available tile in this performer's column.";
                    if(p.selected==null) p.cards.AddRange(allies.Where(c=>Enumerable.Range(0,25).Any(i=>i%5==TileOf(c)%5 && CanRelocate(c,i))));
                    else p.tiles.AddRange(Enumerable.Range(0,25).Where(i=>i%5==TileOf(p.selected)%5 && CanRelocate(p.selected,i)));
                    break;
                case CardEffect.RowInfluence:
                    p.prompt="Choose an ally in the selected row to give allies +2, or an enemy to give enemies -2.";
                    p.cards.AddRange(FieldCards(0).Concat(FieldCards(1)).Where(c=>TileOf(c)/5==p.sourceTile/5 && !Protected(c,p.actor))); break;
                case CardEffect.FormationBonus:
                    if(p.stage==0 && !allies.Any(c=>allies.Any(o=>o!=c && TileOf(o)/5==TileOf(c)/5))) p.stage=2;
                    bool row=p.stage<2;
                    p.prompt=(p.selected==null ? "Choose the first ally" : "Choose the second ally")+
                        (row ? " in the same row: Protected 1 this round and refill 1 energy." : " in the same column: +1 influence this round and draw 1.");
                    p.cards.AddRange(allies.Where(c=>p.selected==null ? allies.Any(o=>o!=c && (row ? TileOf(o)/5==TileOf(c)/5 : TileOf(o)%5==TileOf(c)%5)) :
                        c!=p.selected && (row ? TileOf(c)/5==TileOf(p.selected)/5 : TileOf(c)%5==TileOf(p.selected)%5))); break;
                default:
                    if(p.selected==null){
                        p.prompt="Choose the performer to receive this effect.";
                        p.cards.AddRange(ExtendedInitialTargets(p.source,p.sourceTile));
                    }else if(p.effect==CardEffect.ShareAdjacentKeywords){
                        p.prompt="Choose an adjacent ally. Both gain +1 influence and share keywords.";
                        p.cards.AddRange(allies.Where(c=>Adjacent(TileOf(c),TileOf(p.selected))));
                    }else if(p.effect==CardEffect.SwapAllies){
                        p.prompt="Choose the second ally to swap positions with."; p.cards.AddRange(allies.Where(c=>CanSwap(p.selected,c)));
                    }else{
                        p.prompt="Choose an adjacent destination tile."; p.tiles.AddRange(AdjacentDestinations(p.selected));
                    }
                    break;
            }
        }

        private void ResolveExtendedChoice(PendingCardEffect p,CardState chosen,int tile){
            switch(p.effect){
                case CardEffect.RevealHandKeywords:
                    Reveal(new[] {chosen},"Revealed from hand"); GainKeywords(p.source,PerformerKeywords.Of(chosen)); break;
                case CardEffect.DiscardAndCombine:
                    if(p.stage==0){
                        var keywords=PerformerKeywords.Of(chosen); int cost=chosen.token ? chosen.tokenCost : chosen.data.cost;
                        state.Side(p.actor).hand.Remove(chosen); DiscardFromHand(chosen);
                        var random=CreateRandomCard(p.actor,c=>c.performer && c.cost==cost);
                        if(random!=null) keywords.Include(PerformerKeywords.Of(random));
                        p.created=NewToken(p.source,3); p.created.tokenKeywords=keywords;
                        if(keywords.protection>0) CardInfluence.Protect(p.created,keywords.protection);
                        p.stage=1; RefreshChoice(); return;
                    }
                    PutPerformer(p.created,tile); placedTurns[p.created.instanceId]=turnNumber; break;
                case CardEffect.OtherColumnToken:
                    var token=NewToken(p.source,1); token.tokenKeywords.nimble=true;
                    PutPerformer(token,tile); placedTurns[token.instanceId]=turnNumber; break;
                case CardEffect.DisplaceContested: MoveWithPartner(p.sourceTile,tile,1-p.actor); break;
                case CardEffect.RowInfluence:
                    foreach(var c in FieldCards(chosen.ownerId).Where(c=>TileOf(c)/5==p.sourceTile/5 && !Protected(c,p.actor)).ToArray()) ChangeInfluence(c,c.ownerId==p.actor ? 2 : -2);
                    break;
                case CardEffect.GrantProtected: CardInfluence.Protect(chosen,2); GainKeywords(chosen,new PerformerKeywords {protection=2}); break;
                case CardEffect.ReturnAndCopy:
                    int from=TileOf(chosen); var copy=chosen.token ? NewToken(chosen,chosen.tokenInfluence,chosen.tokenCost) : MakeCard(chosen.data,p.actor);
                    if(chosen.token){ copy.ownerId=p.actor; copy.tokenKeywords=chosen.tokenKeywords; }
                    MarkCreated(copy); Detach(chosen); placedTurns.Remove(chosen.instanceId); StageDeparture(chosen,from,false);
                    ResetReturnedCard(chosen); GiveCard(chosen.ownerId,chosen); GiveCard(p.actor,copy); break;
                default:
                    if(p.selected==null){ p.selected=chosen; RefreshChoice(); return; }
                    if(p.effect==CardEffect.ShareAdjacentKeywords){
                        var keys=PerformerKeywords.Of(p.selected); keys.Include(PerformerKeywords.Of(chosen));
                        GainKeywords(p.selected,keys); GainKeywords(chosen,keys); ChangeInfluence(p.selected,1); ChangeInfluence(chosen,1);
                    }else if(p.effect==CardEffect.FormationBonus){
                        if(p.stage<2){
                            CardInfluence.Protect(p.selected,1,state.roundNumber); CardInfluence.Protect(chosen,1,state.roundNumber);
                            state.Side(p.actor).energy=Math.Min(8,state.Side(p.actor).energy+1);
                            p.stage=2; p.selected=null; RefreshChoice(); return;
                        }
                        ChangeInfluence(p.selected,1,true); ChangeInfluence(chosen,1,true); DrawCards(p.actor,1);
                    }else if(p.effect==CardEffect.SwapAllies){
                        int a=TileOf(p.selected),b=TileOf(chosen); Detach(p.selected); Detach(chosen);
                        PutPerformer(p.selected,b); PutPerformer(chosen,a); PerformerEntered(p.selected,b); PerformerEntered(chosen,a);
                    }else{
                        Relocate(p.selected,tile);
                        if(p.effect==CardEffect.MoveAndAdjust) ChangeInfluence(p.selected,p.selected.ownerId==p.actor ? 1 : -1);
                        if(p.effect==CardEffect.MoveAndCreate) CreateRandomCard(p.actor,c=>c.cardClass==p.selected.data.cardClass,Contested(tile) ? 1 : 0);
                    }
                    break;
            }
            EndChoice();
        }

        private void RefreshPositionKeywords(CardState[] all){
            foreach(var c in all){
                c.sharedKeywords=default;
                if(state.Side(c.ownerId).positionKeywordVenue){
                    c.sharedKeywords.encore=TileOf(c)%5==2;
                    c.sharedKeywords.nimble=TileOf(c)%5==0 || TileOf(c)%5==4;
                }
            }
            for(int actor=0;actor<2;actor++){
                var allies=all.Where(c=>c.ownerId==actor).ToArray();
                if(!allies.Any(c=>HasEffect(c,CardEffect.ShareAllKeywords))) continue;
                var union=new PerformerKeywords(); foreach(var c in allies) union.Include(PerformerKeywords.Of(c));
                foreach(var c in allies) c.sharedKeywords.Include(union);
            }
            foreach(var c in all){
                if(!c.silenced && c.sharedKeywords.protection>c.sharedProtectionLevel && c.sharedKeywords.protection>PerformerKeywords.Of(c,false).protection)
                    CardInfluence.Protect(c,c.sharedKeywords.protection);
                c.sharedProtectionLevel=c.sharedKeywords.protection;
                bool alone=HasEffect(c,CardEffect.LoneCenterProtected) && TileOf(c)%5==2 && all.Count(o=>TileOf(o)%5==2)==1;
                if(alone && !c.soloProtectionCondition) CardInfluence.Protect(c,2);
                c.soloProtectionCondition=alone;
            }
        }

        private void StartMeikoRound(){
            foreach(var c in FieldCards(0).Concat(FieldCards(1)).Where(c=>HasEffect(c,CardEffect.WanderAndToken)).ToArray()){
                int from=TileOf(c);
                var destinations=Enumerable.Range(0,25).Where(i=>i%5==from%5 && CanRelocate(c,i)).ToArray();
                if(destinations.Length==0) continue;
                Relocate(c,destinations[UnityEngine.Random.Range(0,destinations.Length)]);
                var token=NewToken(c,UnityEngine.Random.Range(0,3)); token.tokenKeywords.stageShare=true;
                if(Performer(state.tiles[from],c.ownerId)==null || Assistant(state.tiles[from],c.ownerId)==null){
                    PutPerformer(token,from); placedTurns[token.instanceId]=turnNumber;
                }
            }
        }
    }
}
