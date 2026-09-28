using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VocaloidTCG.BoardUI
{
    public sealed class PreviewBoardBridge : BoardGameBridge
    {
        public CardData[] previewCards;
        [Range(0, 1)] public int localPlayerId;
        public LocalTurnClock clock;
        public Button returnToPlayerTurnButton;
        [Header("Simple enemy AI")]
        public bool enableEnemyAI = true;
        [Min(0.1f)] public float enemyActionDelay = 1f;
        public CardData[] enemyPreviewCards;
        public bool enemyStartsFirst;
        [Min(1)] public int winningScore = 20;
        [Min(0.1f)] public float endRoundDisplaySeconds = 2f;
        [Serializable]

        public sealed class PreviewStats
        {
            [Min(0)] public int score;
            [Min(0)] public int deckCount = 20;
            [Range(0, 8)] public int energy = 8;
        }

        public PreviewStats playerStats = new PreviewStats();
        public PreviewStats enemyStats = new PreviewStats();
        private BoardSnapshot state;
        private int serial;
        private int turnNumber;

        private readonly Dictionary<string, int> placedTurns = new Dictionary<string, int>();
        private readonly int[] lastSideMoveTurn = { -1, -1 };
        private readonly List<CardState> enemyHand = new List<CardState>();
        private int enemyStep;
        private float enemyWait;
        private float endRoundWait;

        public override BoardSnapshot Snapshot{
            get{
                return state;
            }
        }

        public override float RemainingSeconds{
            get{
                return clock ? clock.RemainingSeconds : 60;
            }
        }

        private void Awake(){
            state = new BoardSnapshot { localPlayerId = localPlayerId, activePlayerId = localPlayerId };
            state.roundStarterId = enemyStartsFirst ? 1 - localPlayerId : localPlayerId;
            state.activePlayerId = state.roundStarterId;
            state.winScore = Mathf.Max(1, winningScore);
            ApplyStats();

            for(int i = 0; i < 25; i++) state.tiles[i] = new TileState();
            if(previewCards != null) foreach (var asset in previewCards)
                if(asset) state.Side(localPlayerId).hand.Add(Make(asset, localPlayerId));

            var enemyCards = enemyPreviewCards != null && enemyPreviewCards.Length > 0 ? enemyPreviewCards : previewCards;

            if(enemyCards != null) foreach (var asset in enemyCards)
                if(asset) enemyHand.Add(Make(asset, 1 - localPlayerId));
            state.Side(1 - localPlayerId).hiddenHandCount = enemyHand.Count;

            if(previewCards != null) foreach (var asset in previewCards)
                if(asset && asset.performer)
                {
                    SetPerformer(state.tiles[12], Make(asset, 1 - localPlayerId));
                    break;
                }

            Recompute();
            if(!clock) clock = gameObject.AddComponent<LocalTurnClock>();
            clock.Expired += RequestPass;
        }

        private void Start(){
            if(returnToPlayerTurnButton) returnToPlayerTurnButton.onClick.AddListener(ReturnToPlayerTurn);
            BeginTurn(state.roundStarterId); Publish();
        }

        private void Update(){
            if(state == null) return;
            if(ApplyStats()) Publish();
            if(state.phase == RoundPhase.EndRound){
                if(!clock.Paused){
                    endRoundWait -= Time.unscaledDeltaTime;
                    if(endRoundWait <= 0) FinishRoundDisplay();
                }
                return;
            }
            TickEnemy();
        }

        private bool ApplyStats(){
            bool playerChanged = ApplySideStats(playerStats, state.Side(state.localPlayerId));
            bool enemyChanged = ApplySideStats(enemyStats, state.Side(1 - state.localPlayerId));
            return playerChanged || enemyChanged;
        }

        private static bool ApplySideStats(PreviewStats input, SideState target){
            input.score = Mathf.Max(0, input.score);
            input.deckCount = Mathf.Max(0, input.deckCount);
            input.energy = Mathf.Clamp(input.energy, 0, 8);
            bool changed = target.score != input.score || target.deckCount != input.deckCount || target.energy != input.energy;
            target.score = input.score; target.deckCount = input.deckCount; target.energy = input.energy;
            return changed;
        }
        
        private void OnDestroy(){
            if(clock) clock.Expired -= RequestPass;
            if(returnToPlayerTurnButton) returnToPlayerTurnButton.onClick.RemoveListener(ReturnToPlayerTurn);
        }
        
        private CardState Make(CardData asset, int owner){
            return new CardState {instanceId = "preview-" + (++serial), data = asset, ownerId = owner, currentCost = asset.cost, currentInfluence = asset.influence, hasInfluence = asset.performer};
        }

        private List<CardState> Hand(int actor){
            return actor == state.localPlayerId ? state.Side(actor).hand : enemyHand;
        }

        private CardState Find(BoardAction action, int actor){
            if(action.kind == BoardActionKind.PlayCard)
                return Hand(actor).Find(c => c.instanceId == action.cardInstanceId);

            if(action.fromColumn < 0 || action.fromColumn > 4 || action.fromRow < 0 || action.fromRow > 4) return null;

            var tile = state.tiles[action.fromRow * 5 + action.fromColumn];
            var card = actor == 0 ? tile.side0 : tile.side1;
            return card != null && card.instanceId == action.cardInstanceId ? card : null;
        }

        public override bool CanSubmit(BoardAction action, out string reason){
            return CanAct(action, state.localPlayerId, out reason);
        }
        
        private bool CanAct(BoardAction action, int actor, out string reason){
            reason = "Preview action unavailable";
            if(state.activePlayerId != actor || !state.inputAllowed || (clock && clock.Paused) || state.phase != RoundPhase.Preparation) return false;
            if(action.toColumn < 0 || action.toColumn > 4 || action.toRow < 0 || action.toRow > 4) return false;
            if(action.kind != BoardActionKind.PlayCard && action.kind != BoardActionKind.MovePerformer) return false;

            var card = Find(action, actor);
            if(card == null || !card.data.performer) return false;
            var tile = state.tiles[action.toRow * 5 + action.toColumn];

            if((actor == 0 ? tile.side0 : tile.side1) != null) return false;
            if(action.kind == BoardActionKind.PlayCard && card.currentCost > state.Side(actor).energy) return false;
            if(action.kind == BoardActionKind.PlayCard){
                var friendly = new bool[5, 5];
                for(int y = 0; y < 5; y++) for(int x = 0; x < 5; x++){
                    var occupied = state.tiles[y * 5 + x];
                    friendly[x, y] = (actor == 0 ? occupied.side0 : occupied.side1) != null;
                }

                if(!PreviewRules.CanPlace(friendly, action.toColumn, action.toRow, actor)){
                    reason = "Place in columns 1–3 from your left or in one of the eight squares adjacent to a friendly performer.";
                    return false;
                }
            }

            if(action.kind == BoardActionKind.MovePerformer){
                int placed;
                if(!placedTurns.TryGetValue(card.instanceId, out placed)) placed = -1;
                if(!PreviewRules.CanMove(action.fromColumn, action.fromRow, action.toColumn, action.toRow, placed, lastSideMoveTurn[actor], turnNumber)){
                    reason = "Only one character may move per side per turn, one square horizontally, after its placement turn."; return false;
                }
            }
            reason = ""; return true;
        }

        public override bool TrySubmit(BoardAction action){
            return TryAct(action, state.localPlayerId);
        }

        private bool TryAct(BoardAction action, int actor){
            string reason;
            if(!CanAct(action, actor, out reason)) return false;
            var card = Find(action, actor);

            if(action.kind == BoardActionKind.PlayCard){
                Hand(actor).Remove(card);
                state.Side(actor).energy -= card.currentCost;
                var stats = actor == state.localPlayerId ? playerStats : enemyStats;
                stats.energy = state.Side(actor).energy;
                state.Side(1 - state.localPlayerId).hiddenHandCount = enemyHand.Count;
                placedTurns[card.instanceId] = turnNumber;
            }
            else{
                var from = state.tiles[action.fromRow * 5 + action.fromColumn];
                if(actor == 0){ from.side0 = null; from.assist0 = null; }
                else { from.side1 = null; from.assist1 = null; }
                lastSideMoveTurn[actor] = turnNumber;
            }

            SetPerformer(state.tiles[action.toRow * 5 + action.toColumn], card);
            state.consecutivePasses = 0;
            Recompute(); Publish(); return true;
        }

        private void SetPerformer(TileState tile, CardState card){
            if(card.ownerId == 0) tile.side0 = card; else tile.side1 = card;
        }
        
        private void Recompute(){
            foreach (var tile in state.tiles){
                tile.total0 = tile.side0 != null ? tile.side0.currentInfluence : 0;
                tile.total1 = tile.side1 != null ? tile.side1.currentInfluence : 0;
            }
        }

        private void BeginTurn(int actor){
            state.activePlayerId = actor;
            turnNumber++;
            ResetEnemyTurn();
            clock.BeginTurn();
        }

        public override void RequestPass(){
            if(state == null || !clock || clock.Paused || !state.inputAllowed || (state.phase != RoundPhase.Preparation && state.phase != RoundPhase.Performance)) return;
            state.consecutivePasses++;

            if(state.consecutivePasses < 2){
                BeginTurn(1 - state.activePlayerId);
            }else if(state.phase == RoundPhase.Preparation){
                state.consecutivePasses = 0;
                state.phase = RoundPhase.Performance;
                BeginTurn(state.roundStarterId);
            }else{
                ResolveRound();
            }
            Publish();
        }

        private void ResolveRound(){
            state.phase = RoundPhase.EndRound;
            state.inputAllowed = false;
            state.consecutivePasses = 0;
            clock.Stop();
            Recompute();

            foreach (var tile in state.tiles){
                if(tile.side0 != null && tile.side1 != null){
                    int difference = tile.total0 - tile.total1;
                    if(difference > 0){ tile.side0.currentInfluence = difference; tile.side1 = null; }
                    else if(difference < 0){ tile.side1.currentInfluence = -difference; tile.side0 = null; }
                    else { tile.side0 = null; tile.side1 = null; }
                }
                tile.assist0 = tile.assist1 = null;
            }

            Recompute();
            int total0 = 0, total1 = 0;

            for(int row = 0; row < 5; row++){
                total0 += state.tiles[row * 5 + 2].total0;
                total1 += state.tiles[row * 5 + 2].total1;
            }

            state.side0.score += total0;
            state.side1.score += total1;
            playerStats.score = state.Side(state.localPlayerId).score;
            enemyStats.score = state.Side(1 - state.localPlayerId).score;
            int localTotal = state.localPlayerId == 0 ? total0 : total1;
            int enemyTotal = state.localPlayerId == 0 ? total1 : total0;
            state.roundSummary = "Third column: Player +" + localTotal + " / Enemy +" + enemyTotal + ".";
            endRoundWait = Mathf.Max(0.1f, endRoundDisplaySeconds);
        }

        private void FinishRoundDisplay(){
            if(state.side0.score >= state.winScore || state.side1.score >= state.winScore){
                state.phase = RoundPhase.Finished;
                state.winnerId = state.side0.score == state.side1.score ? -1 : state.side0.score > state.side1.score ? 0 : 1;
                state.roundSummary += " " + (state.winnerId < 0 ? "Draw!" : state.winnerId == state.localPlayerId ? "Player wins!" : "Enemy wins!");
            }else{
                state.roundNumber++;
                state.roundStarterId = 1 - state.roundStarterId;
                state.phase = RoundPhase.Preparation;
                state.inputAllowed = true;
                BeginTurn(state.roundStarterId);
            }
            Publish();
        }

        public override void SetLocalPause(bool paused){
            if(!state.multiplayer && clock) clock.Paused = paused;
        }

        public void ReturnToPlayerTurn(){
            if(state == null || state.phase == RoundPhase.EndRound || state.phase == RoundPhase.Finished) return;
            state.consecutivePasses = 0;
            ApplyStats();
            BeginTurn(state.localPlayerId); Publish();
        }

        private void ResetEnemyTurn(){
            enemyStep = 0; enemyWait = Mathf.Max(0.1f, enemyActionDelay);
        }

        private void TickEnemy(){
            if(!enableEnemyAI || state.multiplayer || state.activePlayerId == state.localPlayerId || !state.inputAllowed || (state.phase != RoundPhase.Preparation && state.phase != RoundPhase.Performance) || !clock || clock.Paused) return;
            enemyWait -= Time.unscaledDeltaTime;
            if(enemyWait > 0) return;
            enemyWait = Mathf.Max(0.1f, enemyActionDelay);
            if(state.phase == RoundPhase.Performance){ RequestPass(); return; }
            int actor = 1 - state.localPlayerId;
            BoardAction action;

            if(enemyStep == 0){
                enemyStep = 1;
                if(ChooseEnemyAction(actor, BoardActionKind.PlayCard, out action)) TryAct(action, actor);
            }else if(enemyStep == 1){
                enemyStep = 2;
                if(ChooseEnemyAction(actor, BoardActionKind.MovePerformer, out action)) TryAct(action, actor);
            }else{
                RequestPass();
            }
        }

        private bool ChooseEnemyAction(int actor, BoardActionKind kind, out BoardAction best){
            best = default(BoardAction);
            bool found = false;
            int bestPriority = int.MinValue;
            var candidates = new List<BoardAction>();
            if(kind == BoardActionKind.PlayCard){
                foreach(var card in enemyHand){
                    for(int y = 0; y < 5; y++) for(int x = 0; x < 5; x++){
                        candidates.Add(new BoardAction(kind, card.instanceId, -1, -1, x, y));
                    }
                }
            }else{
                for(int y = 0; y < 5; y++) for(int x = 0; x < 5; x++){
                    var tile = state.tiles[y * 5 + x];
                    var card = actor == 0 ? tile.side0 : tile.side1;
                    if(card == null) continue;
                    candidates.Add(new BoardAction(kind, card.instanceId, x, y, x - 1, y));
                    candidates.Add(new BoardAction(kind, card.instanceId, x, y, x + 1, y));
                }
            }

            foreach (var candidate in candidates){
                string reason;
                if(!CanAct(candidate, actor, out reason)) continue;

                var card = Find(candidate, actor);
                var target = state.tiles[candidate.toRow * 5 + candidate.toColumn];
                var opponent = actor == 0 ? target.side1 : target.side0;
                int opponentTotal = actor == 0 ? target.total1 : target.total0;
                int priority = 20 - 5 * Mathf.Abs(candidate.toColumn - 2);

                if(opponent != null) priority += card.currentInfluence > opponentTotal ? 10 : -20;
                if(kind == BoardActionKind.MovePerformer && Mathf.Abs(candidate.toColumn - 2) >= Mathf.Abs(candidate.fromColumn - 2) && !(opponent != null && card.currentInfluence > opponentTotal)) continue;
                if(!found || priority > bestPriority){
                    found = true; bestPriority = priority; best = candidate;
                }
            }
            return found;
        }

        private void ChangeStats(){
            if(state == null) return;
            playerStats.score += 2;
            ApplyStats();
            
            foreach (var tile in state.tiles){
                var card = localPlayerId == 0 ? tile.side0 : tile.side1;
                if(card == null) continue;
                card.currentInfluence += 2;
                card.activeEffects.Add("Preview bonus: +2 influence from a solo effect.");
                break;
            }
            Recompute(); Publish();
        }
    }
}
