using System.Collections.Generic;
using UnityEngine;

namespace VocaloidTCG.BoardUI
{
    public sealed class GameplayBoardBridge : BoardGameBridge
    {
        public SFXManager sfx;
        public const int MaxHandSize = 8;
        [Range(0, 1)] public int localPlayerId;
        [Tooltip("Full decks excluding opening card")]
        public CardData[] player0Cards, player1Cards;
        public CardData player0OpeningCard, player1OpeningCard;
        public LocalTurnClock clock;
        [Min(1)] public int winningScore = 50;
        [Min(0)] public float turnSeconds = 60;
        [Min(0)] public float endRoundDisplaySeconds = 2;
        public bool startAutomatically = true;
        public bool enableDebugLogs = true;

        private BoardSnapshot state;
        private int serial, turnNumber;
        private float roundWait;
        private bool deckExhausted;
        private readonly Queue<CardState>[] decks = { new Queue<CardState>(), new Queue<CardState>() };
        private readonly int[] lastMoveTurn = { -1, -1 };
        private readonly Dictionary<string, int> placedTurns = new Dictionary<string, int>();

        public override BoardSnapshot Snapshot { get { return state; } }
        public override float RemainingSeconds { get { return clock ? clock.RemainingSeconds : 0; } }
        public int TurnNumber { get { return turnNumber; } }
        public bool IsPaused { get { return clock && clock.Paused; } }

        private void Awake(){
            if(!clock) clock = gameObject.AddComponent<LocalTurnClock>();
            clock.Expired += OnTurnExpired;
            if(startAutomatically) StartMatch();
        }

        private void OnDestroy(){
            if(clock) clock.Expired -= OnTurnExpired;
        }

        private void OnDisable(){
            if(clock) clock.Stop();
        }

        private void OnEnable(){
            if(state != null && IsPlaying()) StartClock();
        }

        public void StartMatch(int firstPlayerId = -1){
            if(firstPlayerId < -1 || firstPlayerId > 1){ Log("Match start rejected: invalid starting player."); return; }
            if(clock){ clock.Stop(); clock.Paused = false; }
            serial = turnNumber = 0;
            deckExhausted = false;
            placedTurns.Clear();
            for(int i = 0; i < 2; i++) lastMoveTurn[i] = -1;
            int starter = firstPlayerId < 0 ? Random.Range(0, 2) : firstPlayerId;
            state = new BoardSnapshot {
                localPlayerId = Mathf.Clamp(localPlayerId, 0, 1), roundStarterId = starter,
                winScore = Mathf.Max(1, winningScore)
            };
            for(int i = 0; i < 25; i++) state.tiles[i] = new TileState();
            Log("Match started. Player " + starter + " goes first.");
            BuildDeck(player0Cards, 0); BuildDeck(player1Cards, 1);
            AddOpeningCard(player0OpeningCard, 0); AddOpeningCard(player1OpeningCard, 1);
            DrawCards(0, 3); DrawCards(1, 3);
            RefillRoundEnergy();
            BeginTurn(starter); Publish();
        }

        private void BuildDeck(CardData[] cards, int actor){
            decks[actor].Clear();
            var shuffled = new List<CardState>();
            if(cards != null) foreach(var asset in cards){
                if(!asset) continue;
                shuffled.Add(MakeCard(asset, actor));
            }
            for(int i = shuffled.Count - 1; i > 0; i--){
                int other = Random.Range(0, i + 1);
                var card = shuffled[i]; shuffled[i] = shuffled[other]; shuffled[other] = card;
            }
            foreach(var card in shuffled) decks[actor].Enqueue(card);
            state.Side(actor).deckCount = decks[actor].Count;
            Log("Player " + actor + " deck shuffled and ready: " + decks[actor].Count + " cards.");
        }

        private CardState MakeCard(CardData asset, int actor){
            return new CardState {
                instanceId = "match-" + (++serial), ownerId = actor, data = asset,
                currentCost = Mathf.Max(0, asset.cost), currentInfluence = Mathf.Max(0, asset.influence),
                hasInfluence = asset.performer
            };
        }

        private void AddOpeningCard(CardData asset, int actor){
            if(!asset) return;
            var side = state.Side(actor);
            side.hand.Add(MakeCard(asset, actor));
            side.hiddenHandCount = side.hand.Count;
            Log("Player " + actor + " received their guaranteed opening card: " + asset.cardName + ".");
        }

