using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace VocaloidTCG.BoardUI
{
    public enum EnemyAIScoringAxis { Row, Column }

    /// <summary>
    /// Chooses legal plays, moves and card effects using simple score values.
    /// Passes when no useful action remains.
    /// </summary>
    public class EnemyAIController : MonoBehaviour
    {
        public GameplayBoardBridge game;
        public List<CharacterClassData> classes = new List<CharacterClassData>();
        [Min(0.1f)] public float actionDelay = 0.75f;
        [Tooltip("Use the same axis that GameplayBoardBridge uses for end-of-round scoring.")]
        public EnemyAIScoringAxis scoringAxis = EnemyAIScoringAxis.Column;
        public bool enableDecisionLogs = true;

        private int observedTurn = -1;
        private float wait;
        private BoardSnapshot movementSnapshot;
        private int movementRound = -1;
        private readonly Dictionary<string, HashSet<int>> visitedTiles = new Dictionary<string, HashSet<int>>();

        private void RefreshMovementMemory()
        {
            if (movementSnapshot == game.Snapshot && movementRound == game.Snapshot.roundNumber) return;
            movementSnapshot = game.Snapshot; movementRound = movementSnapshot.roundNumber;
            visitedTiles.Clear();
        }

        // Called by the rules engine only after a move succeeds. This is an AI
        // preference, not a gameplay restriction on human movement. It prevents
        // endless back-and-forth across turns from positive local contest scores.
        public void ObserveAcceptedMove(BoardAction action)
        {
            RefreshMovementMemory();
            if (!visitedTiles.TryGetValue(action.cardInstanceId, out var visited))
            {
                visited = new HashSet<int>();
                visitedTiles[action.cardInstanceId] = visited;
            }
            visited.Add(action.fromRow * 5 + action.fromColumn);
            visited.Add(action.toRow * 5 + action.toColumn);
        }

        protected virtual void Update()
        {
            if (!game || game.IsPuzzle || !game.TutorialFreePlay || !game.isActiveAndEnabled || game.IsPaused || game.DrawAnimationPlaying || game.PhaseAnimationPlaying) return;
            var state = game.Snapshot;
            if (state == null || state.multiplayer || !state.inputAllowed ||
                state.activePlayerId == state.localPlayerId ||
                (state.phase != RoundPhase.Preparation && state.phase != RoundPhase.Performance)) return;

            if (observedTurn != game.TurnNumber)
            {
                observedTurn = game.TurnNumber;
                wait = Mathf.Max(0.1f, actionDelay);
            }
            wait -= Time.unscaledDeltaTime;
            if (wait > 0) return;
            wait = Mathf.Max(0.1f, actionDelay);

            int actor = state.activePlayerId;
            // The final tutorial uses a legal easy deck, but makes only one play per turn.
            // Finish any resulting choices before ending the turn.
            if(game.IsTutorial && game.TutorialLesson == 4 && state.pendingEffect == null &&
                game.TutorialEnemyHasActed){ game.TryPassFor(actor); return; }
            BoardAction action;
            EnemyActionEvaluation evaluation;
            if (TryChooseAction(actor, out action, out evaluation))
            {
                LogDecision(actor, action, evaluation);
                game.TrySubmitFor(actor, action);
            }
            else
            {
                if (enableDecisionLogs) Debug.Log("[Enemy AI] Player " + actor + " passes: no positive legal action.", this);
                game.TryPassFor(actor);
            }
        }

        // Also used by deterministic integration checks. This only reads state;
        // GameplayBoardBridge remains the authority for every legal action.
        public bool TryChooseAction(int actor, out BoardAction best, out EnemyActionEvaluation bestEvaluation)
        {
            best = default(BoardAction);
            bestEvaluation = new EnemyActionEvaluation(int.MinValue, "no legal action");
            if (!game) return false;
            var state = game.Snapshot;
            if (state == null || actor < 0 || actor > 1) return false;
            RefreshMovementMemory();
            if (state.pendingEffect != null)
                return ChooseEffect(actor, out best, out bestEvaluation);
            var placementBoard = PlacementBoard(state, actor);
            bool found = false;

            foreach (var card in state.Side(actor).hand)
            {
                for (int y = 0; y < 5; y++) for (int x = 0; x < 5; x++)
                {
                    var candidate = new BoardAction(BoardActionKind.PlayCard, card.instanceId, -1, -1, x, y);
                    string rejectedReason;
                    if (!game.CanSubmitFor(actor, candidate, out rejectedReason)) continue;

                    var tile = state.tiles[y * 5 + x];
                    var friendly = actor == 0 ? tile.side0 : tile.side1;
                    var opponent = actor == 0 ? tile.side1 : tile.side0;
                    if (card.data.stageEffect)
                    {
                        int benefit = EnemyCardPolicy.SpellBenefit(state, actor, card, y * 5 + x);
                        var spell = new EnemyActionEvaluation(benefit - 2 * card.currentCost - 2,
                            card.data.effect + " estimated effect value " + benefit + "; cost " + card.currentCost);
                        Consider(candidate, spell, ref found, ref best, ref bestEvaluation);
                        continue;
                    }
                    int resultingInfluence = card.currentInfluence + EnemyCardPolicy.EntryInfluence(state, actor, card, y * 5 + x);
                    int friendlyInfluence = friendly == null ? 0 : (actor == 0 ? tile.total0 : tile.total1);
                    int opponentInfluence = opponent == null ? 0 : (actor == 0 ? tile.total1 : tile.total0);
                    int influenceGain = resultingInfluence;
                    var evaluation = EnemyActionScorer.Evaluate(
                        Lane(x, y), -1, resultingInfluence + friendlyInfluence, opponentInfluence, card.currentCost, false, Mathf.Max(0, influenceGain));
                    evaluation = EnemyPlacementScorer.Evaluate(evaluation, card.data.flags, placementBoard,
                        x, y, -1, -1, false, resultingInfluence + friendlyInfluence < opponentInfluence);
                    int effectValue = EnemyCardPolicy.PerformerBenefit(state, actor, card, y * 5 + x);
                    evaluation.score += effectValue;
                    evaluation.reason += "; " + card.data.effect + " effect/keyword value " + effectValue;
                    Consider(candidate, evaluation, ref found, ref best, ref bestEvaluation);
                }
            }

            for (int y = 0; y < 5; y++) for (int x = 0; x < 5; x++)
            {
                var source = state.tiles[y * 5 + x];
                var card = actor == 0 ? source.side0 : source.side1;
                if (card == null) continue;

                foreach (var unit in new[] { card, actor == 0 ? source.assist0 : source.assist1 }.Where(c => c != null))
                {
                    var ability = new BoardAction(BoardActionKind.ActivateEffect, unit.instanceId, x, y, x, y);
                    string reason;
                    if (game.CanSubmitFor(actor, ability, out reason))
                        Consider(ability, new EnemyActionEvaluation(EnemyCardPolicy.AbilityBenefit(state, actor, unit),
                            unit.data.effect + " activated ability"), ref found, ref best, ref bestEvaluation);
                }

                ConsiderMove(actor, card, x, y, x - 1, state, placementBoard, ref found, ref best, ref bestEvaluation);
                ConsiderMove(actor, card, x, y, x + 1, state, placementBoard, ref found, ref best, ref bestEvaluation);
            }

            // A deterministic tie-breaker comes from the candidate generation order.
            return found && bestEvaluation.score > 0;
        }

        private bool ChooseEffect(int actor, out BoardAction best, out EnemyActionEvaluation evaluation)
        {
            best = default(BoardAction);
            evaluation = new EnemyActionEvaluation(int.MinValue, "no legal effect target");
            var pending = game.Snapshot.pendingEffect;
            if (pending.actor != actor) return false;
            bool found = false;
            var candidates = pending.cards.Select(c => new BoardAction(BoardActionKind.ChooseEffect, c.instanceId, -1, -1, -1, -1))
                .Concat(pending.tiles.Select(i => new BoardAction(BoardActionKind.ChooseEffect, null, -1, -1, i % 5, i / 5))).ToList();
            if (pending.optional) candidates.Insert(0, new BoardAction(BoardActionKind.FinishEffect, null, -1, -1, -1, -1));
            foreach (var candidate in candidates)
            {
                string reason;
                if (!game.CanSubmitFor(actor, candidate, out reason)) continue;
                int score = EnemyCardPolicy.ChoiceValue(game.Snapshot, pending, candidate);
                Consider(candidate, new EnemyActionEvaluation(score, pending.effect + " / " + pending.choice + " target value " + score),
                    ref found, ref best, ref evaluation);
            }
            // Mandatory effects must finish even when every option is unfavorable.
            return found;
        }

        private void ConsiderMove(int actor, CardState card, int fromX, int fromY, int toX,
            BoardSnapshot state, EnemyPlacementBoard placementBoard, ref bool found, ref BoardAction best, ref EnemyActionEvaluation bestEvaluation)
        {
            if (visitedTiles.TryGetValue(card.instanceId, out var visited) && visited.Contains(fromY * 5 + toX)) return;
            var candidate = new BoardAction(BoardActionKind.MovePerformer, card.instanceId, fromX, fromY, toX, fromY);
            string rejectedReason;
            if (!game.CanSubmitFor(actor, candidate, out rejectedReason)) return;

            var target = state.tiles[fromY * 5 + toX];
            var opponent = actor == 0 ? target.side1 : target.side0;
            int opponentInfluence = opponent == null ? 0 : (actor == 0 ? target.total1 : target.total0);
            var source = state.tiles[fromY * 5 + fromX];
            var assistant = actor == 0 ? source.assist0 : source.assist1;
            int influence = card.currentInfluence + (assistant == null ? 0 : assistant.currentInfluence);
            var evaluation = EnemyActionScorer.Evaluate(
                Lane(toX, fromY), Lane(fromX, fromY), influence, opponentInfluence, 0, true);
            evaluation = EnemyPlacementScorer.Evaluate(evaluation, card.data.flags, placementBoard,
                toX, fromY, fromX, fromY, true, influence < opponentInfluence);
            Consider(candidate, evaluation, ref found, ref best, ref bestEvaluation);
        }

        private static EnemyPlacementBoard PlacementBoard(BoardSnapshot state, int actor)
        {
            var board = new EnemyPlacementBoard();
            board.forwardDirection = actor == 0 ? 1 : -1;
            for (int y = 0; y < 5; y++) for (int x = 0; x < 5; x++)
            {
                var tile = state.tiles[y * 5 + x];
                board.allies[x, y] = (actor == 0 ? tile.side0 : tile.side1) != null;
                board.enemies[x, y] = (actor == 0 ? tile.side1 : tile.side0) != null;
            }
            return board;
        }

        private static void Consider(BoardAction candidate, EnemyActionEvaluation evaluation,
            ref bool found, ref BoardAction best, ref EnemyActionEvaluation bestEvaluation)
        {
            if (!found || evaluation.score > bestEvaluation.score)
            {
                found = true;
                best = candidate;
                bestEvaluation = evaluation;
            }
        }

        private int Lane(int x, int y)
        {
            return scoringAxis == EnemyAIScoringAxis.Row ? y : x;
        }

        private void LogDecision(int actor, BoardAction action, EnemyActionEvaluation evaluation)
        {
            if (!enableDecisionLogs) return;
            var state = game.Snapshot;
            CardState card = state.Side(actor).hand.Concat(state.Side(actor).discard)
                .Concat(state.tiles.SelectMany(t => actor == 0 ? new[] { t.side0, t.assist0 } : new[] { t.side1, t.assist1 }))
                .FirstOrDefault(c => c != null && c.instanceId == action.cardInstanceId);
            string cardName = card != null && card.data ? card.data.cardName : action.cardInstanceId;
            string actionName = action.kind.ToString();
            Debug.Log("[Enemy AI] Player " + actor + " chose " + actionName + " " + cardName +
                " to (" + action.toColumn + ", " + action.toRow + ") — score " + evaluation.score +
                ". Reason: " + evaluation.reason, this);
        }
    }
}
