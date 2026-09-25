using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VocaloidTCG.BoardUI
{
    public sealed partial class GameplayBoardBridge
    {
        [Header("Puzzle (overrides normal deck setup)")]
        public PuzzleData puzzle;
        [Min(0)] public float puzzleEnemyActionDelay = 0.5f;
        public bool IsPuzzle => puzzle;
        public bool PuzzleSucceeded { get; private set; }
        public string PuzzleSaveError { get; private set; }
        private int puzzlePlayerTurns, puzzleRounds, puzzleEnemyTurn, puzzleAction, puzzleObservedTurn;
        private int puzzlePreviousScore, puzzleRoundScore;
        private float puzzleEnemyWait;
        private readonly int[] puzzleSideTurns = new int[2];

        private void StartPuzzle(){
            MatchSetupError = "";
            string error;
            if(!puzzle.Validate(out error)) { PuzzleSetupFailure(error); return; }
            try {
                var library = DeckLibrary.Get(deckCatalog);
                deckCatalog = library.Catalog;
                if(deckCatalog.Card(puzzle.rewardCard.id) != puzzle.rewardCard) {
                    PuzzleSetupFailure("Register the puzzle reward in the deck catalog."); return;
                }
            } catch(Exception ex) { PuzzleSetupFailure(ex.Message); return; }
            chosenBoard = puzzle.boardSetup ? puzzle.boardSetup : boardSetups.FirstOrDefault(b => b && b.PlayerClass == puzzle.puzzleClass && b.EnemyClass == puzzle.againstClass);
            if(!chosenBoard) { PuzzleSetupFailure("Assign a Board Setup matching the puzzle's player and opponent classes."); return; }
            if(chosenBoard.PlayerClass != puzzle.puzzleClass || chosenBoard.EnemyClass != puzzle.againstClass) {
                PuzzleSetupFailure("Puzzle Board Setup classes do not match the puzzle classes."); return;
            }
            playerClasses = new List<CharacterClassData> { puzzle.puzzleClass };
            enemyClasses = new List<CharacterClassData> { puzzle.againstClass };
            selectedPlayerBack = chosenBoard.playerCardBack;
            selectedEnemyBack = chosenBoard.enemyCardBack;
            playerCardBack = selectedPlayerBack ? selectedPlayerBack.image : null;
            enemyCardBack = selectedEnemyBack ? selectedEnemyBack.image : null;
            ConfigurationVersion++;
            if(clock) { clock.Stop(); clock.Paused = false; }
            drawPresentations.Clear(); SetDrawAnimationPlaying(false);
            placedTurns.Clear(); serial = turnNumber = 0; deckExhausted = false;
            puzzlePlayerTurns = puzzleRounds = puzzleAction = 0;
            puzzleEnemyTurn = puzzleObservedTurn = -1;
            PuzzleSucceeded = false; PuzzleSaveError = "";
            localPlayerId = Mathf.Clamp(localPlayerId, 0, 1);
            int starter = puzzle.enemyStarts ? 1 - localPlayerId : localPlayerId;
            state = new BoardSnapshot { localPlayerId = localPlayerId, roundNumber = puzzle.startingRound,
                roundStarterId = starter, phase = puzzle.startingPhase, winScore = puzzle.targetScore };
            for(int i = 0; i < 25; i++) state.tiles[i] = new TileState();
            for(int actor = 0; actor < 2; actor++) {
                var setup = actor == localPlayerId ? puzzle.player : puzzle.enemy;
                var side = state.Side(actor);
                side.energy = setup.energy; side.score = setup.score;
                decks[actor].Clear();
                foreach(var card in setup.startingHand) side.hand.Add(MakeCard(card, actor));
                side.deckCount = 0; side.hiddenHandCount = side.hand.Count;
                puzzleSideTurns[actor] = 0;
                lastMoveTurn[actor] = -1; lastDrawRound[actor] = state.roundNumber;
            }
            foreach(var placement in puzzle.placements) {
                int actor = placement.enemy ? 1 - localPlayerId : localPlayerId;
                var card = MakeCard(placement.card, actor);
                if(placement.influence >= 0) card.currentInfluence = placement.influence;
                SetPerformer(state.tiles[placement.row * 5 + placement.column], actor, card);
                placedTurns[card.instanceId] = -1;
            }
            puzzlePreviousScore = state.Side(localPlayerId).score; puzzleRoundScore = 0;
            Recompute();
            if(!CheckPuzzle(false, false)) BeginTurn(starter);
            Publish();
        }

        private void PuzzleSetupFailure(string error){
            MatchSetupError = error;
            if(clock) clock.Stop();
            if(state != null) { state.inputAllowed = false; state.phase = RoundPhase.Finished; state.roundSummary = error; }
            Debug.LogError("Puzzle setup: " + error, this); Publish();
        }

        private void GivePuzzleTurnCards(int actor){
            int currentTurn = ++puzzleSideTurns[actor];
            var setup = actor == localPlayerId ? puzzle.player : puzzle.enemy;
            var side = state.Side(actor);
            foreach(var grant in setup.cardsOnTurns) {
                if(grant.turn != currentTurn) continue;
                foreach(var asset in grant.cards) {
                    var card = MakeCard(asset, actor);
                    bool discarded = side.hand.Count >= MaxHandSize;
                    drawPresentations.Enqueue(new DrawPresentation { card = card, discarded = discarded });
                    if(!discarded) side.hand.Add(card);
                    Log("Player " + actor + (discarded ? " discarded scheduled card " : " received scheduled card ") + asset.cardName + " on their turn " + currentTurn + ".");
                }
            }
            side.hiddenHandCount = side.hand.Count;
        }

        private bool CheckPuzzle(bool roundResolved, bool beforePlayerTurn){
            if(state.phase == RoundPhase.Finished) return true;
            if(roundResolved) {
                puzzleRoundScore = state.Side(localPlayerId).score - puzzlePreviousScore;
                puzzlePreviousScore = state.Side(localPlayerId).score;
            }
            bool success = false;
            switch(puzzle.objective) {
                case PuzzleObjectiveKind.ReachTotalScore: success = state.Side(localPlayerId).score >= puzzle.targetScore; break;
                case PuzzleObjectiveKind.ScoreInOneRound: success = roundResolved && puzzleRoundScore >= puzzle.targetScore; break;
                case PuzzleObjectiveKind.ClearEnemyCenter:
                case PuzzleObjectiveKind.ClearAllEnemies:
                    success = true;
                    for(int i = 0; i < state.tiles.Length; i++) {
                        if(puzzle.objective == PuzzleObjectiveKind.ClearEnemyCenter && i % 5 != 2) continue;
                        if(Performer(state.tiles[i], 1 - localPlayerId) != null) { success = false; break; }
                    }
                    break;
            }
            bool expired = (roundResolved && puzzle.roundLimit > 0 && puzzleRounds >= puzzle.roundLimit) ||
                ((beforePlayerTurn || roundResolved) && puzzle.playerTurnLimit > 0 && puzzlePlayerTurns >= puzzle.playerTurnLimit);
            if(!success && !expired) return false;
            PuzzleSucceeded = success;
            state.phase = RoundPhase.Finished; state.inputAllowed = false;
            state.winnerId = success ? localPlayerId : 1 - localPlayerId;
            state.roundSummary = success ? "Puzzle completed!" : "Puzzle failed: objective not reached within the limit.";
            if(clock) clock.Stop();
            if(success) SavePuzzleReward();
            return true;
        }

        public bool SavePuzzleReward(){
            if(!IsPuzzle || !PuzzleSucceeded) return false;
            string error;
            try {
                if(DeckLibrary.Get(deckCatalog).CompletePuzzle(puzzle, out error)) {
                    PuzzleSaveError = "";
                    state.roundSummary = "Puzzle completed! Reward unlocked: " + puzzle.rewardCard.cardName;
                    Publish(); return true;
                }
            } catch(Exception ex) { error = ex.Message; }
            PuzzleSaveError = error;
            state.roundSummary = "Puzzle completed, but progress could not be saved: " + error + " Retry saving before leaving.";
            Debug.LogError(state.roundSummary, this); Publish(); return false;
        }

        private void UpdatePuzzleEnemy(){
            if(!IsPuzzle || !IsPlaying() || IsPaused || DrawAnimationPlaying || !state.inputAllowed || state.activePlayerId == localPlayerId) return;
            if(puzzleObservedTurn != turnNumber) {
                puzzleObservedTurn = turnNumber; puzzleEnemyTurn++; puzzleAction = 0;
                puzzleEnemyWait = puzzleEnemyActionDelay;
            }
            puzzleEnemyWait -= Time.unscaledDeltaTime;
            if(puzzleEnemyWait > 0) return;
            puzzleEnemyWait = puzzleEnemyActionDelay;
            var script = puzzleEnemyTurn < puzzle.enemyTurns.Count ? puzzle.enemyTurns[puzzleEnemyTurn] : null;
            int actor = 1 - localPlayerId;
            if(script == null || puzzleAction >= script.actions.Count) { TryPassFor(actor); return; }
            var step = script.actions[puzzleAction];
            var card = step.kind == BoardActionKind.PlayCard ? state.Side(actor).hand.Find(c => c.data == step.card) :
                Performer(state.tiles[step.fromRow * 5 + step.fromColumn], actor);
            var action = new BoardAction(step.kind, card == null ? "" : card.instanceId, step.fromColumn, step.fromRow, step.toColumn, step.toRow);
            string reason;
            if(!CanSubmitFor(actor, action, out reason)) {
                PuzzleSetupFailure("Enemy turn " + (puzzleEnemyTurn + 1) + ", action " + (puzzleAction + 1) + ": " + reason); return;
            }
            puzzleAction++; TrySubmitFor(actor, action);
        }
    }
}
