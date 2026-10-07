using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VocaloidTCG.BoardUI
{
    public sealed partial class GameplayBoardBridge
    {
        private readonly List<CardState>[] openingCards = { new List<CardState>(), new List<CardState>() };
        public bool OpeningRedrawSubmitted { get; private set; }
        public bool OpeningPending => state != null && state.phase == RoundPhase.Mulligan && !IsPuzzle;
        public IReadOnlyList<CardState> OpeningCards(int actor) => openingCards[actor];

        private void PrepareOpeningHand(){
            foreach(var cards in openingCards) cards.Clear();
            while(drawPresentations.Count > 0){
                var draw = drawPresentations.Dequeue();
                if(!draw.discarded) openingCards[draw.card.ownerId].Add(draw.card);
            }
            
            OpeningRedrawSubmitted = false;
            state.phase = RoundPhase.Mulligan; state.inputAllowed = false;
            state.activePlayerId = state.roundStarterId;
        }

        public List<string> ChooseOpponentRedraw(){
            if(IsOnline) return new List<string>();
            int enemy = 1 - state.localPlayerId;
            return openingCards[enemy].Where(c => c.currentCost > 3)
                .OrderByDescending(c => c.currentCost).Take(Mathf.Min(2, decks[enemy].Count))
                .Select(c => c.instanceId).ToList();
        }

        public bool SubmitOpeningRedraw(IEnumerable<string> playerIds, IEnumerable<string> enemyIds){
            if(IsOnline) return online.Command("redraw", default(BoardAction), (playerIds ?? Enumerable.Empty<string>()).ToArray());
            if(!OpeningPending || OpeningRedrawSubmitted || IsPaused) return false;
            var local = new HashSet<string>(playerIds ?? Enumerable.Empty<string>());
            var enemy = new HashSet<string>(enemyIds ?? Enumerable.Empty<string>());
            int actor = state.localPlayerId;
            
            if(local.Any(id => !openingCards[actor].Any(c => c.instanceId == id)) ||
                enemy.Any(id => !openingCards[1 - actor].Any(c => c.instanceId == id))) return false;
            if(local.Count > decks[actor].Count || enemy.Count > decks[1 - actor].Count) return false;
            ExchangeOpeningCards(actor, local);
            ExchangeOpeningCards(1 - actor, enemy);
            OpeningRedrawSubmitted = true;
            Publish();
            return true;
        }

        private void ExchangeOpeningCards(int actor, HashSet<string> selected){
            if(selected.Count == 0) return;
            var returned = new List<CardState>();
            var hand = state.Side(actor).hand;
            
            for(int i = 0; i < openingCards[actor].Count; i++){
                var old = openingCards[actor][i];
                if(!selected.Contains(old.instanceId)) continue;
                var replacement = decks[actor].Dequeue();
                hand[hand.IndexOf(old)] = replacement;
                openingCards[actor][i] = replacement;
                returned.Add(old);
            }
            
            var remaining = decks[actor].Concat(returned).ToList();
            for(int i = remaining.Count - 1; i > 0; i--){
                int other = Random.Range(0, i + 1);
                var card = remaining[i]; remaining[i] = remaining[other]; remaining[other] = card;
            }
            
            decks[actor].Clear();
            foreach(var card in remaining) decks[actor].Enqueue(card);
            state.Side(actor).deckCount = decks[actor].Count;
            state.Side(actor).hiddenHandCount = hand.Count;
        }

        public bool CompleteOpening(){
            if(IsOnline) return online.Command("openingDone", default(BoardAction));
            if(!OpeningPending || !OpeningRedrawSubmitted || IsPaused) return false;
            state.phase = RoundPhase.Preparation; state.inputAllowed = true;
            BeginTurn(state.roundStarterId);
            Publish();
            return true;
        }
    }
}
