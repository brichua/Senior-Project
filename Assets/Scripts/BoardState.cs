using System;
using System.Collections.Generic;
using UnityEngine;

namespace VocaloidTCG.BoardUI
{
    public enum RoundPhase { Mulligan, Preparation, Performance, Finished, EndRound }
    public enum BoardActionKind { PlayCard, MovePerformer, ChooseEffect, FinishEffect, ActivateEffect }

    public enum EffectChoice { None, Ally, Enemy, AdjacentAlly, TokenTile, MoveAlly, MoveTile, OccupiedTile, HandDiscard, HandPerformer, DeckPerformer, DiscardPerformer, SacrificeAlly, RinEnemy, StrongDeckPerformer, Extended, Duet }

    [Serializable] public sealed class PendingCardEffect {
        public int actor, sourceTile, amount, remaining, stage;
        public CardState source;
        public CardEffect effect;
        public EffectChoice choice;
        public string selectedId, prompt;
        public bool optional;
        public CardState selected, created;
        public List<int> tiles = new List<int>();
        public List<CardState> cards = new List<CardState>();
        public List<string> eligibleIds = new List<string>();
        public List<CardState> options = new List<CardState>();
        public PerformerKeywords keyword;
    }

    [Serializable] public sealed class DelayedStageToken {
        public CardState source;
        public int tile, round;
    }

    [Serializable]
    public sealed class CardState{
        public string instanceId;
        public int ownerId;
        [NonSerialized] public CardData data;
        public string definitionId;
        public int currentInfluence, currentCost;
        public bool shieldActive;
        public bool hasInfluence = true;
        public bool isClassCard;
        public List<string> activeEffects = new List<string>();
        public bool token, silenced, grantedEncore;
        public int tokenCost, createdDiscount, protectedUntilRound = -1, sharedProtectionLevel, protectionValue;
        public bool createdCard, soloProtectionCondition;
        public PerformerKeywords tokenKeywords, gainedKeywords, sharedKeywords;
        public int tokenInfluence, auraInfluence, roundInfluence, enteredRound, activatedTurn = -1, nimbleRound = -1;
        public int activatedRound = -1;
        public int rootedRound = -1, protectedTurn = -1, noScoreRound = -1, inheritedHarmonize, inheritedSoloist;
        public bool inheritedStageShare, inheritedEncore, inheritedNimble;
        public string sharedTargetId;
        public string effectProgress;
        public PerformerKeywords roundKeywords, suppressedKeywords;
        public int triggerRound = -1, silenceRound = -1;
        public List<int> visitedColumns = new List<int>();
        public string linkedTokenId;
        public List<string> keywordAuraSources = new List<string>();
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
        public List<CardState> discard = new List<CardState>();
        public int nextSpellDiscount, nextSpellTax, performersPlayed, nextRoundEnergyBonus;
        public bool harmonizeVenue;
        public bool positionKeywordVenue;
        public List<int> nextPerformerColumns = new List<int>();
        public int alliesLeftThisRound, alliesLeftLastRound, nextPerformerInfluence;
        public bool leaveDiscardScoreVenue;
        public int contestedWinBonus, contestedWinUsedRound = -1;
        public int observedScore;
        public List<CardState> venueEffects = new List<CardState>();
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
        public PendingCardEffect pendingEffect;
        public List<PendingCardEffect> queuedEffects = new List<PendingCardEffect>();
        public List<DelayedStageToken> delayedTokens = new List<DelayedStageToken>();
        public List<TileWinReward> tileWinRewards = new List<TileWinReward>();
        public List<CardState> revealedCards = new List<CardState>();
        public string revealMessage;
        public int[] roundTileBonus0 = new int[25], roundTileBonus1 = new int[25];
        public bool[] tileDraw0 = new bool[25], tileDraw1 = new bool[25];
        public SideState Side(int id) { return id == 0 ? side0 : side1; }
    }

    [Serializable] public sealed class TileWinReward {
        public int actor, tile, score, draws;
        public bool contestedOnly;
        public string performerId;
    }

    [Serializable]
    public struct BoardAction{
        public BoardActionKind kind;
        public string cardInstanceId;
        public string targetInstanceId;
        public int fromColumn, fromRow;
        public int toColumn, toRow;
        public BoardAction(BoardActionKind kind, string id, int fromColumn, int fromRow, int toColumn, int toRow, string targetInstanceId = null){
            this.kind = kind; cardInstanceId = id;
            this.targetInstanceId = targetInstanceId;
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
