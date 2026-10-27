#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using VocaloidTCG.BoardUI;

namespace VocaloidTCG
{
    public static class TutorialChecks
    {
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private static object Get(object obj, string field) => obj.GetType().GetField(field, Hidden).GetValue(obj);
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Hidden).SetValue(obj, value);
        private static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Hidden).Invoke(obj, args);
        private static void Check(bool condition, string text){ if(!condition) throw new Exception(text); }

        [MenuItem("Tools/QA/Check Tutorial Lessons")]
        public static void Run(){
            var story = AssetDatabase.LoadAssetAtPath<StoryData>("Assets/Story/Tutorial.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<StoryCatalog>("Assets/Story/StoryCatalog.asset").deckCatalog;
            Check(story && story.HasPlayableContent(catalog), "Tutorial content must be playable without story dialogue/rewards.");
            Check(story.tutorialMessagePrefab, "Tutorial message prefab must be linked for builds.");
            CheckMessages(story);
            StorySession.Clear();
            for(int lesson = 0; lesson < 5; lesson++) CheckLesson(story, catalog, lesson);
            Debug.Log("Tutorial checks passed: persistent prefab messages, reveal/continue clicks, all lesson paths, gates, scoring, redraw, and exam release.");
        }
        public static void RunBatch(){
            try { Run(); EditorApplication.Exit(0); }
            catch(Exception ex){ Debug.LogException(ex); EditorApplication.Exit(1); }
        }
        private static void CheckMessages(StoryData story){
            var boardRoot = new GameObject("Tutorial message check board");
            TutorialMessageView view = null;
            try{
                var board = boardRoot.AddComponent<BoardUIController>();
                view = TutorialMessageView.Create(board, story.tutorialMessagePrefab, null);
                var panel = (RectTransform)Get(view, "panel");
                Check(!view.GetComponent<Canvas>().enabled && !panel.gameObject.activeSelf, "Prefab placeholders stay hidden before the first message.");
                view.SetPaused(false);
                Check(!view.GetComponent<Canvas>().enabled, "Unpausing cannot expose an uninitialized message.");
                var label = (TMP_Text)Get(view, "body");
                Check(label.name == "Text (TMP)", "Dialogue binds to the message label, not the authored continue hint.");
                Check(panel.GetComponentsInChildren<TMP_Text>(true).Length == story.tutorialMessagePrefab.GetComponentsInChildren<TMP_Text>(true).Length,
                    "No generated continue/finish hint is added to the prefab.");
                Check(panel.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget), "Message graphics do not block board actions or screen clicks.");
                var art = (Image)Get(view, "portrait");
                var prefabArt = story.tutorialMessagePrefab.GetComponentsInChildren<Image>(true).Single(i => i.name == "art");
                Check(panel.rect.size == story.tutorialMessagePrefab.GetComponent<RectTransform>().rect.size, "Preserve authored prefab dimensions.");
                Check(art.name == "art" && art.transform.IsChildOf(panel) && art.rectTransform.anchoredPosition == prefabArt.rectTransform.anchoredPosition,
                    "Expressions use the prefab's existing art image without replacing its layout.");
                Check(view.GetComponent<Canvas>().sortingOrder > 30000 && !view.transform.parent, "Opening fades and parent panels cannot hide tutorial messages.");
                view.Show("A persistent tutorial line that needs two clicks.", true, 1, story, null);
                Check(view.GetComponent<Canvas>().enabled && panel.gameObject.activeSelf, "The initialized first message becomes visible.");
                Check(art.sprite == story.tutorialExpressions[1], "Expression changes the actual prefab art sprite.");
                Check(view.IsTyping && !view.Reveal() && !view.IsTyping, "First click completes typing and is consumed.");
                Check(view.Reveal(), "The following click may advance a completed line.");
                string text = label.text;
                Call(view, "TickTyping", 180f);
                Check(view.gameObject.activeSelf && panel.gameObject.activeSelf && label.text == text && label.alpha == 1,
                    "Completed dialogue never fades or clears on a timer.");
                view.Show("Finish this text naturally.", false, 2, story, null);
                var continueLabel = (TMP_Text)Get(view, "continueLabel");
                Check(continueLabel && continueLabel.text == "complete action to continue", "The authored hint identifies required actions.");
                Call(view, "TickTyping", 180f);
                Check(!view.IsTyping && label.maxVisibleCharacters == int.MaxValue && art.sprite == story.tutorialExpressions[2],
                    "Natural typing completion leaves the full instruction and expression visible.");
                view.Show("Click-through dialogue.", true, 0, story, null);
                Check(continueLabel.text == (string)Get(view, "continueText"), "Dialogue restores the authored continue hint.");
                CheckFocusTargets(view, boardRoot, board);
            }finally{
                if(view) UnityEngine.Object.DestroyImmediate(view.gameObject);
                UnityEngine.Object.DestroyImmediate(boardRoot);
            }
        }
        private static Image FocusImage(Transform parent, string name){
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false); return obj.GetComponent<Image>();
        }
        private static void CheckFocusTargets(TutorialMessageView view, GameObject root, BoardUIController board){
            var game = root.AddComponent<GameplayBoardBridge>(); game.startAutomatically = false;
            Set(game, "state", new BoardSnapshot { localPlayerId = 0 }); board.game = game;
            var hud = FocusImage(root.transform, "Player HUD").gameObject.AddComponent<SideHUD>(); board.playerHUD = hud;
            hud.infoBackground = FocusImage(hud.transform, "Visible info panel");
            hud.deckCount = new GameObject("Deck count", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            hud.deckCount.transform.SetParent(hud.transform, false);
            board.playerInfo = FocusImage(hud.transform, "info card player").gameObject.AddComponent<CardInfoView>();
            Check(Call(view, "FocusTarget", board, "info", null, -1, -1) == hud.infoBackground.rectTransform,
                "Info focus follows the visible panel rather than its centered container.");
            Check(Call(view, "FocusTarget", board, "deck", null, -1, -1) == hud.deckCount.rectTransform, "Deck lesson highlights the remaining card count.");
            var tiles = (List<TileView>)Get(board, "tiles");
            for(int i = 0; i < 25; i++){
                var image = FocusImage(root.transform, "Focus tile " + i);
                var tile = image.gameObject.AddComponent<TileView>(); tile.tile = image;
                tile.soloPopup = FocusImage(tile.transform, "Influence popup"); tiles.Add(tile);
            }
            var hand = FocusImage(root.transform, "Romeo in hand").gameObject.AddComponent<CardPointer>();
            ((Dictionary<string, CardPointer>)Get(board, "handViews")).Add("romeo", hand);
            Check(Call(view, "FocusTarget", board, "board", "romeo", 2, 2) == tiles[12].tile.rectTransform,
                "Placement focuses the destination even while Romeo remains in hand.");
            Check(Call(view, "FocusTarget", board, "popup", null, 2, 2) == tiles[12].soloPopup.rectTransform,
                "The post-placement explanation focuses the tile's influence popup.");
        }
        private static void CheckLesson(StoryData story, DeckCatalog catalog, int lesson){
            var root = new GameObject("Tutorial check");
            var viewRoot = new GameObject("Tutorial check view", typeof(RectTransform));
            try{
                var game = root.AddComponent<GameplayBoardBridge>();
                game.startAutomatically = false; game.enableDebugLogs = false;
                game.clock = root.AddComponent<LocalTurnClock>();
                game.deckCatalog = catalog;
                game.enemyDeck = story.auditions[lesson].easyDeck;
                Set(game, "story", story); Set(game, "storyOpponent", story.auditions[lesson].opponent);
                var director = root.AddComponent<TutorialDirector>(); director.game = game;
                Set(game, "tutorialDirector", director);
                var view = viewRoot.AddComponent<TutorialMessageView>();
                var text = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>(); text.transform.SetParent(viewRoot.transform);
                var hint = new GameObject("Hint", typeof(RectTransform)).AddComponent<TextMeshProUGUI>(); hint.transform.SetParent(viewRoot.transform);
                var portrait = new GameObject("Portrait", typeof(RectTransform)).AddComponent<Image>(); portrait.transform.SetParent(viewRoot.transform);
                Set(view, "body", text); Set(view, "portrait", portrait);
                Set(director, "message", view);
                game.StartMatch();
                Check(string.IsNullOrEmpty(game.MatchSetupError), "Lesson setup: " + game.MatchSetupError);
                var state = game.Snapshot;
                Check(state != null && state.localPlayerId == 0 && state.winScore == 50, "Correct tutorial player and win score.");
                Check((state.phase == RoundPhase.Mulligan) == (lesson >= 3), "Only lessons 4/5 use the opening sequence.");
                if(lesson == 3){
                    Check(state.side0.hand[0].data.cardName == "Rabbit Hole" && state.side0.hand[0].data.vip, "Tutorial 4 starts with its VIP first in hand.");
                    Check(game.OpeningCards(0).Count == 3 && game.OpeningCards(0).All(c => !c.data.vip), "Only the three regular opening cards may be redrawn.");
                    Check(game.OpeningCards(1).Count == 3 && state.side1.hiddenHandCount == 3,
                        "Rin displays three face-down cards during the tutorial redraw.");
                    Check(game.ChooseOpponentRedraw().Count == 0 && game.OpeningCards(1).Any(c => c.data.cardName == "Sadistic Love") &&
                        game.OpeningCards(1).Any(c => c.data.cardName == "Meltdown"), "Rin keeps her required scripted opening cards.");
                }
                Check(!game.TryPassFor(0), "Cannot skip introductory dialogue by passing.");
                if(lesson < 4) Check(!(bool)Get(game.clock, "running"), "Scripted lessons have no running turn clock.");
                bool finished = false;
                for(int iteration = 0; iteration < 300; iteration++){
                    Set(director, "enemyWait", 0f);
                    Call(director, "Update");
                    var step = director.GetType().GetProperty("Current", Hidden).GetValue(director);
                    if(step == null) break;
                    if(!(bool)Get(director, "entered")) continue;
                    var stepType = step.GetType();
                    string kind = stepType.GetField("kind").GetValue(step).ToString();
                    string name = (string)stepType.GetField("card").GetValue(step);
                    if(kind == "Finish"){ finished = true; break; }
                    if(kind == "Talk"){
                        int before = (int)Get(director, "cursor");
                        Call(director, "Continue");
                        Check((int)Get(director, "cursor") == before && !view.IsTyping, "First click reveals dialogue without advancing.");
                        Call(director, "Continue");
                        Check((int)Get(director, "cursor") == before + 1, "Second click advances dialogue exactly once.");
                        continue;
                    }
                    if(kind == "Release"){
                        view.Reveal();
                        Call(director, "Continue");
                        Check(director.FreePlay, "Exam releases to normal gameplay."); finished = true; break;
                    }
                    if(kind != "Wait"){
                        int before = (int)Get(director, "cursor");
                        Call(director, "Continue"); Call(director, "Continue");
                        Check((int)Get(director, "cursor") == before, "Clicking an action instruction cannot skip its action.");
                    }else Check(text.text != "Watch the stage...", "Internal waits do not flash temporary dialogue.");
                    if(kind == "Select"){
                        var card = (CardState)Call(director, "FindCard", name);
                        Check(card != null, "Selectable card exists: " + name); director.CardSelected(card); continue;
                    }
                    if(kind == "Play" || kind == "Move"){
                        string id = (string)Get(director, "expectedId");
                        int x = (int)stepType.GetField("x").GetValue(step), y = (int)stepType.GetField("y").GetValue(step);
                        int from = Array.FindIndex(state.tiles, t => t.side0 != null && t.side0.instanceId == id);
                        var action = new BoardAction(kind == "Play" ? BoardActionKind.PlayCard : BoardActionKind.MovePerformer, id, from < 0 ? -1 : from % 5, from < 0 ? -1 : from / 5, x, y);
                        var wrong = action; wrong.toRow = (y + 1) % 5;
                        Check(!game.CanSubmit(wrong, out _), "Wrong destination is gated.");
                        Check(!game.TryPassFor(0), "Cannot pass instead of required action.");
                        Check(game.CanSubmit(action, out var reason), "Required " + name + " must be legal: " + reason);
                        Check(game.TrySubmit(action), "Required play/move succeeds: " + name);
                        if(lesson == 2 && name == "Romeo and Cinderella")
                            Check(state.tiles[12].total0 == 5 && state.tiles[12].total1 == 3, "Stage Share gives a real 5-to-3 lead without Len's Soloist bonus.");
                        continue;
                    }
                    if(kind == "Pass"){ Check(game.TryPassFor(0), "Required pass succeeds."); continue; }
                    if(kind == "Redraw"){
                        var opening = game.OpeningCards(0);
                        var desired = opening.Single(c => c.data.cardName == "From Y to Y");
                        Check(!game.SubmitOpeningRedraw(Array.Empty<string>(), Array.Empty<string>()), "Cannot keep the instructed redraw card.");
                        Check(game.SubmitOpeningRedraw(new[] { desired.instanceId }, Array.Empty<string>()), "Required redraw succeeds.");
                        Check(state.side0.hand.Count(c => c.data.vip) == 1 && state.side0.hand.Any(c => c.data.cardName == "Rabbit Hole"), "Exactly one guaranteed Rabbit Hole VIP is retained.");
                        Check(state.side0.hand[0].data.vip, "Redraw keeps the VIP first in hand.");
                        Check(game.CompleteOpening(), "Opening completes."); continue;
                    }
                    if(kind == "Wait" && state.phase == RoundPhase.EndRound){
                        if(game.RoundResolutionPending){
                            while(game.ScoringRow < 5) Check(game.ResolveNextScoringTile(), "Scoring progresses.");
                            game.FinishRoundResolution();
                        }else if(!director.HoldRound) Check(game.CompleteRound(), "Next teaching round starts.");
                    }
                }
                Check(finished, "Lesson " + (lesson + 1) + " reached its end without a deadlock.");
                if(lesson == 0 || lesson == 1) Check(state.side0.score > 0, "Center Stage demonstration actually scores.");
                if(lesson == 2) Check(state.roundNumber == 2 && state.side0.score == 2 && state.tiles[12].side1 == null && state.tiles[12].total0 > 0,
                    "Movement and Stage Share defeat Fire Flower and score 2 in round 2.");
                if(lesson == 3) Check(state.side0.score > 0 && state.side1.score == 1, "Venue demonstration rewards Rin exactly once.");
            }finally{
                UnityEngine.Object.DestroyImmediate(viewRoot); UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
#endif

