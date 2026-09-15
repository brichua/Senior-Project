using System;
using System.Collections.Generic;
using UnityEngine;

namespace VocaloidTCG.BoardUI
{
    public enum RoundPhase { Mulligan, Preparation, Performance, Finished, EndRound }
    public enum BoardActionKind { PlayCard, MovePerformer }

    [Serializable]
    public sealed class CardState{
        public string instanceId;
        public int ownerId;
        public CardData data;
        public int currentInfluence, currentCost;
        public bool hasInfluence = true;
        public bool isClassCard;
        public List<string> activeEffects = new List<string>();
    }

    [Serializable]
    public sealed class TileState{
        public CardState side0, side1, assist0, assist1;
        public int total0, total1;
    }

    [Serializable]
    public sealed class SideState{
        public int energy, score, deckCount;
        public List<CardState> hand = new List<CardState>();
        public int hiddenHandCount;
    }

    [Serializable]
    public sealed class BoardSnapshot{
        public int localPlayerId;
        public int activePlayerId;
        public int winScore = 20;
        public int roundNumber = 1, roundStarterId, consecutivePasses;
        public int winnerId = -1;
        public string roundSummary = "";
        public bool multiplayer, inputAllowed = true;
        public RoundPhase phase = RoundPhase.Preparation;
        public SideState side0 = new SideState(), side1 = new SideState();
        public TileState[] tiles = new TileState[25];
        public SideState Side(int id) { return id == 0 ? side0 : side1; }
    }

    public struct BoardAction{
        public BoardActionKind kind;
        public string cardInstanceId;
        public int fromColumn, fromRow;
        public int toColumn, toRow;
        public BoardAction(BoardActionKind kind, string id, int fromColumn, int fromRow, int toColumn, int toRow){
            this.kind = kind; cardInstanceId = id;
            this.fromColumn = fromColumn; this.fromRow = fromRow;
            this.toColumn = toColumn; this.toRow = toRow;
        }
    }

    public abstract class BoardGameBridge : MonoBehaviour{
        public event Action Changed;
        public abstract BoardSnapshot Snapshot{ get; }
        public abstract float RemainingSeconds{ get; }
        public abstract bool CanSubmit(BoardAction action, out string reason);
        public virtual bool TryPreviewContest(BoardAction action, CardState incoming, out int localInfluence, out int enemyInfluence){
            localInfluence = enemyInfluence = 0;
            var snapshot = Snapshot;
            if (snapshot == null || incoming == null || incoming.ownerId != snapshot.localPlayerId || action.toColumn < 0 || action.toColumn > 4 || action.toRow < 0 || action.toRow > 4) return false;
            int index = action.toRow * 5 + action.toColumn;
            if (snapshot.tiles == null || index >= snapshot.tiles.Length) return false;
            var tile = snapshot.tiles[index];
            if (tile == null) return false;
            bool local0 = snapshot.localPlayerId == 0;
            var friendly = local0 ? tile.side0 : tile.side1;
            var enemy = local0 ? tile.side1 : tile.side0;
            if (friendly != null || enemy == null) return false;
            localInfluence = incoming.currentInfluence;
            enemyInfluence = local0 ? tile.total1 : tile.total0;
            return true;
        }

        public abstract bool TrySubmit(BoardAction action);
        public abstract void RequestPass();
        public abstract void SetLocalPause(bool paused);
        protected void Publish(){
            if(Changed != null) Changed();
        }
    }
}
