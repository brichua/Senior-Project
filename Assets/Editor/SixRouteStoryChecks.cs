#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VocaloidTCG;
using VocaloidTCG.BoardUI;

// Development runner: every route/order has a separate save under Logs. Results are injected
// to test transitions and persistence; this is not a battle-balance or player-build test.
[InitializeOnLoad]
public static class SixRouteStoryChecks
{
    [Serializable] private sealed class Manifest { public string sourceSha256; public ReviewedRoute[] routes; }
    [Serializable] private sealed class ReviewedRoute {
        public string assetName; public int characterClass;
        public StoryDialogue opening, ending; public StoryAudition[] auditions;
    }
    [Serializable] private sealed class LegacySave {
        public int version = 1; public List<DeckRecord> decks = new List<DeckRecord>();
        public List<string> owned = new List<string>(); public string selectedId;
        public List<string> completedPuzzles = new List<string>();
        public List<DeckLibrary.StoryClearRecord> storyClears = new List<DeckLibrary.StoryClearRecord>();
        public List<string> viewedStoryDialogues = new List<string>();
    }

    private const string Running = "SixRouteStoryChecks.Running", SavePath = "SixRouteStoryChecks.SavePath", Root = "SixRouteStoryChecks.Root", Previewing = "SixRouteStoryChecks.Previewing";
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly string[] IDs = { "story-hatsunemiku", "story-kagaminelen", "story-kagaminerin", "story-kaito", "story-meiko", "story-megurineluka" };
    private static IEnumerator steps;
    private static int assertions, renderedBoxes, battles;
    private static readonly List<string> notes = new List<string>();