        private void DrawCards(int actor, int count){
            var side = state.Side(actor);
            int drawn = 0, discarded = 0;
            while(drawn < count && decks[actor].Count > 0){
                var card = decks[actor].Dequeue(); drawn++;
                if(side.hand.Count >= MaxHandSize){
                    discarded++;
                    Log("Player " + actor + " discarded newly drawn card " + card.data.cardName + " because their hand is full (" + MaxHandSize + ").");
                }else side.hand.Add(card);
            }
            side.deckCount = decks[actor].Count;
            side.hiddenHandCount = side.hand.Count;
            Log("Player " + actor + " drew " + drawn + " card(s); discarded: " + discarded + ". Hand: " + side.hand.Count + "; deck: " + side.deckCount + ".");
            if(side.deckCount == 0){
                deckExhausted = true;
                Log("Player " + actor + " has an empty deck. The match ends after this round's scoring.");
            }
        }

        private void Log(string message){
            if(!enableDebugLogs) return;
            string context = state == null ? "" : "[Round " + state.roundNumber + ", turn " + turnNumber + ", " + state.phase + "] ";
            Debug.Log("[Gameplay] " + context + message, this);
        }

        private bool IsPlaying(){
            return state != null && (state.phase == RoundPhase.Preparation || state.phase == RoundPhase.Performance);
        }

        private bool CanAct(int actor){
            return isActiveAndEnabled && IsPlaying() && actor >= 0 && actor < 2 &&
                state.inputAllowed && state.activePlayerId == actor && !IsPaused;
        }

        private static bool InBounds(int x, int y){
            return x >= 0 && x < 5 && y >= 0 && y < 5;
        }

        private static CardState Performer(TileState tile, int actor){
            return actor == 0 ? tile.side0 : tile.side1;
        }

        private static void SetPerformer(TileState tile, int actor, CardState card){
            if(actor == 0) tile.side0 = card; else tile.side1 = card;
        }

        private CardState Find(int actor, BoardAction action){
            if(string.IsNullOrEmpty(action.cardInstanceId)) return null;
            if(action.kind == BoardActionKind.PlayCard)
                return state.Side(actor).hand.Find(c => c.instanceId == action.cardInstanceId);
            if(!InBounds(action.fromColumn, action.fromRow)) return null;
            var card = Performer(state.tiles[action.fromRow * 5 + action.fromColumn], actor);
            return card != null && card.instanceId == action.cardInstanceId ? card : null;
        }

        public override bool CanSubmit(BoardAction action, out string reason){
            return CanSubmitFor(state == null ? -1 : state.localPlayerId, action, out reason);
        }

        public bool CanSubmitFor(int actor, BoardAction action, out string reason){
            reason = "Wait for your turn in an active phase.";
            if(!CanAct(actor)) return false;
            reason = "Invalid board action or destination.";
            if(!InBounds(action.toColumn, action.toRow) ||
                (action.kind != BoardActionKind.PlayCard && action.kind != BoardActionKind.MovePerformer)) return false;
            var card = Find(actor, action);
            reason = "The card must belong to you and have exactly one card type.";
            if(card == null || card.ownerId != actor || !card.data || card.data.performer == card.data.stageEffect) return false;
            var target = state.tiles[action.toRow * 5 + action.toColumn];
            if(action.kind == BoardActionKind.PlayCard){
                reason = "Not enough energy.";
                if(card.currentCost < 0 || card.currentCost > state.Side(actor).energy) return false;
                if(card.data.stageEffect){
                    reason = "Stage effects target a friendly performer.";
                    if(Performer(target, actor) == null) return false;
                    reason = ""; return true;
                }
            }
            reason = "Performers can only be placed or moved during preparation.";
            if(!card.data.performer || state.phase != RoundPhase.Preparation) return false;
            reason = "You already have a performer on that tile.";
            if(Performer(target, actor) != null) return false;
            if(action.kind == BoardActionKind.MovePerformer){
                int placed;
                if(!placedTurns.TryGetValue(card.instanceId, out placed)) placed = -1;
                reason = "Move one performer one square horizontally per turn, after its placement turn.";
                if(action.fromRow != action.toRow || Mathf.Abs(action.fromColumn - action.toColumn) != 1 ||
                    placed >= turnNumber || lastMoveTurn[actor] >= turnNumber) return false;
            }else{
                reason = "Place in your leftmost three columns or adjacent to a friendly performer.";
                if(!CanPlace(actor, action.toColumn, action.toRow)) return false;
            }
            reason = ""; return true;
        }

