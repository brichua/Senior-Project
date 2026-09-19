using UnityEngine;

namespace VocaloidTCG.BoardUI
{
    public enum EnemyAIScoringAxis { Row, Column }

    /// <summary>
    /// The first official enemy AI. It examines legal actions, scores them with
    /// EnemyActionScorer, performs the best positive action, or passes.
    /// </summary>
    public sealed class EnemyAIController : MonoBehaviour
    {
        public GameplayBoardBridge game;
        [Min(0.1f)] public float actionDelay = 0.75f;
        [Tooltip("Use the same axis that GameplayBoardBridge uses for end-of-round scoring.")]
        public EnemyAIScoringAxis scoringAxis = EnemyAIScoringAxis.Column;
        public bool enableDecisionLogs = true;

        private int observedTurn = -1;
        private float wait;

        private void Update()
        {
            if (!game || !game.isActiveAndEnabled || game.IsPaused) return;
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
            BoardAction action;
            EnemyActionEvaluation evaluation;
            if (ChooseAction(actor, out action, out evaluation))
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

        private bool ChooseAction(int actor, out BoardAction best, out EnemyActionEvaluation bestEvaluation)
        {
            best = default(BoardAction);
            bestEvaluation = new EnemyActionEvaluation(int.MinValue, "no legal action");
            var state = game.Snapshot;
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
                    int resultingInfluence = card.data.stageEffect && friendly != null
                        ? Mathf.Max(0, friendly.currentInfluence + card.data.stageInfluenceChange)
                        : card.currentInfluence;
                    int opponentInfluence = opponent == null ? 0 : (actor == 0 ? tile.total1 : tile.total0);
                    var evaluation = EnemyActionScorer.Evaluate(
                        Lane(x, y), -1, resultingInfluence, opponentInfluence, card.currentCost, false);
                    Consider(candidate, evaluation, ref found, ref best, ref bestEvaluation);
                }
            }

            for (int y = 0; y < 5; y++) for (int x = 0; x < 5; x++)
            {
                var source = state.tiles[y * 5 + x];
                var card = actor == 0 ? source.side0 : source.side1;
                if (card == null) continue;

                ConsiderMove(actor, card, x, y, x - 1, state, ref found, ref best, ref bestEvaluation);
                ConsiderMove(actor, card, x, y, x + 1, state, ref found, ref best, ref bestEvaluation);
            }

            // A deterministic tie-breaker comes from the candidate generation order.
            return found && bestEvaluation.score > 0;
        }

        private void ConsiderMove(int actor, CardState card, int fromX, int fromY, int toX,
            BoardSnapshot state, ref bool found, ref BoardAction best, ref EnemyActionEvaluation bestEvaluation)
        {
            var candidate = new BoardAction(BoardActionKind.MovePerformer, card.instanceId, fromX, fromY, toX, fromY);
            string rejectedReason;
            if (!game.CanSubmitFor(actor, candidate, out rejectedReason)) return;

            var target = state.tiles[fromY * 5 + toX];
            var opponent = actor == 0 ? target.side1 : target.side0;
            int opponentInfluence = opponent == null ? 0 : (actor == 0 ? target.total1 : target.total0);
            var evaluation = EnemyActionScorer.Evaluate(
                Lane(toX, fromY), Lane(fromX, fromY), card.currentInfluence, opponentInfluence, 0, true);
            Consider(candidate, evaluation, ref found, ref best, ref bestEvaluation);
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
            CardState card = action.kind == BoardActionKind.PlayCard
                ? state.Side(actor).hand.Find(c => c.instanceId == action.cardInstanceId)
                : (actor == 0 ? state.tiles[action.fromRow * 5 + action.fromColumn].side0 :
                    state.tiles[action.fromRow * 5 + action.fromColumn].side1);
            string cardName = card != null && card.data ? card.data.cardName : action.cardInstanceId;
            string actionName = action.kind == BoardActionKind.PlayCard ? "play" : "move";
            Debug.Log("[Enemy AI] Player " + actor + " chose " + actionName + " " + cardName +
                " to (" + action.toColumn + ", " + action.toRow + ") — score " + evaluation.score +
                ". Reason: " + evaluation.reason, this);
        }
    }
}