    static SixRouteStoryChecks(){
        if(SessionState.GetBool(Running, false)) EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state => {
            if(state == PlayModeStateChange.EnteredEditMode) SessionState.SetBool(Previewing, false);
        };
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void UseTestSave(){
        if(!SessionState.GetBool(Running, false) && !SessionState.GetBool(Previewing, false)) return;
        SetLibrary(NewLibrary());
        Application.runInBackground = true;
    }

    private static Manifest ReadManifest() => JsonUtility.FromJson<Manifest>(File.ReadAllText("Tools/Story/ReviewedRoutes.json"));
    private static StoryCatalog Catalog() => AssetDatabase.LoadAssetAtPath<StoryCatalog>("Assets/Story/StoryCatalog.asset");
    private static StoryData Story(ReviewedRoute route) => AssetDatabase.LoadAssetAtPath<StoryData>("Assets/Story/" + route.assetName + ".asset");
    private static StoryUI UI() => UnityEngine.Object.FindAnyObjectByType<StoryUI>();
    private static GameplayBoardBridge Bridge() => UnityEngine.Object.FindAnyObjectByType<GameplayBoardBridge>();
    private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj, value);
    private static void Call(object obj, string name) => obj.GetType().GetMethod(name, Private).Invoke(obj, null);
    private static void SetLibrary(DeckLibrary library) => typeof(DeckLibrary).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, library);

    private static DeckLibrary NewLibrary(){
        var path = SessionState.GetString(SavePath, "");
        Check(Path.GetFullPath(path).StartsWith(Path.GetFullPath("Logs") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Test save stays under Logs.");
        return (DeckLibrary)Activator.CreateInstance(typeof(DeckLibrary), Private, null,
            new object[] { Catalog().deckCatalog, path }, null);
    }

    private static void Check(bool valid, string message){
        assertions++;
        if(!valid) throw new InvalidOperationException(message);
    }

    private static void Log(string message){
        notes.Add(message);
        File.WriteAllText("Logs/six-route-progress.txt", message);
        Debug.Log("[Six routes] " + message);
    }

    private static void Compare(StoryDialogue actual, StoryDialogue expected, string label){
        Check(actual.lines.Count == expected.lines.Count, label + " box count.");
        for(int i = 0; i < expected.lines.Count; i++){
            var line = actual.lines[i];
            Check(line.speaker == expected.lines[i].speaker && line.text == expected.lines[i].text, label + " exact reviewed text at " + i + ".");
            Check(line.avatars != null && line.avatars.Count > 0 && line.avatars.All(a => a.sprite && a.slot >= 0 && a.slot < 6) &&
                line.avatars.Select(a => a.slot).Distinct().Count() == line.avatars.Count, label + " valid visible cast.");
            Check(line.speaker == "Narrator" ? !line.portrait : line.portrait, label + " log portrait.");
        }
    }

    [MenuItem("Tools/Story/Check Six Reviewed Routes")]
    public static void CheckAssets(){
        var manifest = ReadManifest(); var catalog = Catalog();
        Check(manifest.routes.Length == 6 && catalog && catalog.Validate(out _), "Six routes and catalog structure.");
        Check(catalog.deckCatalog.ValidateCatalog(out _), "Registered cards/classes.");
        int total = 0;
        foreach(var route in manifest.routes){
            var story = Story(route);
            Check(story && story.id == IDs[route.characterClass] && (int)story.Class == route.characterClass &&
                story.HasPlayableContent(catalog.deckCatalog), route.assetName + " permanent ID and playable content.");
            Compare(story.prologue, route.opening, route.assetName + " opening");
            Compare(story.epilogue, route.ending, route.assetName + " ending");
            total += story.prologue.lines.Count + story.epilogue.lines.Count;
            Check(story.epilogue.lines.Count <= 8, "Ending is at most eight boxes.");
            var owned = catalog.deckCatalog.startingCollection.Where(c => c).Select(c => c.id).ToList();
            Check(story.playerStarterDeck && story.playerStarterDeck.classes.Any(c => c.identity == story.Class) &&
                DeckRules.Validate(story.playerStarterDeck.CreateRecord(), catalog.deckCatalog, owned, out _), route.assetName + " starter is usable with starting ownership.");
            foreach(var expected in route.auditions){
                var audition = story.Audition(expected.opponent);
                Check(audition != null && audition.location == expected.location, "Opponent identity and location.");
                Compare(audition.before, expected.before, route.assetName + " / " + expected.opponent + " before");
                Compare(audition.after, expected.after, route.assetName + " / " + expected.opponent + " after");
                total += audition.before.lines.Count + audition.after.lines.Count;
                Check(audition.before.lines.Count >= 4 && audition.before.lines.Count <= 6 && audition.after.lines.Count >= 4 && audition.after.lines.Count <= 6, "Regular scene box limits.");
                foreach(StoryDifficulty difficulty in Enum.GetValues(typeof(StoryDifficulty)))
                    Check(DeckRules.Validate(audition.Deck(difficulty).CreateRecord(), catalog.deckCatalog,
                        catalog.deckCatalog.cards.Select(c => c.id).ToList(), out _), "Enemy deck legality: " + expected.opponent + " / " + difficulty);
            }
            var incomplete = UnityEngine.Object.Instantiate(story);
            incomplete.auditions[0].easyDeck = null;
            Check(!incomplete.HasPlayableContent(catalog.deckCatalog) && story.HasPlayableContent(catalog.deckCatalog), "Incomplete route stays unavailable without altering its source.");
            UnityEngine.Object.DestroyImmediate(incomplete);
        }
        Check(total == 439, "All 439 reviewed boxes are imported.");
        Check(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == "Assets/Scenes/Story.unity"), "Story scene is enabled.");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/six-route-content-checks.txt", "PASS\n6 routes, 72 sequences, 439 exact reviewed dialogue boxes; valid cast, rewards, starters and difficulty decks.\n");
    }

    [MenuItem("Tools/Story/Run Six Route Flow Checks")]
    public static void Run(){
        try{
            CheckAssets();
            Directory.CreateDirectory("Logs");
            var root = Path.GetFullPath("Logs/six-route-check-" + Guid.NewGuid().ToString("N"));
            SessionState.SetString(Root, root);
            SessionState.SetString(SavePath, Path.Combine(root, "initial", "decks-v1.json"));
            SessionState.SetBool(Running, true);
            EditorSceneManager.OpenScene("Assets/Scenes/Story.unity");
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }catch(Exception ex){ Finish(false, ex.ToString()); }
    }

    [MenuItem("Tools/Story/Preview Reviewed Dialogue (Isolated Save)")]
    public static void BeginPreview(){
        if(EditorApplication.isPlaying) return;
        SessionState.SetString(SavePath, Path.GetFullPath("Logs/six-route-preview-" + Guid.NewGuid().ToString("N") + "/decks-v1.json"));
        SessionState.SetBool(Previewing, true);
        EditorSceneManager.OpenScene("Assets/Scenes/Story.unity");
        EditorApplication.EnterPlaymode();
    }

    [MenuItem("Tools/Story/End Isolated Preview")]
    public static void EndPreview(){
        SessionState.SetBool(Previewing, false);
        if(EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }

    private static void Tick(){
        if(!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if(EditorApplication.isPaused) EditorApplication.isPaused = false;
        EditorApplication.QueuePlayerLoopUpdate();
        try{
            if(steps == null) steps = Flow();
            if(!steps.MoveNext()) Finish(true, "Six routes passed exact-content checks, two opponent orders, mixed-deck identities, full dialogue layout, wins/losses/draws, save retry, replay and legacy progress.");
        }catch(Exception ex){ Finish(false, ex.ToString()); }
    }

    private static IEnumerator WaitFor(string scene){
        float deadline = Time.realtimeSinceStartup + 30;
        while(SceneManager.GetActiveScene().name != scene || (scene == "Story" ? UI() == null || typeof(StoryUI).GetField("library", Private).GetValue(UI()) == null : Bridge() == null || Bridge().Snapshot == null)){
            if(Time.realtimeSinceStartup > deadline) throw new TimeoutException("Waiting for " + scene);
            yield return null;
        }
        yield return null;
    }

    private static IEnumerator ReadDialogue(StoryDialogue script){
        var ui = UI().dialogue;
        for(int i = 0; i < script.lines.Count; i++){
            yield return null;
            Canvas.ForceUpdateCanvases(); ui.line.ForceMeshUpdate();
            var expected = script.lines[i];
            Check(ui.panel.activeSelf && ui.line.text == expected.text && ui.speaker.text == expected.speaker, "Displayed dialogue matches reviewed line.");
            Check(!ui.line.isTextOverflowing, "Dialogue fits: " + expected.text);
            bool narrated = expected.speaker == "Narrator";
            Check(ui.speaker.gameObject.activeSelf == !narrated && ui.narrationBackground.activeSelf == narrated &&
                ui.characterBackground.activeSelf == !narrated, "Narrator/character nameplate transition.");
            foreach(var avatar in expected.avatars)
                Check(ui.avatars[avatar.slot].gameObject.activeSelf && ui.avatars[avatar.slot].sprite == avatar.sprite, "Correct visible cast.");
            renderedBoxes++;
            ui.line.maxVisibleCharacters = int.MaxValue;
            ui.Next();
        }
    }

    private static void StartBattle(StoryData story, CharacterClass opponent){
        var ui = UI(); ui.SelectAudition(opponent);
        Check(ui.start.interactable, "A legal protagonist deck is selected.");
        ui.start.onClick.Invoke();
        Check(ui.dialogue.panel.activeSelf, "Before dialogue precedes battle launch.");
    }

    private static void CheckBattle(StoryData story, CharacterClass opponent){
        var bridge = Bridge();
        Check(bridge.IsStory && string.IsNullOrEmpty(bridge.MatchSetupError), "Real Game scene accepts story inputs.");
        Check(bridge.playerClasses.First().identity == story.Class && bridge.enemyClasses.First().identity == opponent, "Player and rival stay on their named stages with support cards.");
        battles++;
    }

    private static void Result(int winner){
        Bridge().Snapshot.phase = RoundPhase.Finished;
        Bridge().Snapshot.winnerId = winner;
        Bridge().SaveStoryReward();
    }

    private static IEnumerator Flow(){
        var wait = WaitFor("Story"); while(wait.MoveNext()) yield return null;
        CheckAssets();
        var manifest = ReadManifest();
        for(int order = 0; order < 2; order++) foreach(var route in manifest.routes){
            var story = Story(route);
            SessionState.SetString(SavePath, Path.Combine(SessionState.GetString(Root, ""), route.assetName + "-" + order, "decks-v1.json"));
            SetLibrary(NewLibrary()); StorySession.Clear(); SceneManager.LoadScene("Story");
            wait = WaitFor("Story"); while(wait.MoveNext()) yield return null;
            var library = DeckLibrary.Get(); var oldSelected = library.Selected.id;
            UI().SelectStory(story); UI().begin.onClick.Invoke();
            Check(UI().dialogue.panel.activeSelf && library.Selected.id == oldSelected, "Opening and story starter preserve global deck selection.");
            int decks = library.Decks.Count;
            Check(library.EnsureStoryDeck(story, out _, out _) && library.Decks.Count == decks, "Repeated begin never duplicates a usable starter.");
            var read = ReadDialogue(story.prologue); while(read.MoveNext()) yield return null;
            Check(library.HasViewedStoryDialogue(story, false), "Opening viewed flag is saved.");
            var opponents = story.auditions.Select(a => a.opponent).ToArray(); if(order == 1) Array.Reverse(opponents);
            if(order == 0){
                foreach(int loss in new[] { 1, -1 }){
                    StartBattle(story, opponents[0]); UI().dialogue.Finish();
                    wait = WaitFor("Game"); while(wait.MoveNext()) yield return null;
                    CheckBattle(story, opponents[0]); Result(loss);
                    Check(library.StoryClearCount(story) == 0 && Bridge().ReturnToStory(), "Loss/draw awards no clear and returns.");
                    wait = WaitFor("Story"); while(wait.MoveNext()) yield return null;
                    Check(!UI().dialogue.panel.activeSelf && !UI().after.interactable, "Loss/draw leaves after scene locked.");
                }
            }
            foreach(var opponent in opponents){
                int beforeCount = library.StoryClearCount(story);
                StartBattle(story, opponent);
                if(order == 0){ read = ReadDialogue(story.Audition(opponent).before); while(read.MoveNext()) yield return null; }
                else UI().dialogue.Finish();
                wait = WaitFor("Game"); while(wait.MoveNext()) yield return null;
                CheckBattle(story, opponent);
                Check(StorySession.EnemyDeck == story.Audition(opponent).Deck(StorySession.AutomaticDifficulty(beforeCount)), "Automatic difficulty uses the configured slot.");
                if(order == 0 && beforeCount == 0){
                    var save = SessionState.GetString(SavePath, "");
                    Directory.CreateDirectory(save + ".tmp"); Result(0);
                    Check(!string.IsNullOrEmpty(Bridge().StorySaveError) && library.StoryClearCount(story) == 0 && !Bridge().ReturnToStory(), "Save failure retains the result without awarding progress.");
                    Directory.Delete(save + ".tmp"); Bridge().RetryStorySave();
                }else Result(0);
                Check(string.IsNullOrEmpty(Bridge().StorySaveError) && library.StoryClearCount(story) == beforeCount + 1, "Win saves exactly one new clear.");
                Check(Bridge().ReturnToStory(), "Saved win returns to Story.");
                wait = WaitFor("Story"); while(wait.MoveNext()) yield return null;
                Check(UI().dialogue.panel.activeSelf && UI().dialogue.line.text == story.Audition(opponent).after.lines[0].text, "Return selects the named opponent's after scene.");
                if(order == 0){ read = ReadDialogue(story.Audition(opponent).after); while(read.MoveNext()) yield return null; }
                else UI().dialogue.Finish();
                if(beforeCount == 4){
                    Check(UI().dialogue.panel.activeSelf && UI().dialogue.line.text == story.epilogue.lines[0].text, "Fifth clear launches this route's ending.");
                    read = ReadDialogue(story.epilogue); while(read.MoveNext()) yield return null;
                }else Check(!UI().dialogue.panel.activeSelf, "Ending waits for five distinct clears.");
            }
            string first = library.StoryFirstClear(story, opponents[0]); int owned = library.Owned.Count;
            Check(library.CompleteAudition(story, opponents[0], out _) && library.StoryFirstClear(story, opponents[0]) == first && library.Owned.Count == owned, "Repeated clear keeps timestamp and unique rewards.");
            UI().prologue.onClick.Invoke(); Check(UI().dialogue.panel.activeSelf, "Opening replay."); UI().dialogue.Finish();
            UI().epilogue.onClick.Invoke(); Check(UI().dialogue.panel.activeSelf, "Ending replay."); UI().dialogue.Finish();
            var reloaded = NewLibrary();
            Check(reloaded.IsStoryCompleted(story) && reloaded.HasViewedStoryDialogue(story, false) && reloaded.HasViewedStoryDialogue(story, true) &&
                reloaded.Owned.Distinct().Count() == reloaded.Owned.Count, "Clears, dialogue flags and unique ownership survive reload.");
            Log(route.assetName + " order " + order + " PASS; battles=" + battles + ", rendered boxes=" + renderedBoxes);
        }
        CheckLegacySave();
    }

    private static void CheckLegacySave(){
        var story = Story(ReadManifest().routes[0]); var catalog = Catalog().deckCatalog;
        var deck = catalog.starterDeck.CreateRecord(); deck.id = "legacy-player-deck"; deck.deckName = "Legacy Deck";
        var old = new LegacySave { selectedId = deck.id, decks = new List<DeckRecord> { deck },
            owned = deck.cardIds.Concat(new[] { deck.vipCardId }).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList(),
            completedPuzzles = new List<string> { "legacy-puzzle" },
            viewedStoryDialogues = new List<string> { "story-hatsunemiku/prologue", "story-hatsunemiku/epilogue" } };
        foreach(var audition in story.auditions) old.storyClears.Add(new DeckLibrary.StoryClearRecord {
            storyId = "story-hatsunemiku", opponent = audition.opponent, firstClearUtc = "2026-10-07T12:00:00.0000000Z" });
        var path = Path.Combine(SessionState.GetString(Root, ""), "legacy", "decks-v1.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, JsonUtility.ToJson(old));
        SessionState.SetString(SavePath, path); var reloaded = NewLibrary();
        Check(reloaded.Selected.id == deck.id && reloaded.Selected.deckName == deck.deckName && reloaded.IsStoryCompleted(story) &&
            reloaded.HasViewedStoryDialogue(story, false) && reloaded.HasViewedStoryDialogue(story, true), "Literal version-1 legacy save retains decks, selection, five clears and viewed flags.");
        foreach(var audition in story.auditions)
            Check(reloaded.StoryFirstClear(story, audition.opponent) == "2026-10-07T12:00:00.0000000Z", "Legacy first-clear timestamp.");
        Log("Legacy version-1 Miku save PASS.");
    }

    private static void Finish(bool success, string message){
        SessionState.SetBool(Running, false); EditorApplication.update -= Tick; steps = null;
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/six-route-flow-checks.txt", (success ? "PASS\n" : "FAIL\n") + message +
            "\nAssertions=" + assertions + "; battle launches=" + battles + "; rendered boxes=" + renderedBoxes + "\n" + string.Join("\n", notes));
        Debug.Log("[Six routes] " + (success ? "PASS " : "FAIL ") + message);
        if(Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1); else EditorApplication.ExitPlaymode();
    }
}
#endif
