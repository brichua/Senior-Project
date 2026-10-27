using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VocaloidTCG.BoardUI;

namespace VocaloidTCG
{
    /// <summary>Owns lesson gates; the bridge still validates and executes every game action.</summary>
    public sealed class TutorialDirector : MonoBehaviour
    {
        public GameplayBoardBridge game;
        private enum Kind { Talk, Select, Play, Move, Pass, Wait, Redraw, Finish, Release }
        private sealed class Step
        {
            public Kind kind;
            public Func<string> text;
            public string card;
            public int x = -1, y = -1, expression;
            public string focus = "board";
            public Func<bool> until;
        }
        private readonly List<Step> steps = new List<Step>();
        private int cursor;
        private bool entered, done, freePlay;
        private string expectedId;
        private float enemyWait;
        private TutorialMessageView message;
        private BoardUIController board;
        private Step displayedStep;
        private string displayedCardId;
        private Step Current => cursor < steps.Count ? steps[cursor] : null;
        private BoardSnapshot State => game.Snapshot;
        public bool ReadyForRedraw => entered && Current != null && Current.kind == Kind.Redraw;
        public bool ReadyToFinish => Current != null && Current.kind == Kind.Finish;
        public bool ExplainingOpeningClasses => Current != null && Current.focus == "openingClasses";
        public bool FreePlay => freePlay;
        public bool HoldRound => !freePlay && Current != null && Current.focus != "nextRound";
        public string FocusCardId => Current != null && (Current.kind == Kind.Play || Current.kind == Kind.Select) ? expectedId : null;

        public static string LessonTitle(int index){
            string[] titles = { "1. Take Center Stage", "2. Win the Crowd", "3. Make Your Move", "4. The Opening Act", "5. Your First Concert" };
            return index >= 0 && index < titles.Length ? titles[index] : "Tutorial";
        }
        public static IEnumerable<string> RequiredCards(int index){
            switch(index){
                case 0: return new[] { "Romeo and Cinderella" };
                case 1: return new[] { "Romeo and Cinderella", "Glow", "From Y to Y", "Cool Me Down" };
                case 2: return new[] { "Deep Sea Girl", "Darling Dance", "Romeo and Cinderella", "Fire Flower", "Butterfly on Your Right Shoulder" };
                case 3: return new[] { "Rabbit Hole", "Deep Sea Girl", "Glow", "From Y to Y", "Romeo and Cinderella", "Sadistic Love", "Meltdown", "Tengaku" };
                default: return Array.Empty<string>();
            }
        }