        private bool CanPlace(int actor, int x, int y){
            if((actor == 0 ? x : 4 - x) <= 2) return true;
            for(int dy = -1; dy <= 1; dy++) for(int dx = -1; dx <= 1; dx++){
                if(dx == 0 && dy == 0) continue;
                int nx = x + dx, ny = y + dy;
                if(InBounds(nx, ny) && Performer(state.tiles[ny * 5 + nx], actor) != null) return true;
            }
            return false;
        }

        public override bool TrySubmit(BoardAction action){
            return TrySubmitFor(state == null ? -1 : state.localPlayerId, action);
        }

        public bool TrySubmitFor(int actor, BoardAction action){
            string reason;
            if(!CanSubmitFor(actor, action, out reason)){
                Log("Player " + actor + " action rejected: " + reason); return false;
            }
            var card = Find(actor, action);
            var target = state.tiles[action.toRow * 5 + action.toColumn];
            if(action.kind == BoardActionKind.PlayCard){
                var side = state.Side(actor);
                side.hand.Remove(card); side.hiddenHandCount = side.hand.Count;
                side.energy -= card.currentCost;
                if(card.data.stageEffect){
                    var performer = Performer(target, actor);
                    performer.currentInfluence = Mathf.Max(0, performer.currentInfluence + card.data.stageInfluenceChange);
                    Log("Player " + actor + " used stage effect " + card.data.cardName + " at (" + action.toColumn + ", " + action.toRow +
                        "). Performer influence: " + performer.currentInfluence + ".");
                }else{
                    SetPerformer(target, actor, card); placedTurns[card.instanceId] = turnNumber;
                    Log("Player " + actor + " placed " + card.data.cardName + " at (" + action.toColumn + ", " + action.toRow + ").");
                }
                Log("Player " + actor + " spent " + card.currentCost + " energy; remaining: " + side.energy + ". Hand: " + side.hand.Count + ".");
            }else{
                SetPerformer(state.tiles[action.fromRow * 5 + action.fromColumn], actor, null);
                SetPerformer(target, actor, card); lastMoveTurn[actor] = turnNumber;
                Log("Player " + actor + " moved " + card.data.cardName + " from (" + action.fromColumn + ", " + action.fromRow +
                    ") to (" + action.toColumn + ", " + action.toRow + ").");
            }
            state.consecutivePasses = 0;
            Recompute();
            if (action.kind == BoardActionKind.PlayCard && sfx)
            {
                sfx.PlayCardSound(card.data.playSfx);
            }
            Publish();
            return true;
        }

        public override void RequestPass(){
            if(state != null) TryPassFor(state.localPlayerId);
        }

        public bool TryPassFor(int actor){
            if(!CanAct(actor)){ Log("Player " + actor + " pass rejected: not an active, unpaused turn."); return false; }
            state.consecutivePasses++;
            Log("Player " + actor + " passed. Consecutive passes: " + state.consecutivePasses + ".");
            if(state.consecutivePasses < 2) BeginTurn(1 - actor);
            else if(state.phase == RoundPhase.Preparation){
                state.consecutivePasses = 0; state.phase = RoundPhase.Performance;
                Log("Preparation complete. Performance begins.");
                BeginTurn(state.roundStarterId);
            }else ResolveRound();
            Publish(); return true;
        }

        private void OnTurnExpired(){
            if(state != null){
                Log("Player " + state.activePlayerId + " timer expired; passing turn.");
                TryPassFor(state.activePlayerId);
            }
        }

        private void BeginTurn(int actor){
            state.activePlayerId = actor; turnNumber++;
            Log("Player " + actor + " turn begins. Remaining energy: " + state.Side(actor).energy + ".");
            StartClock();
        }

        private void RefillRoundEnergy(){
            int energy = Mathf.Clamp(state.roundNumber, 1, 8);
            state.side0.energy = state.side1.energy = energy;
            Log("Round energy refilled to " + energy + " for both players.");
        }

