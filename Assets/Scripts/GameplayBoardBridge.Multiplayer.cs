using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VocaloidTCG.BoardUI
{
    [Serializable] public sealed class OnlineBoardPacket
    {
        public BoardSnapshot snapshot;
        public int revision, turn;
        public float seconds;
        public string[] placedIds;
        public int[] placedTurns, lastMove;
        public CardState[] opening;
        public int enemyOpeningCount;
        public bool redrawAccepted;
        public bool bothRedrawConfirmed;
        public int[] opponentRedrawSlots;
        public bool resolving;
        public int scoringRow, playEvent;
        public string playedCard;
    }

    public sealed partial class GameplayBoardBridge
    {
        private MultiplayerSession online;
        private int revision, receivedRevision;
        private float onlineSeconds, onlineClockAt;
        private int onlinePlayEvent, receivedPlayEvent;
        private string onlinePlayedCard;
        private bool onlineForfeited;
        private readonly bool[] redrawAccepted = new bool[2], openingComplete = new bool[2];
        private readonly HashSet<string>[] pendingRedraw = new HashSet<string>[2];
        private readonly int[][] redrawSlots = { Array.Empty<int>(), Array.Empty<int>() };
        public bool OnlineRedrawReady { get; private set; }
        public IReadOnlyList<int> OnlineOpponentRedrawSlots => redrawSlots[1 - localPlayerId];
        public bool IsOnline => online != null;
        public bool OnlineAuthority => online && online.Authority;
        private float OnlineRemaining => state != null && (state.phase == RoundPhase.Preparation || state.phase == RoundPhase.Performance)
            ? Mathf.Max(0, onlineSeconds - (Time.unscaledTime - onlineClockAt)) : onlineSeconds;

        public void InitializeOnline(MultiplayerSession session, int actor)
        {
            online = session; localPlayerId = actor; deckCatalog = session.Catalog;
            if(enemyAI) enemyAI.enabled = false;
            ConfigureOnlineDecks();
        }
        private bool ConfigureOnlineDecks()
        {
            var records = online.MatchDecks;
            if(records == null || records.Length != 2){ MatchSetupError = "Missing multiplayer decks."; return false; }
            var mine = records[localPlayerId]; var theirs = records[1 - localPlayerId];
            playerClasses = DeckPresentation.Classes(mine, deckCatalog);
            enemyClasses = DeckPresentation.Classes(theirs, deckCatalog);
            selectedPlayerBack = deckCatalog.BackData(mine); selectedEnemyBack = deckCatalog.BackData(theirs);
            playerCardBack = selectedPlayerBack ? selectedPlayerBack.image : null;
            enemyCardBack = selectedEnemyBack ? selectedEnemyBack.image : null;
            player0Cards = records[0].cardIds.Select(deckCatalog.Card).ToArray();
            player1Cards = records[1].cardIds.Select(deckCatalog.Card).ToArray();
            player0OpeningCard = deckCatalog.Card(records[0].vipCardId);
            player1OpeningCard = deckCatalog.Card(records[1].vipCardId);
            chosenBoard = boardSetups.FirstOrDefault(b => b && b.PlayerClass == playerClasses.FirstOrDefault() && b.EnemyClass == enemyClasses.FirstOrDefault())
                ?? BoardSetupCatalog.Find(playerClasses.FirstOrDefault(), enemyClasses.FirstOrDefault(), multiplayerBoards);
            ConfigurationVersion++; MatchSetupError = ""; return true;
        }
        private bool SubmitOnline(string kind, BoardAction action) => online.Command(kind, action);
        public bool ExecuteOnline(int actor, string kind, BoardAction action, string[] cards, int expectedTurn)
        {
            if(!OnlineAuthority || state == null || actor < 0 || actor > 1) return false;
            if(kind == "redraw"){
                if(!OpeningPending || redrawAccepted[actor]) return false;
                var ids = new HashSet<string>(cards ?? Array.Empty<string>());
                if(ids.Count > decks[actor].Count || ids.Any(id => !openingCards[actor].Any(c => c.instanceId == id))) return false;
                pendingRedraw[actor] = ids; redrawAccepted[actor] = true;
                redrawSlots[actor] = openingCards[actor].Select((card, index) => ids.Contains(card.instanceId) ? index : -1).Where(index => index >= 0).ToArray();
                if(actor == localPlayerId) OpeningRedrawSubmitted = true;
                if(redrawAccepted.All(accepted => accepted)){
                    ExchangeOpeningCards(0, pendingRedraw[0]);
                    ExchangeOpeningCards(1, pendingRedraw[1]);
                    OnlineRedrawReady = true;
                }
                Publish(); return true;
            }
            if(kind == "openingDone"){
                if(!OpeningPending || !OnlineRedrawReady || !redrawAccepted[actor]) return false;
                openingComplete[actor] = true;
                if(openingComplete.All(done => done)){
                    state.phase = RoundPhase.Preparation; state.inputAllowed = true;
                    BeginTurn(state.roundStarterId); Publish();
                }
                return true;
            }
            if(expectedTurn != turnNumber) return false;
            if(kind == "action") return TrySubmitFor(actor, action);
            if(kind == "pass") return TryPassFor(actor);
            return false;
        }
        public void OnlineForfeit(int loser, string reason)
        {
            if(state == null || state.phase == RoundPhase.Finished) return;
            state.phase = RoundPhase.Finished; state.inputAllowed = false; state.winnerId = 1 - loser;
            onlineForfeited = true;
            state.roundSummary = reason; RoundResolutionPending = false;
            if(clock) clock.Stop(); Publish();
        }
        private IEnumerable<CardState> AllCards(BoardSnapshot snapshot)
        {
            return snapshot.side0.hand.Concat(snapshot.side1.hand).Concat(snapshot.tiles.Where(t => t != null)
                .SelectMany(t => new[] { t.side0, t.side1, t.assist0, t.assist1 })).Where(c => c != null);
        }
        public OnlineBoardPacket ExportOnlinePacket(int recipient)
        {
            foreach(var card in AllCards(state)) if(card.data) card.definitionId = card.data.id;
            var copy = JsonUtility.FromJson<BoardSnapshot>(JsonUtility.ToJson(state));
            copy.localPlayerId = recipient;
            if(!onlineForfeited && (copy.phase == RoundPhase.EndRound || copy.phase == RoundPhase.Finished)){
                copy.roundSummary = "Third column: Player +" + (recipient == 0 ? roundScore0 : roundScore1) +
                    " / Opponent +" + (recipient == 0 ? roundScore1 : roundScore0) + ".";
                if(copy.phase == RoundPhase.Finished) copy.roundSummary += copy.winnerId < 0 ? " Draw!" : copy.winnerId == recipient ? " Player wins!" : " Opponent wins!";
            }
            copy.Side(1 - recipient).hand = copy.Side(1 - recipient).hand.Select(c => new CardState {
                instanceId = c.instanceId, ownerId = c.ownerId
            }).ToList();
            return new OnlineBoardPacket { snapshot = copy, revision = ++revision, turn = turnNumber, seconds = RemainingSeconds,
                placedIds = placedTurns.Keys.ToArray(), placedTurns = placedTurns.Values.ToArray(), lastMove = (int[])lastMoveTurn.Clone(),
                opening = openingCards[recipient].Select(c => { c.definitionId = c.data.id; return c; }).ToArray(),
                enemyOpeningCount = openingCards[1 - recipient].Count, redrawAccepted = redrawAccepted[recipient],
                bothRedrawConfirmed = OnlineRedrawReady,
                opponentRedrawSlots = OnlineRedrawReady ? redrawSlots[1 - recipient] : Array.Empty<int>(),
                resolving = RoundResolutionPending, scoringRow = ScoringRow, playEvent = onlinePlayEvent, playedCard = onlinePlayedCard };
        }
        public void SetOnlineClock(float seconds){ onlineSeconds = Mathf.Max(0, seconds); onlineClockAt = Time.unscaledTime; }
        public void ApplyOnlinePacket(OnlineBoardPacket packet)
        {
            if(OnlineAuthority || packet == null || packet.snapshot == null || packet.revision <= receivedRevision) return;
            bool wasMulligan = state == null || state.phase == RoundPhase.Mulligan;
            var previousHand = state == null ? new HashSet<string>() : state.side0.hand.Concat(state.side1.hand).Select(c => c.instanceId).ToHashSet();
            receivedRevision = packet.revision;
            if(state == null) state = packet.snapshot;
            else CopySnapshot(packet.snapshot, state);
            foreach(var tile in state.tiles){
                if(tile == null) continue;
                if(string.IsNullOrEmpty(tile.side0?.instanceId)) tile.side0 = null;
                if(string.IsNullOrEmpty(tile.side1?.instanceId)) tile.side1 = null;
                if(string.IsNullOrEmpty(tile.assist0?.instanceId)) tile.assist0 = null;
                if(string.IsNullOrEmpty(tile.assist1?.instanceId)) tile.assist1 = null;
            }
            foreach(var card in AllCards(state)) card.data = deckCatalog.Card(card.definitionId);
            turnNumber = packet.turn; SetOnlineClock(packet.seconds);
            placedTurns.Clear();
            for(int i = 0; i < packet.placedIds.Length; i++) placedTurns[packet.placedIds[i]] = packet.placedTurns[i];
            Array.Copy(packet.lastMove, lastMoveTurn, 2);
            openingCards[localPlayerId].Clear();
            foreach(var card in packet.opening){ card.data = deckCatalog.Card(card.definitionId); openingCards[localPlayerId].Add(card); }
            openingCards[1 - localPlayerId].Clear();
            for(int i = 0; i < packet.enemyOpeningCount; i++) openingCards[1 - localPlayerId].Add(new CardState());
            OpeningRedrawSubmitted = packet.redrawAccepted;
            OnlineRedrawReady = packet.bothRedrawConfirmed;
            redrawSlots[1 - localPlayerId] = packet.opponentRedrawSlots ?? Array.Empty<int>();
            RoundResolutionPending = packet.resolving; ScoringRow = packet.scoringRow;
            if(packet.playEvent > receivedPlayEvent){
                receivedPlayEvent = packet.playEvent;
                var played = deckCatalog.Card(packet.playedCard);
                if(sfx && played){ sfx.PlayDropSound(); sfx.PlayCardSound(played.playSfx); }
            }
            if(state.phase != RoundPhase.Mulligan && !wasMulligan)
                foreach(var card in state.side0.hand.Concat(state.side1.hand).Where(c => !previousHand.Contains(c.instanceId)))
                    drawPresentations.Enqueue(new DrawPresentation { card = card });
            online.BoardReceived(); Publish();
        }
        private static void CopySnapshot(BoardSnapshot from, BoardSnapshot to)
        {
            to.localPlayerId = from.localPlayerId; to.activePlayerId = from.activePlayerId;
            to.winScore = from.winScore; to.roundNumber = from.roundNumber;
            to.roundStarterId = from.roundStarterId; to.consecutivePasses = from.consecutivePasses;
            to.winnerId = from.winnerId; to.roundSummary = from.roundSummary;
            to.multiplayer = from.multiplayer; to.inputAllowed = from.inputAllowed; to.phase = from.phase;
            to.side0 = from.side0; to.side1 = from.side1; to.tiles = from.tiles;
        }
    }
}
