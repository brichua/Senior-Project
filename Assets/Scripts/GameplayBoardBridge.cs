using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VocaloidTCG.BoardUI
{
    public sealed partial class GameplayBoardBridge : BoardGameBridge
    {
        public SFXManager sfx;
        public VoicelinesManager voicelines;
        [Header("Saved deck and class-based boards")]
        public bool useSelectedDeck = true;
        public DeckCatalog deckCatalog;
        public DeckData enemyDeck;
        public EnemyAIController enemyAI;
        public List<CharacterClassData> playerClasses = new List<CharacterClassData>();
        public List<CharacterClassData> enemyClasses = new List<CharacterClassData>();
        public List<BoardSetup> boardSetups = new List<BoardSetup>();
        [Tooltip("Class matchup boards for multiplayer, story, and puzzles. This catalog can be stored anywhere in Assets.")]
        public BoardSetupCatalog multiplayerBoards;
        public Sprite playerCardBack, enemyCardBack;
        public string MatchSetupError { get; private set; }
        public int ConfigurationVersion { get; private set; }
        private BoardSetup chosenBoard;
        private CardBackData selectedPlayerBack, selectedEnemyBack;

        private bool ConfigureDecks()
        {
            if(IsOnline) return ConfigureOnlineDecks();
            if(IsTutorial) return ConfigureTutorialDecks();
            MatchSetupError = "";
            selectedPlayerBack = selectedEnemyBack = null;
            if (!enemyAI) enemyAI = GetComponent<EnemyAIController>();
            if(useSelectedDeck) {
                var catalog = deckCatalog ? deckCatalog : Resources.Load<DeckCatalog>("DeckCatalog");
                if(catalog) {
                    try {
                        var library = DeckLibrary.Get(catalog); catalog = library.Catalog;
                        var selected = IsStory ? storyPlayerDeck : library.Selected;
                        string error;
                        if(!DeckRules.Validate(selected, catalog, library.Owned, out error)) {
                            MatchSetupError = "Select a valid deck: " + error; return false;
                        }
                        var cards = selected.cardIds.Select(catalog.Card).ToArray();
                        if(localPlayerId == 0) { player0Cards = cards; player0OpeningCard = catalog.Card(selected.vipCardId); }
                        else { player1Cards = cards; player1OpeningCard = catalog.Card(selected.vipCardId); }
                        playerClasses = DeckPresentation.Classes(selected, catalog);
                        selectedPlayerBack = catalog.BackData(selected);
                        playerCardBack = selectedPlayerBack ? selectedPlayerBack.image : null;
                    } catch(System.Exception ex) { MatchSetupError = ex.Message; return false; }
                }
            }
            if(enemyDeck) {
                var cards = enemyDeck.cards.Where(c => c).ToArray();
                if(localPlayerId == 0) { player1Cards = cards; player1OpeningCard = enemyDeck.vipCard; }
                else { player0Cards = cards; player0OpeningCard = enemyDeck.vipCard; }
                enemyClasses = enemyDeck.classes.Where(c => c).OrderByDescending(c => enemyDeck.cards.Count(card => card && card.cardClass == c) + (enemyDeck.vipCard && enemyDeck.vipCard.cardClass == c ? 1 : 0)).ToList();
                
                selectedEnemyBack = enemyDeck.cardBack;
                enemyCardBack = selectedEnemyBack ? selectedEnemyBack.image : null;
            } else if(enemyAI) enemyClasses = new List<CharacterClassData>(enemyAI.classes);
            if(enemyAI) { enemyAI.game = this; enemyAI.classes = new List<CharacterClassData>(enemyClasses); }
            chosenBoard = FindBoardSetup(playerClasses.FirstOrDefault(), enemyClasses.FirstOrDefault());
            ConfigurationVersion++;
            return true;
        }
        private BoardSetup FindBoardSetup(CharacterClassData player, CharacterClassData enemy)
        {
            return boardSetups.FirstOrDefault(b => b && b.PlayerClass == player && b.EnemyClass == enemy)
                ?? BoardSetupCatalog.Find(player, enemy, multiplayerBoards);
        }

        public BoardSetup CreatePresentationSetup(BoardSetup fallback)
        {
            var template = chosenBoard ? chosenBoard : fallback;
            if(!template) return null;
            var source = Instantiate(template);
            if(playerClasses.Count > 0) source.playerClass = playerClasses[0];
            if(enemyClasses.Count > 0) source.enemyClass = enemyClasses[0];
            var result = source.CreateRuntimeSetup(selectedPlayerBack, selectedEnemyBack);
            Destroy(source);
            if(!selectedPlayerBack && playerCardBack) result.player.cardBack = playerCardBack;
            if(!selectedEnemyBack && enemyCardBack) result.enemy.cardBack = enemyCardBack;
            return result;
        }
        public const int MaxHandSize = 8;
        [Range(0, 1)] public int localPlayerId;
        [Tooltip("Full decks excluding opening card")]
        public CardData[] player0Cards, player1Cards;
        public CardData player0OpeningCard, player1OpeningCard;
        public LocalTurnClock clock;
        [Min(1)] public int winningScore = 50;
        [Min(0)] public float turnSeconds = 60;
        [Min(0)] public float endRoundDisplaySeconds = 2;
        [Min(1)] public int minimumRoundEnergy = 1;
        public bool startAutomatically = true;
        public bool enableOpeningSequence = true;
        [HideInInspector] public MatchResultAnimator resultPresenter;
        public bool enableDebugLogs = true;

        private BoardSnapshot state;
        private int serial, turnNumber;
        private bool turnHadAction;
        private float roundWait;
        private bool deckExhausted;
        private bool approachingEndVoicelinePlayed;
        public event System.Action<VoicelineEvent> LocalVoicelineRequested;
        public bool DepartureVoicelinePlayed { get; private set; }
        public void PlayLocalVoiceline(VoicelineEvent reaction){
            if(reaction==VoicelineEvent.Multiplayer) DepartureVoicelinePlayed=true;
            if(LocalVoicelineRequested!=null) LocalVoicelineRequested(reaction);
            else if(voicelines && playerClasses.Count>0 && playerClasses[0]) voicelines.PlayVoiceline(playerClasses[0].identity,reaction);
        }
        public struct DrawPresentation {
            public CardState card;
            public bool discarded;
        }
        private readonly Queue<DrawPresentation> drawPresentations = new Queue<DrawPresentation>();
        public bool DrawAnimationPlaying { get; private set; }
        public bool PhaseAnimationPlaying { get; private set; }

        public void SetPhaseAnimationPlaying(bool playing){
            PhaseAnimationPlaying = playing;
            if(clock) clock.PhaseAnimationPlaying = !IsOnline && playing;
        }

        public bool TryTakeDrawPresentation(out DrawPresentation draw){
            draw = default(DrawPresentation);
            if(drawPresentations.Count == 0) return false;
            draw = drawPresentations.Dequeue();
            return true;
        }

        public void SetDrawAnimationPlaying(bool playing){
            DrawAnimationPlaying = playing;
            if(clock) clock.DrawAnimationPlaying = !IsOnline && playing;
        }
        private readonly Queue<CardState>[] decks = { new Queue<CardState>(), new Queue<CardState>() };
        private readonly int[] lastMoveTurn = { -1, -1 };
        private readonly int[] lastDrawRound = { 0, 0 };
        private readonly Dictionary<string, int> placedTurns = new Dictionary<string, int>();

        public override BoardSnapshot Snapshot { get { return state; } }
        public override float RemainingSeconds { get { return IsOnline && !OnlineAuthority ? OnlineRemaining : clock ? clock.RemainingSeconds : 0; } }
        public int TurnNumber { get { return turnNumber; } }
        public bool IsPaused { get { return clock && clock.Paused; } }

        private void Awake(){
            if(!clock) clock = gameObject.AddComponent<LocalTurnClock>();
            clock.Expired += OnTurnExpired;
            if(MultiplayerSession.Instance && MultiplayerSession.Instance.InMatch){
                MultiplayerSession.Instance.AttachBoard(this); return;
            }
            if(PuzzleLaunch.Pending) {
                puzzle = PuzzleLaunch.Pending; deckCatalog = PuzzleLaunch.Catalog;
                PuzzleReturnScene = PuzzleLaunch.ReturnScene; PuzzleLaunch.Clear();
            }
            ConsumeStoryLaunch();
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
            if(IsOnline && (!OnlineAuthority || state != null)) return;
            if(!string.IsNullOrEmpty(storyLaunchError)) { MatchSetupError = storyLaunchError; Publish(); return; }
            if(IsStory && !string.IsNullOrEmpty(StorySaveError)) return;
            StorySaveError = "";
            FirstClearReward = null;
            RoundResolutionPending = false;
            if(puzzle) { StartPuzzle(); return; }
            if(firstPlayerId < -1 || firstPlayerId > 1){ Log("Match start rejected: invalid starting player."); return; }
            if(!ConfigureDecks()) { Debug.LogError(MatchSetupError, this); Publish(); return; }
            if(clock){ clock.Stop(); clock.Paused = false; clock.EffectInputPending=false; }
            drawPresentations.Clear();
            SetDrawAnimationPlaying(false);
            serial = turnNumber = 0;
            turnHadAction = false;
            deckExhausted = false;
            approachingEndVoicelinePlayed = false;
            DepartureVoicelinePlayed = false;
            placedTurns.Clear();
            for(int i = 0; i < 2; i++){
                lastMoveTurn[i] = -1;
                lastDrawRound[i] = 1;
            }
            int starter = IsTutorial ? localPlayerId : firstPlayerId < 0 ? Random.Range(0, 2) : firstPlayerId;
            state = new BoardSnapshot {
                multiplayer = IsOnline,
                localPlayerId = Mathf.Clamp(localPlayerId, 0, 1), roundStarterId = starter,
                winScore = Mathf.Max(1, winningScore)
            };
            for (int i = 0; i < 25; i++) state.tiles[i] = new TileState();
            Log("Match started. Player " + starter + " goes first.");
            BuildDeck(player0Cards, 0); BuildDeck(player1Cards, 1);
            AddOpeningCard(player0OpeningCard, 0); AddOpeningCard(player1OpeningCard, 1);
            DrawCards(0, 3); DrawCards(1, 3);
            RefillRoundEnergy();
            if(IsTutorial) PrepareTutorialBoard();
            if(enableOpeningSequence){
                PrepareOpeningHand();
                Publish();
                return;
            }
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
            var card = new CardState {
                instanceId = "match-" + (++serial), ownerId = actor, data = asset,
                currentCost = Mathf.Max(0, asset.cost), currentInfluence = Mathf.Max(0, asset.influence),
                enteredRound = state != null ? state.roundNumber : 1,
                hasInfluence = asset.performer
            };
            CardInfluence.Reset(card);
            return card;
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
                drawPresentations.Enqueue(new DrawPresentation {
                    card = card, discarded = side.hand.Count >= MaxHandSize
                });
                if (side.hand.Count >= MaxHandSize)
                {
                    discarded++;
                    Discard(card);
                    Log("Player " + actor + " discarded newly drawn card " + card.data.cardName + " because their hand is full (" + MaxHandSize + ").");
                }
                else side.hand.Add(card);
            }
            side.deckCount = decks[actor].Count;
            side.hiddenHandCount = side.hand.Count;
            Log("Player " + actor + " drew " + drawn + " card(s); discarded: " + discarded + ". Hand: " + side.hand.Count + "; deck: " + side.deckCount + ".");
            if(side.deckCount == 0 && !IsPuzzle){
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
                state.inputAllowed && state.activePlayerId == actor && !IsPaused && (IsOnline || (!DrawAnimationPlaying && !PhaseAnimationPlaying));
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
            if(tutorialDirector && !tutorialDirector.Allows(actor, action, out reason)) return false;
            if(action.kind==BoardActionKind.ChooseEffect || action.kind==BoardActionKind.FinishEffect){
                reason="Choose one of the highlighted effect targets."; return CanChooseEffect(actor,action);
            }
            reason = "Wait for your turn in an active phase.";
            if(!CanAct(actor)) return false;
            reason="Finish choosing targets for the current effect.";
            if(state.pendingEffect!=null) return false;
            if(action.kind==BoardActionKind.ActivateEffect){
                reason="This ability is not available.";
                return CanActivateEffect(FieldCards(actor).FirstOrDefault(c=>c.instanceId==action.cardInstanceId));
            }
            reason = "Invalid board action or destination.";
            if(!InBounds(action.toColumn, action.toRow) ||
                (action.kind != BoardActionKind.PlayCard && action.kind != BoardActionKind.MovePerformer)) return false;
            var card = Find(actor, action);
            reason = "The card must belong to you and have exactly one card type.";
            if(card == null || card.ownerId != actor || !card.data || card.data.performer == card.data.stageEffect) return false;
            var target = state.tiles[action.toRow * 5 + action.toColumn];
            if(action.kind == BoardActionKind.PlayCard){
                reason = "Not enough energy.";
                if(CardCost(card) > state.Side(actor).energy) return false;
                if(card.data.stageEffect && !card.token){
                    reason="Only Cheer Effects can be played during Performance.";
                    if(state.phase==RoundPhase.Performance && !card.data.cheerEffect) return false;
                    reason = "Choose a legal target for this stage effect.";
                    if(!SpellTarget(card,action.toRow*5+action.toColumn)) return false;
                    if(!string.IsNullOrEmpty(action.targetInstanceId) && !StageTargets(card,action.toRow*5+action.toColumn).Any(c=>c.instanceId==action.targetInstanceId)) return false;
                    reason = ""; return true;
                }
                reason="Discard another card from your hand to play this performer.";
                if(HasEffect(card,CardEffect.DiscardToPlay) && state.Side(actor).hand.Count<2) return false;
            }
            reason = "Performers can only be placed or moved during preparation.";
            if((!card.data.performer && !card.token) || state.phase != RoundPhase.Preparation) return false;
            reason = "You already have a performer on that tile.";
            if(Performer(target, actor) != null && !(action.kind==BoardActionKind.PlayCard && StageShare(card) && Assistant(target,actor)==null)) return false;
            if(action.kind == BoardActionKind.MovePerformer){
                int placed;
                if(!placedTurns.TryGetValue(card.instanceId, out placed)) placed = -1;
                reason = "Move one performer one square horizontally per turn, after its placement turn.";
                if(card.rootedRound==state.roundNumber) { reason="This performer cannot move this round."; return false; }
                if(action.fromRow != action.toRow || Mathf.Abs(action.fromColumn - action.toColumn) != 1 ||
                    placed >= turnNumber || (lastMoveTurn[actor] >= turnNumber && !(Nimble(card) && card.nimbleRound!=turnNumber))) return false;
            }else{
                reason = "Place in your leftmost three columns or adjacent to a friendly performer.";
                if(Performer(target,actor)==null && !CanPlace(actor, action.toColumn, action.toRow)) return false;
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
            if(IsOnline) return SubmitOnline("action", action);
            return TrySubmitFor(state == null ? -1 : state.localPlayerId, action);
        }

        public bool TrySubmitFor(int actor, BoardAction action){
            if(IsOnline && !OnlineAuthority) return false;
            if(tutorialDirector && !tutorialDirector.Allows(actor, action, out _)) return false;
            if(action.kind==BoardActionKind.ChooseEffect || action.kind==BoardActionKind.FinishEffect) return ResolveChoice(actor,action);
            if(action.kind==BoardActionKind.ActivateEffect) return ActivateEffect(actor,action);
            string reason;
            if(!CanSubmitFor(actor, action, out reason)){
                Log("Player " + actor + " action rejected: " + reason); return false;
            }
            var card = Find(actor, action);
            var target = state.tiles[action.toRow * 5 + action.toColumn];
            if(action.kind == BoardActionKind.PlayCard){
                var side = state.Side(actor);
                side.hand.Remove(card); side.hiddenHandCount = side.hand.Count;
                int paid=CardCost(card); side.energy -= paid;
                if(card.data.stageEffect && !card.token){
                    side.nextSpellDiscount=side.nextSpellTax=0;
                    var spellTarget=AtTile(action.toRow*5+action.toColumn).FirstOrDefault(c=>c.instanceId==action.targetInstanceId);
                    PlayStageEffect(card,action.toRow*5+action.toColumn,spellTarget);
                }else{
                    if(Performer(target,actor)!=null) SetAssistant(target,actor,card); else SetPerformer(target, actor, card);
                    placedTurns[card.instanceId] = turnNumber;
                    PerformerPlayed(card,action.toRow*5+action.toColumn);
                    Log("Player " + actor + " placed " + card.data.cardName + " at (" + action.toColumn + ", " + action.toRow + ").");
                }
                Log("Player " + actor + " spent " + card.currentCost + " energy; remaining: " + side.energy + ". Hand: " + side.hand.Count + ".");
            }else{
                MoveWithPartner(action.fromRow*5+action.fromColumn,action.toRow*5+action.toColumn,actor);
                if (enemyAI) enemyAI.ObserveAcceptedMove(action);
                if(Nimble(card) && card.nimbleRound!=turnNumber) card.nimbleRound=turnNumber;
                else lastMoveTurn[actor] = turnNumber;
                Log("Player " + actor + " moved " + card.data.cardName + " from (" + action.fromColumn + ", " + action.fromRow +
                    ") to (" + action.toColumn + ", " + action.toRow + ").");
            }
            turnHadAction = true;
            state.consecutivePasses = 0;
            Recompute();
            if(IsOnline && action.kind == BoardActionKind.PlayCard){ onlinePlayedCard = card.data.id; onlinePlayEvent++; }
            if(action.kind == BoardActionKind.PlayCard && !card.token && (card.data.venueEffect || card.data.effect == CardEffect.HarmonizeVenue)){
                var venues = state.Side(actor).venueEffects;
                if(!venues.Exists(c => c.data == card.data)) venues.Add(card);
            }
            if (action.kind == BoardActionKind.PlayCard && sfx)
            {
                sfx.PlayDropSound();
                sfx.PlayCardSound(card.data.playSfx);
            }
            if(IsPuzzle && state.pendingEffect==null) CheckPuzzle(false, false);
            if(tutorialDirector) tutorialDirector.ActionAccepted(actor, action);
            Publish();
            return true;
        }

        public override void RequestPass(){
            if(IsOnline){ SubmitOnline("pass", default(BoardAction)); return; }
            if(state != null) TryPassFor(state.localPlayerId);
        }

        public bool TryPassFor(int actor){
            if(IsOnline && !OnlineAuthority) return false;
            if(tutorialDirector && !tutorialDirector.AllowsPass(actor)) return false;
            if(!CanAct(actor) || state.pendingEffect!=null){ Log("Player " + actor + " pass rejected: finish the current action first."); return false; }
            foreach(var c in FieldCards(actor).ToArray()) CardInfluence.Regenerate(c);
            if(IsPuzzle && actor == state.localPlayerId) puzzlePlayerTurns++;
            state.consecutivePasses = turnHadAction ? 0 : state.consecutivePasses + 1;
            Log("Player " + actor + " ended their turn. Consecutive empty turns: " + state.consecutivePasses + ".");
            if(state.consecutivePasses < 2) BeginTurn(1 - actor);
            else if(state.phase == RoundPhase.Preparation){
                state.consecutivePasses = 0; state.phase = RoundPhase.Performance;
                Log("Preparation complete. Performance begins.");
                BeginTurn(state.roundStarterId);
            }else ResolveRound();
            if(tutorialDirector) tutorialDirector.PassAccepted(actor);
            Publish(); return true;
        }

        private void OnTurnExpired(){
            if(IsPuzzle) return;
            if(IsOnline && !OnlineAuthority) return;
            if(state != null){
                Log("Player " + state.activePlayerId + " timer expired; passing turn.");
                TryPassFor(state.activePlayerId);
            }
        }

        private void BeginTurn(int actor){
            if(IsPuzzle && actor == state.localPlayerId && CheckPuzzle(false, true)) return;
            state.activePlayerId = actor; turnNumber++;
            turnHadAction = false;
            if(lastDrawRound[actor] < state.roundNumber){
                lastDrawRound[actor] = state.roundNumber;
                if(IsPuzzle) GivePuzzleRoundCards(actor);
                else DrawCards(actor, 1 + FieldCards(actor).Count(c=>HasEffect(c,CardEffect.DrawAndRevive)));
            }
            Log("Player " + actor + " turn begins. Remaining energy: " + state.Side(actor).energy + ".");
            Recompute();
            StartClock();
            if (actor == state.localPlayerId && state.phase == RoundPhase.Preparation) PlayApproachingEndVoiceline();
        }

        private void RefillRoundEnergy(){
            int energy = Mathf.Clamp(state.roundNumber, 1, 8);
            state.side0.energy = Mathf.Min(8,energy+state.side0.nextRoundEnergyBonus);
            state.side1.energy = Mathf.Min(8,energy+state.side1.nextRoundEnergyBonus);
            state.side0.nextRoundEnergyBonus=state.side1.nextRoundEnergyBonus=0;
            Log("Round energy refilled to " + energy + " for both players.");
        }

        private void StartClock(){
            if(!clock) return;
            float seconds = IsPuzzle || (IsTutorial && (TutorialLesson < 4 || !TutorialFreePlay)) ? 0 : turnSeconds;
            clock.BeginTurn(Mathf.Max(0, seconds));
            if(seconds <= 0) clock.Stop();
        }

        private void Recompute(){
            RestoreLucidOnScore(0,state.side0.observedScore); RestoreLucidOnScore(1,state.side1.observedScore);
            state.side0.observedScore=state.side0.score; state.side1.observedScore=state.side1.score;
            RecomputeEffects();
            for(int i=0;i<state.tiles.Length;i++){
                var tile=state.tiles[i];
                tile.total0 = (tile.side0?.currentInfluence ?? 0) + (tile.assist0?.currentInfluence ?? 0) + state.roundTileBonus0[i];
                tile.total1 = (tile.side1?.currentInfluence ?? 0) + (tile.assist1?.currentInfluence ?? 0) + state.roundTileBonus1[i];
            }
            if(!resolvingOuterContests) BeginQueuedEffect();
        }

        private void RemovePerformer(TileState tile, int actor){
            if (sfx) sfx.PlayDestroySound();
            var card = Performer(tile, actor);
            var assist=Assistant(tile,actor);
            SetPerformer(tile, actor, null);
            SetAssistant(tile,actor,null);
            int index=System.Array.IndexOf(state.tiles,tile);
            if(card != null) { placedTurns.Remove(card.instanceId); StageDeparture(card,index); }
            if(assist != null) { placedTurns.Remove(assist.instanceId); StageDeparture(assist,index); }
        }

        private void ResolveRound(){
            state.phase = RoundPhase.EndRound; state.inputAllowed = false;
            state.consecutivePasses = 0;
            if(clock) clock.Stop();
            Recompute();
            Log("Resolving round.");
            roundScore0 = roundScore1 = 0;
            resolvingOuterContests=true;
            try{
                for(int i = 0; i < state.tiles.Length; i++)
                    if(i % 5 != 2){ ResolveContest(i); Recompute(); }
            }finally{ resolvingOuterContests=false; }
            Recompute();
            ScoringRow = 0;
            RoundResolutionPending = true;
        }

        private void ResolveContest(int index){
            var tile = state.tiles[index];
            if((tile.side0==null && tile.total0<=0) || (tile.side1==null && tile.total1<=0)) return;
            int difference = tile.total0 - tile.total1;
            if(index%5!=2 && difference!=0){
                int winner=difference>0 ? 0 : 1;
                ResolveDuetWin(winner,index,Contested(index),new[] {Performer(tile,winner),Assistant(tile,winner)}.Where(c=>c!=null).ToArray());
            }
            if(difference != 0 && Contested(index)){
                int winner=difference>0 ? 0 : 1;
                foreach(var c in new[] {Performer(tile,winner),Assistant(tile,winner)})
                    if(HasEffect(c,CardEffect.ContestedWinToken)) QueueRinChoice(c,index,EffectChoice.TokenTile,0);
            }
            if(index%5!=2 && difference!=0){
                int actor=difference>0 ? 0 : 1;
                int extra=new[] {Performer(tile,actor),Assistant(tile,actor)}.Count(c=>c!=null && c.noScoreRound!=state.roundNumber && HasEffect(c,CardEffect.ContestedWinScore));
                state.Side(actor).score+=extra;
                if(actor==0) roundScore0+=extra; else roundScore1+=extra;
                if((actor==0 ? state.tileDraw0 : state.tileDraw1)[index]) DrawCards(actor,1);
            }
            if(difference > 0){ ContestDamage(index,0,tile.total1); DefeatContestSide(tile, 1); }
            else if(difference < 0){ ContestDamage(index,1,tile.total0); DefeatContestSide(tile, 0); }
            else { DefeatContestSide(tile, 0); DefeatContestSide(tile, 1); }
        }

        private void DefeatContestSide(TileState tile, int actor){
            var cards=new[] { Performer(tile, actor), Assistant(tile, actor) }.Where(c=>c!=null).ToArray();
            var survivors=cards.Where(c=>CardInfluence.BreakShield(c)).ToList();
            SetPerformer(tile, actor, survivors.Count > 0 ? survivors[0] : null);
            SetAssistant(tile, actor, survivors.Count > 1 ? survivors[1] : null);
            foreach(var card in cards.Where(c=>!survivors.Contains(c))){
                placedTurns.Remove(card.instanceId); StageDeparture(card,System.Array.IndexOf(state.tiles,tile)); if(sfx) sfx.PlayDestroySound();
            }
        }

        public EndRoundScoringAnimator scoringPresenter;
        public bool RoundResolutionPending { get; private set; }
        public int ScoringRow { get; private set; }
        private int roundScore0, roundScore1;
        private bool resolvingOuterContests;

        public bool ResolveNextScoringTile(){
            if(IsOnline && !OnlineAuthority) return false;
            if(!RoundResolutionPending || ScoringRow >= 5 || IsPaused || PhaseAnimationPlaying || state.pendingEffect!=null) return false;
            int index = ScoringRow * 5 + 2;
            bool contested=Contested(index);
            int winningActor=state.tiles[index].total0>state.tiles[index].total1 ? 0 : state.tiles[index].total1>state.tiles[index].total0 ? 1 : -1;
            var scoringCards=winningActor<0 ? new CardState[0] : new[] { Performer(state.tiles[index],winningActor), Assistant(state.tiles[index],winningActor) }.Where(c=>c!=null).ToArray();
            if(winningActor>=0) ResolveDuetWin(winningActor,index,contested,scoringCards);
            int bonus=scoringCards.Count(c=>c.noScoreRound!=state.roundNumber && Encore(c));
            if(contested) bonus+=scoringCards.Count(c=>c.noScoreRound!=state.roundNumber && HasEffect(c,CardEffect.ContestedWinScore));
            ResolveContest(index);
            Recompute();
            var tile = state.tiles[index];
            int score0=contested && winningActor!=0 ? 0 : ScoreTile(index,0)+(winningActor==0 ? bonus : 0);
            int score1=contested && winningActor!=1 ? 0 : ScoreTile(index,1)+(winningActor==1 ? bonus : 0);
            roundScore0 += score0; roundScore1 += score1;
            state.side0.score += score0; state.side1.score += score1;
            Recompute();
            if(winningActor>=0 && (winningActor==0 ? state.tileDraw0 : state.tileDraw1)[index]) DrawCards(winningActor,1);
            ScoringRow++;
            Publish();
            return true;
        }

        public void FinishRoundResolution(){
            if(IsOnline && !OnlineAuthority) return;
            if(!RoundResolutionPending || ScoringRow < 5 || IsPaused || PhaseAnimationPlaying || state.pendingEffect!=null) return;
            RoundResolutionPending = false;
            int roundWinner=roundScore0>roundScore1 ? 0 : roundScore1>roundScore0 ? 1 : -1;
            if(roundWinner>=0) state.Side(roundWinner).score=Mathf.Max(0,state.Side(roundWinner).score-2*FieldCards(1-roundWinner).Count(c=>HasEffect(c,CardEffect.ReduceOpponentScore)));
            EndRoundEffects();
            state.roundSummary = "Third column: Player +" + (state.localPlayerId == 0 ? roundScore0 : roundScore1) +
                " / Opponent +" + (state.localPlayerId == 0 ? roundScore1 : roundScore0) + ".";
            if (deckExhausted) state.roundSummary += " A deck is empty; this is the final round.";
            roundWait = Mathf.Max(0, endRoundDisplaySeconds);
            if(IsPuzzle) { puzzleRounds++; CheckPuzzle(true, false); }
            Publish();
        }
        private void Update(){
            if(IsOnline && !OnlineAuthority) return;
            if(state?.pendingEffect!=null){
                var p=state.pendingEffect;
                // A configured AI owns its choices and delay. Keep the original
                // fallback for puzzles/boards that deliberately have no active AI.
                if(!IsOnline && p.actor!=state.localPlayerId && !IsPaused && !DrawAnimationPlaying && !PhaseAnimationPlaying &&
                    (IsPuzzle || !enemyAI || !enemyAI.isActiveAndEnabled || enemyAI.game!=this)){
                    if(p.cards.Count>0) ResolveChoice(p.actor,new BoardAction(BoardActionKind.ChooseEffect,p.cards[0].instanceId,-1,-1,-1,-1));
                    else if(p.tiles.Count>0) ResolveChoice(p.actor,new BoardAction(BoardActionKind.ChooseEffect,null,-1,-1,p.tiles[0]%5,p.tiles[0]/5));
                    else if(p.optional) ResolveChoice(p.actor,new BoardAction(BoardActionKind.FinishEffect,null,-1,-1,-1,-1));
                }
                return;
            }
            UpdatePuzzleEnemy();
            if(state == null || state.phase != RoundPhase.EndRound || IsPaused || PhaseAnimationPlaying) return;
            if(RoundResolutionPending){
                if(scoringPresenter && scoringPresenter.IsAvailable) return;
                while(ScoringRow < 5) if(!ResolveNextScoringTile()) return;
                FinishRoundResolution();
                return;
            }
            roundWait -= Time.unscaledDeltaTime;
            if(roundWait <= 0) CompleteRound();
        }

        public bool CompleteRound(){
            if(IsOnline && !OnlineAuthority) return false;
            if(tutorialDirector && tutorialDirector.HoldRound) return false;
            if(!isActiveAndEnabled || state == null || state.phase != RoundPhase.EndRound || IsPaused || PhaseAnimationPlaying || RoundResolutionPending || state.pendingEffect!=null) return false;
            if(!IsPuzzle && (deckExhausted || state.side0.score >= state.winScore || state.side1.score >= state.winScore)){
                state.phase = RoundPhase.Finished;
                state.winnerId = state.side0.score == state.side1.score ? -1 : state.side0.score > state.side1.score ? 0 : 1;
                state.roundSummary += state.winnerId < 0 ? " Draw!" : state.winnerId == state.localPlayerId ? " Player wins!" : " Opponent wins!";
                SaveStoryReward();
                Log("Match finished (" + (deckExhausted ? "empty deck" : "winning score reached") + "). " +
                    (state.winnerId < 0 ? "Draw." : "Player " + state.winnerId + " wins.") + " Scores: " + state.side0.score + " / " + state.side1.score + ".");
            }else{
                state.roundNumber++; state.roundStarterId = 1 - state.roundStarterId;
                StartRinRound();
                state.phase = RoundPhase.Preparation; state.inputAllowed = true;
                Log("New round begins. Player " + state.roundStarterId + " goes first.");
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

        private void PlayApproachingEndVoiceline()
        {
            if (state == null || approachingEndVoicelinePlayed) return;

            int playerScore = state.Side(state.localPlayerId).score;
            int enemyScore = state.Side(1 - state.localPlayerId).score;
            if ((state.winScore - playerScore <= 5 || deckExhausted) && playerScore > enemyScore) PlayLocalVoiceline(VoicelineEvent.Excited);
            else if ((state.winScore - enemyScore <= 5 || deckExhausted) && playerScore < enemyScore) PlayLocalVoiceline(VoicelineEvent.Concerned);
            else return;
            approachingEndVoicelinePlayed = true;

        }

    }
}