        public void ResetLesson(){
            cursor = 0; entered = done = freePlay = false; expectedId = null; enemyWait = 0;
            steps.Clear(); BuildLesson();
        }
        private void Talk(string text, string focus = "board", int expression = 0, int x = -1, int y = -1){
            steps.Add(new Step { kind = Kind.Talk, text = () => text, focus = focus, expression = expression, x = x, y = y });
        }
        private void Action(Kind kind, string text, string card = null, int x = -1, int y = -1, string focus = "hand"){
            steps.Add(new Step { kind = kind, text = () => text, card = card, x = x, y = y, focus = focus });
        }
        private void Wait(Func<bool> until, string focus = "board"){
            // Internal synchronization keeps the previous instruction visible; it is not a dialogue line.
            steps.Add(new Step { kind = Kind.Wait, until = until, focus = focus });
        }
        private void BackToPlayer(){ Wait(() => State.activePlayerId == State.localPlayerId && State.phase == RoundPhase.Preparation); }
        private void EndPreparation(){
            Action(Kind.Pass, "We've made our moves. Click End Turn.", focus: "endTurn");
            BackToPlayer();
            Action(Kind.Pass, "Our opponent passed. Pass too: two empty turns in a row advance the phase.", focus: "endTurn");
            Wait(() => State.phase == RoundPhase.Performance && State.activePlayerId == State.localPlayerId);
        }
        private void EndPerformance(){
            Action(Kind.Pass, "Pass the performance turn to let the round resolve.", focus: "endTurn");
            Wait(() => State.phase == RoundPhase.EndRound && !game.RoundResolutionPending);
        }
        private void Finish(){
            steps.Add(new Step { kind = Kind.Finish, text = () => "Nice work! That's this lesson complete. Click to return to the story map and unlock the next stage.", expression = 1 });
        }
        private void BuildLesson(){
            switch(game.TutorialLesson){
                case 0:
                    Talk("Welcome to Virtual Encore! I'm Miku. This 5 by 5 grid is our stage. The middle column is Center Stage.");
                    Talk("Performers stay on tiles. Their influence helps you control the stage. Stage Effects change the action, then leave your hand.", "hand");
                    Action(Kind.Select, "Click Romeo and Cinderella in your hand. Its influence icon tells you it's a performer.", "Romeo and Cinderella");
                    Talk("The card info box shows the name, energy cost, influence, and effects. You can inspect cards and performers whenever you need a reminder.", "info");
                    Talk("Here's your energy. Each round it refills to the round number, up to 8. You begin a normal game with 1 energy.", "energy");
                    Action(Kind.Play, "Romeo and Cinderella costs 1 energy. Drag it onto the highlighted middle tile in Center Stage.", "Romeo and Cinderella", 2, 2, "board");
                    Talk("Perfect! Your class color marks your side of the tile. The popup shows how much influence you control here.", "popup", 1, 2, 2);
                    EndPreparation();
                    Talk("Welcome to Performance! A round has Preparation, then Performance. You can play performers and move during Preparation; only Cheer Effects can be played during Performance.");
                    EndPerformance();
                    steps.Add(new Step { kind = Kind.Talk, focus = "score", expression = 1, text = () => "The round is over! You gained " + State.Side(State.localPlayerId).score + " fan score. Each controlled Center Stage tile scores its influence. Reach 50 before your opponent to win." });
                    Finish(); break;
                case 1:
                    Talk("Our opponent already has 3 influence in Center Stage. Let's contest that tile.");
                    Action(Kind.Play, "Place Romeo and Cinderella on the opponent's middle tile.", "Romeo and Cinderella", 2, 2, "board");
                    Talk("Both sides can occupy the same tile. Normally, each side gets one performer. The winner scores the difference in influence; right now our 2 loses to their 3.", expression: 2);
                    Action(Kind.Play, "Stage Effects have no influence icon and don't occupy tiles. Play Glow on Romeo and Cinderella to give it +2 influence this round.", "Glow", 2, 2, "board");
                    Talk("Now we're ahead, 4 to 3! Keep that lead until scoring.", expression: 1);
                    EndPreparation();
                    Action(Kind.Select, "In Performance, only Stage Effects with Cheer Effect can be played. Click From Y to Y to read one.", "From Y to Y");
                    Talk("Hover a keyword in the card info box for its tooltip. From Y to Y protects an ally from opposing Stage Effects for this turn.", "info");
                    Action(Kind.Play, "Play From Y to Y on Romeo and Cinderella.", "From Y to Y", 2, 2, "board");
                    Action(Kind.Pass, "That's our Cheer Effect played. End your turn.", focus: "endTurn");
                    Wait(() => State.phase == RoundPhase.Performance && State.activePlayerId == State.localPlayerId);
                    EndPerformance();
                    Talk("We won the tile and scored the difference! The losing performer leaves the stage; the winner takes influence damage equal to the opposing total. Glow's temporary bonus expires after scoring.", "score", 1);
                    Finish(); break;
                case 2:
                    Action(Kind.Select, "Deep Sea Girl is in trouble. Click Butterfly on Your Right Shoulder, the enemy support performer just right of Center Stage.", "Butterfly on Your Right Shoulder", 3, 2, "board");
                    Talk("Butterfly weakens opposing performers on the tile directly in front of it by 1. Our Deep Sea Girl now loses the contested tile.", "enemyInfo", 2);
                    Action(Kind.Play, "You normally place performers in your two home columns or Center Stage. An ally also opens adjacent tiles, including diagonals. Place Darling Dance on Butterfly's tile to challenge that support.", "Darling Dance", 3, 2, "board");
                    Action(Kind.Move, "Once per turn, move one performer one tile horizontally. It must have been played on an earlier turn. Drag Deep Sea Girl one tile left to safety.", "Deep Sea Girl", 1, 2, "board");
                    EndPreparation(); EndPerformance();
                    Talk("Darling Dance defeated the support outside Center Stage. Our opponent scored this round, but Deep Sea Girl is safe and the weakening effect is gone.", "score");
                    Wait(() => State.roundNumber >= 2 && State.phase == RoundPhase.Preparation && State.activePlayerId == State.localPlayerId, "nextRound");
                    Talk("It's a new round, so you draw a card when your first turn begins. You draw once each round, except the first round when you already have your opening hand.", "deck");
                    Action(Kind.Move, "Now move Deep Sea Girl one tile right, back to the middle tile.", "Deep Sea Girl", 2, 2, "board");
                    Action(Kind.Play, "Add Romeo and Cinderella to Deep Sea Girl's tile. Stage Share allows one supporting performer alongside your original performer. Their influence adds together.", "Romeo and Cinderella", 2, 2, "board");
                    Talk("Deep Sea Girl and Romeo and Cinderella give us 5 influence against Fire Flower's 3. We're ahead by 2!", "popup", 1, 2, 2);
                    EndPreparation(); EndPerformance();
                    Talk("Our combined influence won Center Stage! Good positioning and Stage Share turned the round around.", "score", 1);
                    Finish(); break;
                case 3:
                    Talk("These icons show the classes in your deck. A deck can have up to 2 classes. We're using Miku's class for this tutorial.", "openingClasses");
                    Wait(() => !board || !board.openingAnimator || board.openingAnimator.TutorialRedrawReady);
                    Talk("Let's learn the opening hand. Your VIP is guaranteed, alongside 3 starting cards. You may replace any of those 3 cards once before play begins.", "opening");
                    Action(Kind.Redraw, "Rabbit Hole is your guaranteed VIP. Keep Deep Sea Girl and Glow. Select From Y to Y for redraw, then confirm your hand.", "From Y to Y", focus: "opening");
                    Wait(() => State.phase == RoundPhase.Preparation && State.activePlayerId == State.localPlayerId);
                    Talk("Your VIP is unique to your class, with only one copy per deck. It stays first in your hand. You draw a card when your first turn in a new round begins, except in round 1 when you already have your opening hand.", "hand");
                    Talk("This is your remaining deck count. When either player runs out of cards to draw, the game ends after the current round. Whoever has the highest fan score wins; equal scores are a draw.", "deck");
                    Talk("Normally we'd begin with 1 energy. For this demonstration, both sides have 4.", "energy");
                    Action(Kind.Play, "Place Rabbit Hole on the middle tile. Its Stage Share and Harmonize keywords reward supporting performers.", "Rabbit Hole", 2, 2, "board");
                    Action(Kind.Pass, "End your turn and watch Rin's response.", focus: "endTurn");
                    BackToPlayer();
                    Talk("Rin played Sadistic Love, a Venue Effect. Hover the venue icon to read it: whenever her performer leaves the stage or she discards a performer, she gains 1 fan score. The venue stays for the whole game.", "venue", 2);
                    Action(Kind.Pass, "Rin also contested Rabbit Hole with Meltdown. Pass to move into Performance.", focus: "endTurn");
                    Wait(() => State.phase == RoundPhase.Performance && State.activePlayerId == State.localPlayerId);
                    EndPerformance();
                    Talk("Rabbit Hole won the tile, but Rin still gained 1 fan score when Meltdown left the stage. Venue Effects can reward you even when you lose a contest!", "score", 1);
                    Finish(); break;
                default:
                    Talk("You've learned the basics! This final audition is a full game with a gentle, single-class opponent. Win to complete the tutorial.");
                    Talk("Inspect cards and performers whenever you're curious, and hover keywords for reminders. You'll meet new effects as you explore the other stories.");
                    steps.Add(new Step { kind = Kind.Release, text = () => "The stage is yours. Good luck!", expression = 1 });
                    break;
            }
        }

