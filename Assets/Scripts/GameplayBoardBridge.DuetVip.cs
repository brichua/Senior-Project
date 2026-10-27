using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VocaloidTCG.BoardUI
{
    public sealed partial class GameplayBoardBridge
    {
        private CardState influenceSource;
        private static bool IsDuetEffect(CardEffect effect) => effect >= CardEffect.RuinToken;
        private IEnumerable<CardState> Enemies(int actor) => FieldCards(1-actor).Where(c=>!Protected(c,actor));
        private IEnumerable<int> OpenTokenTiles(int actor) => Enumerable.Range(0,25).Where(i=>Performer(state.tiles[i],actor)==null && CanPlace(actor,i%5,i/5));
        private static IEnumerable<PerformerKeywords> SingleKeywords(PerformerKeywords k){
            if(k.stageShare) yield return new PerformerKeywords {stageShare=true};
            if(k.encore) yield return new PerformerKeywords {encore=true};
            if(k.nimble) yield return new PerformerKeywords {nimble=true};
            if(k.regen) yield return new PerformerKeywords {regen=true};
            if(k.harmonize>0) yield return new PerformerKeywords {harmonize=k.harmonize};
            if(k.soloist>0) yield return new PerformerKeywords {soloist=k.soloist};
            if(k.protection>0) yield return new PerformerKeywords {protection=k.protection};
        }

        private void TemporaryKeywords(CardState card,PerformerKeywords k){
            card.roundKeywords.Include(k);
            if(k.protection>0) CardInfluence.Protect(card,k.protection,state.roundNumber);
        }
        
        private void RandomKeywords(CardState card,int count,bool temporary){
            var choices=new List<PerformerKeywords> {
                new PerformerKeywords {stageShare=true},new PerformerKeywords {encore=true},new PerformerKeywords {nimble=true},
                new PerformerKeywords {regen=true},new PerformerKeywords {harmonize=1},new PerformerKeywords {soloist=1},new PerformerKeywords {protection=1}
            };
            for(int i=0;i<count && choices.Count>0;i++){
                int index=UnityEngine.Random.Range(0,choices.Count); var k=choices[index]; choices.RemoveAt(index);
                if(temporary) TemporaryKeywords(card,k); else GainKeywords(card,k);
            }
        }
        
        private void KeywordChoices(PendingCardEffect p,PerformerKeywords keywords){
            p.options.Clear();
            foreach(var k in SingleKeywords(keywords)){
                var option=NewToken(p.source,0); option.tokenKeywords=k; p.options.Add(option);
            }
        }
        
        private void RewardTile(int actor,int tile,int score=0,int draws=0,bool contested=false,string performer=null){
            state.tileWinRewards.Add(new TileWinReward {actor=actor,tile=tile,score=score,draws=draws,contestedOnly=contested,performerId=performer});
        }
        
        private void QueueDuet(CardState source,int tile,int stage=0,CardState selected=null,PerformerKeywords keywords=default(PerformerKeywords),int amount=0){
            var p=new PendingCardEffect {actor=source.ownerId,source=source,effect=source.data.effect,sourceTile=tile,
                choice=EffectChoice.Duet,remaining=1,stage=stage,selected=selected,amount=amount};
            if(keywords.Count>0) KeywordChoices(p,keywords);
            state.queuedEffects.Add(p);
        }
        
        private bool DuetSpellTarget(CardState spell,int tile){
            int actor=spell.ownerId;
            switch(spell.data.effect){
                case CardEffect.SelfishPrincess:return state.Side(actor).hand.Any(c=>c!=spell && (c.token || c.data.performer)) && OpenTokenTiles(actor).Any();
                case CardEffect.LastOfMe:return FieldCards(actor).Any(c=>Contested(TileOf(c)));
                case CardEffect.Attakaito:case CardEffect.Hibikase:return FieldCards(actor).Any();
                case CardEffect.PerfectCrime:return Contested(tile) && Enemies(actor).Any(c=>TileOf(c)==tile);
                default:return DuetInitialTargets(spell,tile).Any();
            }
        }
        
        private IEnumerable<CardState> DuetInitialTargets(CardState spell,int tile){
            int actor=spell.ownerId;
            return AtTile(tile).Where(c=>{
                bool ally=c.ownerId==actor;
                switch(spell.data.effect){
                    case CardEffect.Fragile:return !ally && !Protected(c,actor) && AdjacentDestinations(c).Any();
                    case CardEffect.EgoRock:return !ally && !Protected(c,actor);
                    case CardEffect.StealKeyword:return !ally && !Protected(c,actor) && PerformerKeywords.Of(c).Count>0 && FieldCards(actor).Any();
                    case CardEffect.Cendrillon:case CardEffect.OnTheRocks:case CardEffect.Quarter:return ally && AdjacentDestinations(c).Any();
                    case CardEffect.Division:return ally && AdjacentDestinations(c).Any(i=>Performer(state.tiles[i],1-actor)!=null);
                    case CardEffect.Blender:return ally && FieldCards(actor).Any(o=>o!=c && Adjacent(tile,TileOf(o)));
                    case CardEffect.ColorfulMelody:return ally && Enumerable.Range(0,25).Any(i=>Adjacent(tile,i) && AtTile(i).Any(o=>o!=c));
                    case CardEffect.DropPopCandy:return ally && Contested(tile);
                    case CardEffect.Futairo:return ally && tile%5==2;
                    case CardEffect.Pallucination:return ally && Enemies(actor).Any(o=>Adjacent(tile,TileOf(o)) && AdjacentDestinations(o).Any());
                    case CardEffect.Gemini:case CardEffect.Magnet:case CardEffect.Wonderland:case CardEffect.FairyTaled:case CardEffect.HappyHalloween:return ally;
                    default:return false;
                }
            });
        }
        
        private void PlayDuetSpell(CardState card,int tile,CardState target=null){
            int actor=card.ownerId;
            switch(card.data.effect){
                case CardEffect.Attakaito:
                    foreach(var group in FieldCards(actor).GroupBy(c=>TileOf(c)%5)){
                        var targets=group.ToArray(); int strongest=targets.Max(c=>c.currentInfluence);
                        var tied=targets.Where(c=>c.currentInfluence==strongest).ToArray();
                        if(tied.Length==1) ChangeInfluence(tied[0],2);
                        else { QueueDuet(card,tile,90); state.queuedEffects.Last().eligibleIds=tied.Select(c=>c.instanceId).ToList(); }
                    }
                    break;
                case CardEffect.LastOfMe:
                    foreach(var c in FieldCards(actor).Where(c=>Contested(TileOf(c))).ToArray()) ChangeInfluence(c,2,true);
                    state.Side(actor).contestedWinBonus++; break;
                case CardEffect.PerfectCrime:
                    foreach(var c in Enemies(actor).Where(c=>TileOf(c)==tile).ToArray()) ChangeInfluence(c,-2,true);
                    RewardTile(actor,tile,1); break;
                case CardEffect.Hibikase:
                    foreach(var c in FieldCards(actor).ToArray()) ChangeInfluence(c,1,true);
                    StartChoice(card,tile,EffectChoice.Duet); break;
                default:
                    StartChoice(card,tile,EffectChoice.Duet);
                    var p=state.pendingEffect;
                    if(target!=null && p!=null && p.source==card && p.cards.Contains(target)) ResolveDuetChoice(p,target,tile);
                    break;
            }
            Discard(card);
        }
        
        private void RefreshDuetChoice(PendingCardEffect p){
            var allies=FieldCards(p.actor).ToArray(); var enemies=Enemies(p.actor).ToArray();
            p.prompt="Choose a performer for "+p.source.data.cardName+".";
            if(p.effect==CardEffect.Quarter) p.optional=true;
            if(p.options.Count>0){ p.prompt="Choose one keyword."; p.cards.AddRange(p.options); return; }
            if(p.stage>=90){ p.prompt=p.stage==90 ? "Choose a strongest ally in this column to gain +2 influence." : "Choose an opposing performer on this tile."; p.cards.AddRange(allies.Concat(enemies).Where(c=>p.eligibleIds.Contains(c.instanceId))); return; }
            switch(p.effect){
                case CardEffect.FruitKiller:case CardEffect.EgoRenegade:p.cards.AddRange(enemies); return;
                case CardEffect.DyingBreath:
                    if(p.selected==null) p.cards.AddRange(allies.Where(c=>c!=p.source && Enumerable.Range(0,25).Any(i=>(i/5==TileOf(c)/5 || i%5==TileOf(c)%5) && CanRelocate(c,i))));
                    else p.tiles.AddRange(Enumerable.Range(0,25).Where(i=>(i/5==TileOf(p.selected)/5 || i%5==TileOf(p.selected)%5) && CanRelocate(p.selected,i)));
                    return;
                case CardEffect.Void:
                    p.optional=true; p.prompt="You may move a 1-influence token into the previous tile.";
                    p.cards.AddRange(allies.Where(c=>c.token && c.currentInfluence==1 && CanRelocate(c,p.sourceTile))); return;
                case CardEffect.Doubleganger:case CardEffect.UnbalanceHero:case CardEffect.Ohedo:
                    p.cards.AddRange(allies.Where(c=>c!=p.source && (Adjacent(p.sourceTile,TileOf(c)) || p.effect==CardEffect.Ohedo && TileOf(c)==p.sourceTile))); return;
                case CardEffect.Projection:p.cards.AddRange(enemies.Where(c=>TileOf(c)%5==p.sourceTile%5)); return;
                case CardEffect.Shinjinrui:case CardEffect.SelfishPrincess:
                    if(p.effect==CardEffect.SelfishPrincess && p.stage==0){ p.prompt="Discard a performer from your hand."; p.cards.AddRange(state.Side(p.actor).hand.Where(c=>c.token || c.data.performer)); }
                    else { p.prompt="Choose an available tile for the token."; p.tiles.AddRange(OpenTokenTiles(p.actor).Where(i=>p.effect!=CardEffect.Shinjinrui || Adjacent(p.sourceTile,i))); }
                    return;
                case CardEffect.Gemini:if(p.stage==1){ p.cards.AddRange(enemies.Where(c=>c.currentInfluence<=p.amount)); return; } break;
                case CardEffect.Wonderland:if(p.stage==1){ p.cards.AddRange(enemies.Where(c=>Adjacent(p.sourceTile,TileOf(c)))); return; } break;
                case CardEffect.Pallucination:if(p.stage==1){ p.cards.AddRange(enemies.Where(c=>Adjacent(p.sourceTile,TileOf(c)) && AdjacentDestinations(c).Any())); return; } break;
                case CardEffect.StealKeyword:if(p.stage==2){ p.cards.AddRange(allies); return; } break;
            }
            if(p.selected==null){
                if(p.effect==CardEffect.Hibikase) p.cards.AddRange(allies);
                else if(p.effect==CardEffect.Quarter) p.cards.AddRange(allies.Where(c=>!p.eligibleIds.Contains(c.instanceId) && AdjacentDestinations(c).Any()));
                else p.cards.AddRange(DuetInitialTargets(p.source,p.sourceTile));
                return;
            }
            switch(p.effect){
                case CardEffect.Cendrillon:if(p.stage==0){ p.tiles.AddRange(AdjacentDestinations(p.selected)); break; } goto case CardEffect.Blender;
                case CardEffect.Blender:
                    p.cards.AddRange(allies.Where(c=>c!=p.selected && Adjacent(TileOf(p.selected),TileOf(c)))); break;
                case CardEffect.ColorfulMelody:case CardEffect.Hibikase:
                    p.prompt="Choose an adjacent occupied tile for the influence this round.";
                    p.tiles.AddRange(Enumerable.Range(0,25).Where(i=>Adjacent(p.sourceTile,i) && AtTile(i).Any())); break;
                default:
                    p.prompt="Choose a highlighted destination tile.";
                    p.tiles.AddRange(AdjacentDestinations(p.selected).Where(i=>p.effect!=CardEffect.Division || Performer(state.tiles[i],1-p.actor)!=null)); break;
            }
        }
        
        private void ResolveDuetChoice(PendingCardEffect p,CardState chosen,int tile){
            var previous=influenceSource; influenceSource=p.source;
            try { ResolveDuetChoiceCore(p,chosen,tile); } finally { influenceSource=previous; }
        }
        
        private void ResolveDuetChoiceCore(PendingCardEffect p,CardState chosen,int tile){
            if(p.stage>=90){ ChangeInfluence(chosen,p.stage==90 ? 2 : p.amount,p.stage==92); EndChoice(); return; }
            if(p.options.Count>0){
                p.keyword=chosen.tokenKeywords; p.options.Clear();
                if(p.effect==CardEffect.StealKeyword){
                    p.selected.suppressedKeywords.Include(p.keyword);
                    if(p.keyword.protection>0) CardInfluence.BreakShield(p.selected);
                    p.stage=2; p.selected=null; RefreshChoice(); return;
                }
                if(p.effect==CardEffect.FruitKiller){ GainKeywords(p.source,p.keyword); ChangeInfluence(p.selected,-1,true); }
                else if(p.effect==CardEffect.YoungYouOut) GainKeywords(p.source,p.keyword);
                else TemporaryKeywords(p.source,p.keyword);
                EndChoice(); return;
            }
            switch(p.effect){
                case CardEffect.FruitKiller:
                    p.selected=chosen; KeywordChoices(p,PerformerKeywords.Of(chosen));
                    if(p.options.Count==0){ ChangeInfluence(chosen,-1,true); EndChoice(); } else RefreshChoice(); return;
                case CardEffect.EgoRenegade:
                    ChangeInfluence(chosen,-2,true); if(chosen.currentInfluence<=0) state.Side(p.actor).energy=Math.Min(8,state.Side(p.actor).energy+1); break;
                case CardEffect.EgoRock:
                    chosen.silenced=true; chosen.silenceRound=state.roundNumber; ChangeInfluence(chosen,-3);
                    if(chosen.currentInfluence<=0) DrawCards(p.actor,1); break;
                case CardEffect.StealKeyword:
                    if(p.stage==0){ p.selected=chosen; KeywordChoices(p,PerformerKeywords.Of(chosen)); p.stage=1; RefreshChoice(); return; }
                    TemporaryKeywords(chosen,p.keyword); CardInfluence.Protect(chosen,1,state.roundNumber); break;
                case CardEffect.FairyTaled:
                    var copy=NewToken(chosen,chosen.token ? chosen.tokenInfluence : chosen.data.influence,Math.Max(0,(chosen.token ? chosen.tokenCost : chosen.data.cost)-1));
                    copy.ownerId=p.actor; RandomKeywords(copy,2,false); YoungKeywordTrigger(copy); GiveCard(p.actor,copy); break;
                case CardEffect.Futairo:
                    RandomKeywords(chosen,1,true); RewardTile(p.actor,-1,draws:1,contested:true,performer:chosen.instanceId); break;
                case CardEffect.Magnet:
                    ChangeInfluence(chosen,2,true);
                    foreach(var c in FieldCards(p.actor).Where(c=>Adjacent(TileOf(chosen),TileOf(c))).ToArray()) ChangeInfluence(c,1,true);
                    if(TileOf(chosen)%5==2) RewardTile(p.actor,TileOf(chosen),1,performer:chosen.instanceId); break;
                case CardEffect.Doubleganger:case CardEffect.Ohedo:ChangeInfluence(chosen,p.effect==CardEffect.Doubleganger ? 2 : 1,true); break;
                case CardEffect.UnbalanceHero:ChangeInfluence(chosen,1); break;
                case CardEffect.Projection:ChangeInfluence(chosen,-1,true); break;
                case CardEffect.Void:
                    Relocate(chosen,p.sourceTile); p.source.linkedTokenId=chosen.instanceId; break;
                case CardEffect.Shinjinrui:CreateStageToken(p.source,tile,1); break;
                case CardEffect.SelfishPrincess:
                    if(p.stage==0){
                        int cost=chosen.token ? chosen.tokenCost : chosen.data.cost;
                        state.Side(p.actor).hand.Remove(chosen); DiscardFromHand(chosen);
                        CreateRandomCard(p.actor,c=>c.performer && c.cost==cost); p.stage=1; RefreshChoice(); return;
                    }
                    CreateStageToken(p.source,tile,1); break;
                case CardEffect.DropPopCandy:case CardEffect.HappyHalloween:case CardEffect.Gemini:case CardEffect.Pallucination:
                    if(p.stage==0){
                        p.sourceTile=TileOf(chosen); p.amount=chosen.token ? chosen.tokenInfluence : chosen.data.influence;
                        RemoveSingle(chosen);
                        if(p.effect==CardEffect.DropPopCandy){ state.Side(p.actor).score++; CreateStageToken(p.source,p.sourceTile,2); }
                        else if(p.effect==CardEffect.HappyHalloween){ DrawCards(p.actor,2); if(DiscardPerformers(p.actor)>=4) CreateStageToken(p.source,p.sourceTile,3); }
                        else { p.stage=1; RefreshChoice(); return; }
                    }else if(p.effect==CardEffect.Gemini) ChangeInfluence(chosen,-3);
                    else if(p.selected==null){ p.selected=chosen; p.stage=2; RefreshChoice(); return; }
                    else { Relocate(p.selected,tile); ChangeInfluence(p.selected,-1,true); }
                    break;
                case CardEffect.Wonderland:
                    if(p.stage==0){ ChangeInfluence(chosen,2,true); p.sourceTile=TileOf(chosen); p.stage=1; RefreshChoice(); return; }
                    ChangeInfluence(chosen,-2,true); break;
                case CardEffect.ColorfulMelody:case CardEffect.Hibikase:
                    if(p.selected==null){
                        p.selected=chosen; p.sourceTile=TileOf(chosen); p.amount=chosen.currentInfluence/(p.effect==CardEffect.Hibikase ? 2 : 1);
                        if(p.effect==CardEffect.ColorfulMelody) RemoveSingle(chosen);
                        RefreshChoice(); return;
                    }
                    (p.actor==0 ? state.roundTileBonus0 : state.roundTileBonus1)[tile]+=p.amount;
                    if(p.effect==CardEffect.ColorfulMelody) DrawCards(p.actor,1); break;
                case CardEffect.Blender:
                    if(p.selected==null){ p.selected=chosen; RefreshChoice(); return; }
                    var keys=PerformerKeywords.Of(p.selected); keys.Include(PerformerKeywords.Of(chosen));
                    TemporaryKeywords(p.selected,keys); TemporaryKeywords(chosen,keys);
                    ChangeInfluence(p.selected,1); ChangeInfluence(chosen,1); break;
                default:
                    if(p.selected==null){ p.selected=chosen; RefreshChoice(); return; }
                    int from=TileOf(p.selected);
                    if(p.effect==CardEffect.Cendrillon && p.stage==1){ ChangeInfluence(chosen,1,true); break; }
                    Relocate(p.selected,tile);
                    switch(p.effect){
                        case CardEffect.DyingBreath:break;
                        case CardEffect.Cendrillon:
                            ChangeInfluence(p.selected,1,true); p.stage=1; RefreshChoice(); return;
                        case CardEffect.Division:ChangeInfluence(p.selected,2,true); break;
                        case CardEffect.Fragile:ChangeInfluence(p.selected,-2,true); p.selected.rootedRound=state.roundNumber; break;
                        case CardEffect.OnTheRocks:RandomKeywords(p.selected,1,true); if(from%5!=tile%5) DrawCards(p.actor,1); break;
                        case CardEffect.Quarter:
                            p.eligibleIds.Add(p.selected.instanceId); p.selected=null; p.stage++;
                            if(p.stage<3){ p.optional=true; RefreshChoice(); return; } break;
                    }
                    break;
            }
            EndChoice();
        }
        
        private void FinishDuetChoice(PendingCardEffect p){
            if(p.effect!=CardEffect.Quarter) return;
            var moved=FieldCards(p.actor).Where(c=>p.eligibleIds.Contains(c.instanceId)).ToArray();
            int amount=Math.Min(2,moved.Select(c=>TileOf(c)%5).Distinct().Count());
            foreach(var c in moved) ChangeInfluence(c,amount,true);
        }
        
        private void DuetPerformerPlayed(CardState card,int tile){
            int actor=card.ownerId;
            DuetEntered(card,tile);
            if(FieldCards(actor).Any(c=>c!=card && HasEffect(c,CardEffect.DyingBreath))) ChangeInfluence(card,1,true);
            foreach(var rabbit in FieldCards(actor).Where(c=>c!=card && HasEffect(c,CardEffect.RabbitHole) && c.triggerRound!=state.roundNumber && Adjacent(tile,TileOf(c))).ToArray()){
                rabbit.triggerRound=state.roundNumber; ChangeInfluence(card,1); ChangeInfluence(rabbit,1,true);
            }
            YoungKeywordTrigger(card);
            if(!EffectsEnabled(card)) return;
            switch(card.data.effect){
                case CardEffect.FruitKiller:StartChoice(card,tile,EffectChoice.Duet); break;
                case CardEffect.UnbalanceHero:
                    QueueDuet(card,tile); QueueDuetEnemyHit(card,tile,-1); break;
                case CardEffect.Pygmalion:
                    if(Contested(tile)){ QueueDuetEnemyHit(card,tile,-1); RewardTile(actor,tile,1,performer:card.instanceId); } break;
            }
        }
        
        private void QueueDuetEnemyHit(CardState source,int tile,int amount){
            var targets=Enemies(source.ownerId).Where(c=>TileOf(c)==tile).ToArray();
            if(targets.Length==1) ChangeInfluence(targets[0],amount);
            else if(targets.Length>1){
                QueueDuet(source,tile,91,amount:amount); state.queuedEffects.Last().eligibleIds=targets.Select(c=>c.instanceId).ToList();
            }
        }
        
        private void YoungKeywordTrigger(CardState card){
            if(!card.token && !card.data.performer || PerformerKeywords.Of(card).Count==0) return;
            foreach(var young in FieldCards(card.ownerId).Where(c=>c!=card && HasEffect(c,CardEffect.YoungYouOut) && c.triggerRound!=state.roundNumber).ToArray()){
                young.triggerRound=state.roundNumber; QueueDuet(young,TileOf(young),keywords:PerformerKeywords.Of(card));
            }
        }
        
        private void DuetEntered(CardState card,int tile){
            if(HasEffect(card,CardEffect.ColorfulSexy) && tile%5==2){
                if(PerformerKeywords.Of(card).Count>0) ChangeInfluence(card,1);
                RandomKeywords(card,1,false);
            }
        }
        
        private void DuetMoved(CardState card,int from,int to){
            if(from==to) return;
            if(from%5!=2 && to%5==2) DuetEntered(card,to);
            if(from%5!=to%5 && !card.visitedColumns.Contains(to%5) && FieldCards(card.ownerId).Any(c=>HasEffect(c,CardEffect.DyingBreath))) ChangeInfluence(card,1,true);
            if(!card.visitedColumns.Contains(to%5)) card.visitedColumns.Add(to%5);
            if(!EffectsEnabled(card)) return;
            switch(card.data.effect){
                case CardEffect.WintryWinds:
                    if(card.triggerRound!=state.roundNumber){ card.triggerRound=state.roundNumber; RandomKeywords(card,1,true); } break;
                case CardEffect.Luoyang:if(from%5!=2 && to%5==2) ChangeInfluence(card,1,true); break;
                case CardEffect.Ohedo:QueueDuet(card,to); break;
                case CardEffect.Void:QueueDuet(card,from); break;
                case CardEffect.Projection:if(from%5!=to%5) QueueDuet(card,to); break;
            }
        }
        
        private void DuetDeparture(CardState card,int tile,bool contested){
            int actor=card.ownerId;
            foreach(var other in FieldCards(actor).Where(c=>c!=card).ToArray()){
                if(HasEffect(other,CardEffect.LoveHate)){
                    QueueDuet(other,TileOf(other),92,amount:-2);
                    state.queuedEffects.Last().eligibleIds=Enemies(actor).Where(c=>TileOf(c)==TileOf(other)).Select(c=>c.instanceId).ToList();
                }
                if(HasEffect(other,CardEffect.Shinjinrui)){
                    if(card.token) ChangeInfluence(other,1);
                    if(other.triggerRound!=state.roundNumber){ other.triggerRound=state.roundNumber; QueueDuet(other,TileOf(other)); }
                }
            }
            if(!EffectsEnabled(card)) return;
            switch(card.data.effect){
                case CardEffect.RuinToken:if(contested) CreateStageToken(card,tile,1); break;
                case CardEffect.Doubleganger:QueueDuet(card,tile); break;
                case CardEffect.Leona:
                    var copy=NewToken(card,card.data.influence,Math.Max(0,card.data.cost-1)); GiveCard(actor,copy); break;
                case CardEffect.Void:
                    ChangeInfluence(FieldCards(actor).FirstOrDefault(c=>c.instanceId==card.linkedTokenId),1); break;
            }
        }
        
        private bool CanActivateDuet(CardState c){
            if(c.activatedRound==state.roundNumber) return false;
            if(HasEffect(c,CardEffect.EgoRenegade)) return Enemies(c.ownerId).Any();
            return HasEffect(c,CardEffect.DyingBreath) && FieldCards(c.ownerId).Any(a=>a!=c && Enumerable.Range(0,25).Any(i=>(i/5==TileOf(a)/5 || i%5==TileOf(a)%5) && CanRelocate(a,i)));
        }
        
        private void TrackKeywordAura(CardState card,CardState source,string[] previous){
            if(card==source || !HasEffect(card,CardEffect.Taoyuan) || card.keywordAuraSources.Contains(source.instanceId)) return;
            card.keywordAuraSources.Add(source.instanceId);
            if(!previous.Contains(source.instanceId)) QueueDuet(card,TileOf(card),keywords:PerformerKeywords.Of(source));
        }
        
        private int DuetAura(CardState c,CardState[] all,string[] previous){
            int tile=TileOf(c), bonus=HasEffect(c,CardEffect.Projection) && OpponentSide(c.ownerId,tile) ? 1 : 0;
            foreach(var source in all.Where(o=>o.ownerId==c.ownerId)){
                int before=bonus;
                if((HasEffect(source,CardEffect.Doubleganger) || HasEffect(source,CardEffect.Makeup) && Contested(TileOf(source))) && Adjacent(tile,TileOf(source))) bonus++;
                if(source!=c && HasEffect(source,CardEffect.YoungYouOut) && PerformerKeywords.Of(c).Count>=2) bonus++;
                if(bonus>before) TrackKeywordAura(c,source,previous);
            }
            return bonus;
        }
        
        private void ResolveDuetWin(int actor,int tile,bool contested,CardState[] winners){
            var side=state.Side(actor); int bonus=0, draws=0;
            foreach(var reward in state.tileWinRewards.Where(r=>r.actor==actor && (r.tile==tile || r.tile<0) && (!r.contestedOnly || contested) &&
                (string.IsNullOrEmpty(r.performerId) || winners.Any(c=>c.instanceId==r.performerId && c.noScoreRound!=state.roundNumber))).ToArray()){
                bonus+=reward.score; draws+=reward.draws;
            }
            if(contested){
                if(side.contestedWinUsedRound!=state.roundNumber){ bonus+=side.contestedWinBonus; side.contestedWinUsedRound=state.roundNumber; }
                if(tile%5==2) bonus+=winners.Count(c=>HasEffect(c,CardEffect.LucidDreamscape) && c.noScoreRound!=state.roundNumber);
            }
            side.score+=bonus; if(actor==0) roundScore0+=bonus; else roundScore1+=bonus;
            if(draws>0) DrawCards(actor,draws);
        }
        
        private void RestoreLucidOnScore(int actor,int previousScore){
            if(state.Side(actor).score<=previousScore) return;
            foreach(var lucid in FieldCards(actor).Where(c=>HasEffect(c,CardEffect.LucidDreamscape) && c.triggerRound!=state.roundNumber).ToArray()){
                lucid.triggerRound=state.roundNumber;
                int original=lucid.data.influence;
                if(!lucid.shieldActive && lucid.currentInfluence-lucid.auraInfluence-lucid.roundInfluence<original) ChangeInfluence(lucid,1);
            }
        }
        
        private void ClearDuetRound(){
            foreach(var c in FieldCards(0).Concat(FieldCards(1)).Concat(state.side0.hand).Concat(state.side1.hand).Concat(state.side0.discard).Concat(state.side1.discard)){
                c.roundKeywords=c.suppressedKeywords=default(PerformerKeywords); c.visitedColumns.Clear();
                if(TileOf(c)>=0) c.visitedColumns.Add(TileOf(c)%5);
                if(c.silenceRound==state.roundNumber){ c.silenced=false; c.silenceRound=-1; }
            }
            state.tileWinRewards.Clear(); state.side0.contestedWinBonus=state.side1.contestedWinBonus=0;
        }
    }
}
