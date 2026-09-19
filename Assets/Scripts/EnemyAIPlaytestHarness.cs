using UnityEngine;

namespace VocaloidTCG.BoardUI
{
    /// <summary>
    /// Development-only helper for the Enemy AI test scene.
    /// It plays a simple legal card for the local player, then leaves the enemy
    /// turn to EnemyAIController. This makes the AI loop reproducible without UI input.
    /// </summary>
    public sealed class EnemyAIPlaytestHarness : MonoBehaviour
    {
        public GameplayBoardBridge game;
        public float playerActionDelay = 0.4f;
        public int maximumPlayerTurns = 12;
        public bool enableLogs = true;

        private int observedTurn = -1;
        private int completedPlayerTurns;
        private float actionAt;
        private bool finished;

        private void Awake()
        {
            // MCP-driven tests may run while the editor is not the foreground app.
            // Keep only this development scene's simulation advancing in that case.
            Application.runInBackground = true;
        }

        private void Reset()
        {
            game = GetComponent<GameplayBoardBridge>();
        }

        private void Update()
        {
            if (finished || !game || !game.isActiveAndEnabled || game.IsPaused) return;

            var state = game.Snapshot;
            if (state == null || !state.inputAllowed || state.activePlayerId != state.localPlayerId) return;

            if (observedTurn != game.TurnNumber)
            {
                observedTurn = game.TurnNumber;
                actionAt = Time.unscaledTime + playerActionDelay;
                return;
            }

            if (Time.unscaledTime < actionAt) return;
            TakePlayerTurn(state.localPlayerId);
        }

        private void TakePlayerTurn(int actor)
        {
            if (completedPlayerTurns >= maximumPlayerTurns)
            {
                finished = true;
                Log("Stopped after " + completedPlayerTurns + " automated player turns.");
                return;
            }

            completedPlayerTurns++;
            var state = game.Snapshot;
            BoardAction best = default(BoardAction);
            EnemyActionEvaluation bestEvaluation = new EnemyActionEvaluation(int.MinValue, "no legal play");
            bool found = false;

            foreach (var card in state.Side(actor).hand)
            {
                for (int y = 0; y < 5; y++) for (int x = 0; x < 5; x++)
                {
                    var candidate = new BoardAction(BoardActionKind.PlayCard, card.instanceId, -1, -1, x, y);
                    string rejectedReason;
                    if (!game.CanSubmitFor(actor, candidate, out rejectedReason)) continue;

                    var tile = state.tiles[y * 5 + x];
                    var friendly = tile.side0;
                    var opponent = tile.side1;
                    int resultingInfluence = card.data.stageEffect && friendly != null
                        ? Mathf.Max(0, friendly.currentInfluence + card.data.stageInfluenceChange)
                        : card.currentInfluence;
                    var evaluation = EnemyActionScorer.Evaluate(
                        x, x, resultingInfluence, opponent == null ? 0 : tile.total1,
                        card.currentCost, false);

                    if (!found || evaluation.score > bestEvaluation.score)
                    {
                        found = true;
                        best = candidate;
                        bestEvaluation = evaluation;
                    }
                }
            }

            if (found)
            {
                Log("Player automation chose " + best.cardInstanceId + " at (" + best.toColumn + ", " + best.toRow +
                    ") — score " + bestEvaluation.score + ". " + bestEvaluation.reason);
                game.TrySubmitFor(actor, best);
            }
            else
            {
                Log("Player automation passes: no legal card at energy " + state.Side(actor).energy + ".");
                game.TryPassFor(actor);
            }
        }

        private void Log(string message)
        {
            if (enableLogs) Debug.Log("[Enemy AI Playtest] " + message, this);
        }
    }
}