        private CardState FindCard(string name){
            if(State == null) return null;
            var hand = State.Side(State.localPlayerId).hand;
            return hand.Concat(State.tiles.SelectMany(t => new[] { t.side0, t.side1, t.assist0, t.assist1 }))
                .FirstOrDefault(c => c != null && c.data && c.data.cardName == name);
        }
        private void Enter(){
            entered = true; done = false;
            expectedId = Current.card != null ? FindCard(Current.card)?.instanceId : null;
            if(Current.kind == Kind.Wait) return;
            displayedStep = Current; displayedCardId = expectedId;
            message.Show(Current.text(), Current.kind == Kind.Talk || Current.kind == Kind.Finish || Current.kind == Kind.Release,
                Current.expression, game.TutorialStory, game.deckCatalog.Class(CharacterClass.HatsuneMiku).avatar);
            if(board) board.Refresh();
        }
        private void Next(){ cursor++; entered = done = false; }
        private void Start(){
            board = FindObjectsByType<BoardUIController>(FindObjectsSortMode.None).FirstOrDefault(b => b.game == game);
            if(!board || board.game != game){ Debug.LogError("Tutorial requires its BoardUIController.", this); enabled = false; return; }
            try { message = TutorialMessageView.Create(board, game.TutorialStory.tutorialMessagePrefab, Continue); }
            catch(Exception ex){ Debug.LogException(ex, this); enabled = false; return; }
            if(steps.Count == 0) ResetLesson();
        }
        private void Update(){
            if(!game || !message || State == null || game.IsPaused || freePlay) return;
            if(Current == null) return;
            // Keep lessons legible while animations own the stage.
            if(game.PhaseAnimationPlaying || game.DrawAnimationPlaying) return;
            if(!entered) Enter();
            if(Current.kind == Kind.Redraw && game.OpeningRedrawSubmitted) done = true;
            if(Current.kind == Kind.Wait){
                if(Current.until()) done = true;
                else UpdateOpponent();
            }
            if(done && State.pendingEffect == null) Next();
        }
        private void LateUpdate(){
            if(!message || !board || freePlay) return;
            message.SetPaused(game.IsPaused);
            if(displayedStep == null || game.IsPaused) return;
            message.SetFocus(board, displayedStep.focus, displayedCardId, displayedStep.x, displayedStep.y);
        }
        private void OnDestroy(){ if(message) Destroy(message.gameObject); }
        private void Continue(){
            if(!game || game.IsPaused || !entered || Current == null) return;
            if(!message.Reveal()) return;
            if(Current.kind == Kind.Finish){
                if(!game.FinishTutorialLesson(out var error) && !string.IsNullOrEmpty(error)) message.ShowError(error);
            }else if(Current.kind == Kind.Release){
                freePlay = true; message.gameObject.SetActive(false); game.ReleaseTutorialIntro();
            }else if(Current.kind == Kind.Talk) Next();
        }
        public bool Allows(int actor, BoardAction action, out string reason){
            reason = "Follow Miku's current instruction.";
            if(freePlay) return true;
            if(Current == null || !entered || game.IsPaused) return false;
            if(actor != State.localPlayerId) return Current.kind == Kind.Wait;
            if(action.kind == BoardActionKind.ChooseEffect){
                // A required play may have a separate target selection; keep it on the taught tile.
                if(State.pendingEffect == null) return false;
                if(Current.x < 0) return true;
                var target = State.tiles[Current.y * 5 + Current.x];
                return action.toColumn == Current.x && action.toRow == Current.y ||
                    new[] { target.side0, target.side1, target.assist0, target.assist1 }.Any(c => c != null && c.instanceId == action.cardInstanceId);
            }
            if(done) return false;
            var kind = Current.kind == Kind.Play ? BoardActionKind.PlayCard : BoardActionKind.MovePerformer;
            return (Current.kind == Kind.Play || Current.kind == Kind.Move) && action.kind == kind && action.cardInstanceId == expectedId &&
                action.toColumn == Current.x && action.toRow == Current.y;
        }
        public bool AllowsPass(int actor){
            return freePlay || Current != null && entered && !done &&
                (actor == State.localPlayerId ? Current.kind == Kind.Pass : Current.kind == Kind.Wait);
        }
        public void ActionAccepted(int actor, BoardAction action){
            if(actor == State.localPlayerId && entered && Current != null &&
                (Current.kind == Kind.Play || Current.kind == Kind.Move) && action.cardInstanceId == expectedId) done = true;
        }
        public void PassAccepted(int actor){ if(actor == State.localPlayerId && Current != null && Current.kind == Kind.Pass) done = true; }
        public void CardSelected(CardState card){
            if(Current != null && entered && Current.kind == Kind.Select && card.instanceId == expectedId && !game.IsPaused) done = true;
        }
        private void UpdateOpponent(){
            if(game.TutorialLesson >= 4 || State.activePlayerId == State.localPlayerId || !State.inputAllowed) return;
            enemyWait -= Time.unscaledDeltaTime;
            if(enemyWait > 0) return;
            enemyWait = 0.65f;
            int enemy = 1 - State.localPlayerId;
            if(game.TutorialLesson == 3 && State.phase == RoundPhase.Preparation && State.roundNumber == 1){
                var card = State.Side(enemy).hand.FirstOrDefault(c => c.data.cardName == "Sadistic Love") ??
                    State.Side(enemy).hand.FirstOrDefault(c => c.data.cardName == "Meltdown");
                if(card != null){
                    game.TrySubmitFor(enemy, new BoardAction(BoardActionKind.PlayCard, card.instanceId, -1, -1, 2, 2));
                    return;
                }
            }
            game.TryPassFor(enemy);
        }
    }
}

