using UnityEngine;

namespace VocaloidTCG.BoardUI
{
    public sealed class BasicEnemyAITest : MonoBehaviour
    {
        public GameplayBoardBridge game;
        [Min(0.1f)] public float actionDelay = 0.75f;
        private int observedTurn = -1;
        private float wait;

        private void Update(){
            if(!game || !game.isActiveAndEnabled || game.IsPaused) return;
            var state = game.Snapshot;
            if(state == null || state.multiplayer || !state.inputAllowed ||
                state.activePlayerId == state.localPlayerId ||
                (state.phase != RoundPhase.Preparation && state.phase != RoundPhase.Performance)) return;
            if(observedTurn != game.TurnNumber){
                observedTurn = game.TurnNumber; wait = Mathf.Max(0.1f, actionDelay);
            }
            wait -= Time.unscaledDeltaTime;
            if(wait > 0) return;
            wait = Mathf.Max(0.1f, actionDelay);
            int actor = state.activePlayerId;
            BoardAction action;
            if(ChooseAction(actor, out action)) game.TrySubmitFor(actor, action);
            else game.TryPassFor(actor);
        }

        private bool ChooseAction(int actor, out BoardAction best){
            best = default(BoardAction);
            bool found = false;
            int bestValue = int.MinValue;
            var state = game.Snapshot;
            foreach(var card in state.Side(actor).hand){
                for(int y = 0; y < 5; y++) for(int x = 0; x < 5; x++){
                    var action = new BoardAction(BoardActionKind.PlayCard, card.instanceId, -1, -1, x, y);
                    string reason;
                    if(!game.CanSubmitFor(actor, action, out reason)) continue;
                    int value = (x == 2 ? 100 : 0) + card.currentInfluence;
                    if(card.data.flags != null && card.data.flags.Contains("center") && x == 2) value += 10;
                    if(!found || value > bestValue){ best = action; bestValue = value; found = true; }
                }
            }
            if(found) return true;
            for(int y = 0; y < 5; y++) for(int x = 0; x < 5; x++){
                var tile = state.tiles[y * 5 + x];
                var card = actor == 0 ? tile.side0 : tile.side1;
                if(card == null || x == 2) continue;
                int destination = x + (x < 2 ? 1 : -1);
                var action = new BoardAction(BoardActionKind.MovePerformer, card.instanceId, x, y, destination, y);
                string reason;
                if(game.CanSubmitFor(actor, action, out reason)){ best = action; return true; }
            }
            return false;
        }
    }
}