        private void StartClock(){
            if(!clock) return;
            clock.BeginTurn(Mathf.Max(0, turnSeconds));
            if(turnSeconds <= 0) clock.Stop();
        }

        private void Recompute(){
            foreach(var tile in state.tiles){
                tile.total0 = tile.side0 == null ? 0 : Mathf.Max(0, tile.side0.currentInfluence);
                tile.total1 = tile.side1 == null ? 0 : Mathf.Max(0, tile.side1.currentInfluence);
            }
        }

        private void RemovePerformer(TileState tile, int actor){
            var card = Performer(tile, actor);
            if(card != null) placedTurns.Remove(card.instanceId);
            SetPerformer(tile, actor, null);
        }

        private void ResolveRound(){
            state.phase = RoundPhase.EndRound; state.inputAllowed = false;
            state.consecutivePasses = 0;
            if(clock) clock.Stop();
            Recompute();
            Log("Resolving round.");
            for(int i = 0; i < state.tiles.Length; i++){
                var tile = state.tiles[i];
                if(tile.side0 == null || tile.side1 == null) continue;
                int difference = tile.total0 - tile.total1;
                Log("Contest at (" + (i % 5) + ", " + (i / 5) + "): " + tile.total0 + " vs " + tile.total1 + ". " +
                    (difference == 0 ? "Tie; both performers removed." : "Player " + (difference > 0 ? 0 : 1) + " survives with " + Mathf.Abs(difference) + " influence."));
                if(difference > 0){ tile.side0.currentInfluence = difference; RemovePerformer(tile, 1); }
                else if(difference < 0){ tile.side1.currentInfluence = -difference; RemovePerformer(tile, 0); }
                else { RemovePerformer(tile, 0); RemovePerformer(tile, 1); }
            }
            Recompute();
            int total0 = 0, total1 = 0;
            for(int row = 0; row < 5; row++){
                total0 += state.tiles[row * 5 + 2].total0;
                total1 += state.tiles[row * 5 + 2].total1;
            }
            state.side0.score += total0; state.side1.score += total1;
            state.roundSummary = "Third column: Player +" + (state.localPlayerId == 0 ? total0 : total1) +
                " / Opponent +" + (state.localPlayerId == 0 ? total1 : total0) + ".";
            if(deckExhausted) state.roundSummary += " A deck is empty; this is the final round.";
            Log("Round scored: Player 0 +" + total0 + ", Player 1 +" + total1 + ". Total scores: " + state.side0.score + " / " + state.side1.score + ".");
            roundWait = Mathf.Max(0, endRoundDisplaySeconds);
        }

        private void Update(){
            if(state == null || state.phase != RoundPhase.EndRound || IsPaused) return;
            roundWait -= Time.unscaledDeltaTime;
            if(roundWait <= 0) CompleteRound();
        }

        public bool CompleteRound(){
            if(!isActiveAndEnabled || state == null || state.phase != RoundPhase.EndRound || IsPaused) return false;
            if(deckExhausted || state.side0.score >= state.winScore || state.side1.score >= state.winScore){
                state.phase = RoundPhase.Finished;
                state.winnerId = state.side0.score == state.side1.score ? -1 : state.side0.score > state.side1.score ? 0 : 1;
                state.roundSummary += state.winnerId < 0 ? " Draw!" : state.winnerId == state.localPlayerId ? " Player wins!" : " Opponent wins!";
                Log("Match finished (" + (deckExhausted ? "empty deck" : "winning score reached") + "). " +
                    (state.winnerId < 0 ? "Draw." : "Player " + state.winnerId + " wins.") + " Scores: " + state.side0.score + " / " + state.side1.score + ".");
            }else{
                state.roundNumber++; state.roundStarterId = 1 - state.roundStarterId;
                state.phase = RoundPhase.Preparation; state.inputAllowed = true;
                Log("New round begins. Player " + state.roundStarterId + " goes first.");
                DrawCards(0, 1); DrawCards(1, 1);
                RefillRoundEnergy();
                BeginTurn(state.roundStarterId);
            }
            Publish(); return true;
        }

        public override void SetLocalPause(bool paused){
            if(state != null && !state.multiplayer && clock){
                if(clock.Paused == paused) return;
                clock.Paused = paused; Log(paused ? "Match paused." : "Match resumed."); Publish();
            }
        }
    }
}
