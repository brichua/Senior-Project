#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VocaloidTCG;
using VocaloidTCG.BoardUI;

// Development checks use a separate save in Logs; they never change the player's collection.
[InitializeOnLoad]
public static class MikuStoryChecks
{
    private const string Running = "MikuStoryChecks.Running";
    private const string SavePath = "MikuStoryChecks.SavePath";
    private static IEnumerator checks;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    static MikuStoryChecks(){
        if(SessionState.GetBool(Running, false)) EditorApplication.update += Tick;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void UseTestSave(){
        if(!SessionState.GetBool(Running, false)) return;
        var library = NewLibrary();
        typeof(DeckLibrary).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, library);
        Application.runInBackground = true;
    }

    private static DeckLibrary NewLibrary(){
        var catalog = AssetDatabase.LoadAssetAtPath<DeckCatalog>("Assets/Decks/deck catalog.asset");
        return (DeckLibrary)Activator.CreateInstance(typeof(DeckLibrary), Private, null,
            new object[] { catalog, SessionState.GetString(SavePath, "") }, null);
    }

    private static void Check(bool condition, string message){
        if(!condition) throw new InvalidOperationException(message);
        Debug.Log("[Miku check] " + message);
    }

    [MenuItem("Tools/Story/Check Miku Content")]
    public static void CheckAssets(){
        var catalog = AssetDatabase.LoadAssetAtPath<StoryCatalog>("Assets/Story/StoryCatalog.asset");
        var story = AssetDatabase.LoadAssetAtPath<StoryData>("Assets/Story/HatsuneMiku.asset");
        Check(catalog && catalog.Validate(out _), "Story catalog structure is valid.");
        Check(catalog.deckCatalog.ValidateCatalog(out _), "Deck catalog is valid.");
        Check(story.id == "story-hatsunemiku" && story.Validate(out _), "Miku keeps its permanent ID and five opponents.");
        var scripts = new[] { story.prologue, story.epilogue }.Concat(story.auditions.SelectMany(a => new[] { a.before, a.after })).ToList();
        Check(scripts.Count == 12 && scripts.Sum(s => s.lines.Count) == 62, "Twelve sequences contain 62 dialogue boxes.");
        Check(scripts.All(s => s.lines.Count >= 4 && s.lines.Count <= 6 && s.lines.All(l => !string.IsNullOrWhiteSpace(l.text))), "Every sequence has four to six nonempty lines.");
        Check(scripts.All(s => s.lines.All(l => l.avatars.All(a => a.sprite))), "Every dialogue avatar resolves to an existing sprite.");
        foreach(var audition in story.auditions){
            Check(catalog.deckCatalog.Card(audition.firstClearReward.id) == audition.firstClearReward, audition.opponent + " reward is registered.");
            foreach(StoryDifficulty difficulty in Enum.GetValues(typeof(StoryDifficulty))){
                var deck = audition.Deck(difficulty);
                Check(deck && deck.classes.Any(c => c.identity == audition.opponent) && deck.cards.Count > 0 &&
                    deck.cards.All(c => c && catalog.deckCatalog.Card(c.id) == c && deck.classes.Contains(c.cardClass)), audition.opponent + " " + difficulty + " deck is usable.");
                Check(DeckRules.Validate(deck.CreateRecord(), catalog.deckCatalog, catalog.deckCatalog.cards.Select(c => c.id).ToList(), out var error), audition.opponent + " deck rules: " + error);
            }
        }
        Check(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == "Assets/Scenes/Story.unity"), "Story is enabled in build settings.");
    }

    [MenuItem("Tools/Story/Run Miku Flow Checks")]
    public static void Run(){
        try{
            CheckAssets();
            Directory.CreateDirectory("Logs");
            SessionState.SetString(SavePath, Path.GetFullPath("Logs/miku-check-" + Guid.NewGuid().ToString("N") + "/decks-v1.json"));
            SessionState.SetBool(Running, true);
            EditorSceneManager.OpenScene("Assets/Scenes/Story.unity");
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }catch(Exception ex){ Finish(false, ex.ToString()); }
    }

    private static void Tick(){
        if(!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        // Console Error Pause can stop the player loop (including our deliberate save error).
        if(EditorApplication.isPaused) EditorApplication.isPaused = false;
        EditorApplication.QueuePlayerLoopUpdate();
        try{
            if(checks == null) checks = Flow();
            if(!checks.MoveNext()) Finish(true, "Content, dialogue, launch, loss/draw, save retry, five wins, replay and persistence checks passed.");
        }catch(Exception ex){ Finish(false, ex.ToString()); }
    }

    private static StoryUI UI() => UnityEngine.Object.FindFirstObjectByType<StoryUI>();
    private static GameplayBoardBridge Bridge() => UnityEngine.Object.FindFirstObjectByType<GameplayBoardBridge>();
    private static object Field(object obj, string name) => obj.GetType().GetField(name, Private).GetValue(obj);
    private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj, value);
    private static void Call(object obj, string name) => obj.GetType().GetMethod(name, Private).Invoke(obj, null);

    private static IEnumerator WaitFor(string scene){
        float deadline = Time.realtimeSinceStartup + 20;
        while(SceneManager.GetActiveScene().name != scene || (scene == "Story" ? UI() == null || Field(UI(), "library") == null : Bridge() == null || Bridge().Snapshot == null)){
            if(Time.realtimeSinceStartup > deadline) throw new TimeoutException("Waiting for " + scene);
            yield return null;
        }
        yield return null;
    }

    private static void StartBattle(CharacterClass opponent){
        var ui = UI(); var library = DeckLibrary.Get();
        ui.SelectAudition(opponent);
        Set(ui, "selectedDeckId", library.Decks.First(d => d.classes.Contains(CharacterClass.HatsuneMiku)).id);
        Call(ui, "BuildDecks");
        ui.start.onClick.Invoke();
        Check(ui.dialogue.panel.activeSelf && SceneManager.GetActiveScene().name == "Story", opponent + " dialogue plays before loading battle.");
        ui.dialogue.Finish();
    }

    private static void Result(int winner){
        var bridge = Bridge();
        Check(bridge.IsStory && string.IsNullOrEmpty(bridge.MatchSetupError), "Battle consumes the story launch without setup errors.");
        bridge.Snapshot.phase = RoundPhase.Finished;
        bridge.Snapshot.winnerId = winner;
        bridge.SaveStoryReward();
    }

    private static IEnumerator Flow(){
        var wait = WaitFor("Story"); while(wait.MoveNext()) yield return null;
        var story = AssetDatabase.LoadAssetAtPath<StoryData>("Assets/Story/HatsuneMiku.asset");
        var library = DeckLibrary.Get();
        Check((string)Field(library, "path") == SessionState.GetString(SavePath, ""), "Playtest uses an isolated save.");
        UI().SelectStory(story); UI().begin.onClick.Invoke();
        Check(UI().dialogue.panel.activeSelf && UI().dialogue.speaker.text == "Narrator", "First entry opens narrated prologue.");
        var dialogue = UI().dialogue;
        dialogue.Next(); dialogue.ToggleLog(); var current = dialogue.line.text; dialogue.Next();
        Check(dialogue.logPanel.activeSelf && dialogue.line.text == current, "Log pauses manual advancement.");
        dialogue.CloseLog(); dialogue.charactersPerSecond = 10000; dialogue.autoDelay = 0.01f; dialogue.ToggleAuto();
        float deadline = Time.realtimeSinceStartup + 15;
        while(dialogue.panel.activeSelf){
            if(Time.realtimeSinceStartup > deadline) throw new TimeoutException("Automatic dialogue playback: frame " + Time.frameCount + ", paused " + EditorApplication.isPaused + ", line " + dialogue.line.text);
            yield return null;
        }
        Check(library.HasViewedStoryDialogue(story, false) && UI().mapPanel.activeSelf, "Automatic prologue ends on the map and saves viewed state.");
        UI().prologue.onClick.Invoke(); Check(dialogue.panel.activeSelf, "Prologue remains replayable."); dialogue.Finish();
        UI().begin.onClick.Invoke(); Check(!dialogue.panel.activeSelf, "Seen prologue is not forced again.");

        foreach(int outcome in new[] { 1, -1 }){
            StartBattle(CharacterClass.KagamineLen); wait = WaitFor("Game"); while(wait.MoveNext()) yield return null;
            Result(outcome);
            Check(!library.IsAuditionCompleted(story, CharacterClass.KagamineLen), "Loss/draw does not award a clear.");
            Check(Bridge().ReturnToStory(), "Loss/draw can return to audition.");
            wait = WaitFor("Story"); while(wait.MoveNext()) yield return null;
            Check(!UI().dialogue.panel.activeSelf && !UI().after.interactable, "Loss/draw does not play or unlock after-win dialogue.");
        }

        StartBattle(CharacterClass.KagamineLen); wait = WaitFor("Game"); while(wait.MoveNext()) yield return null;
        string save = SessionState.GetString(SavePath, "");
        Directory.CreateDirectory(save + ".tmp");
        Result(0);
        Check(!string.IsNullOrEmpty(Bridge().StorySaveError) && !library.IsAuditionCompleted(story, CharacterClass.KagamineLen), "Failed save keeps progress unchanged.");
        Check(!Bridge().ReturnToStory(), "Failed save prevents leaving the result.");
        Directory.Delete(save + ".tmp");
        Bridge().RetryStorySave();
        Check(string.IsNullOrEmpty(Bridge().StorySaveError) && library.StoryClearCount(story) == 1, "Retry saves the win.");
        string firstClear = library.StoryFirstClear(story, CharacterClass.KagamineLen);
        int owned = library.Owned.Count;
        Check(Bridge().ReturnToStory(), "Saved win returns to Story.");
        wait = WaitFor("Story"); while(wait.MoveNext()) yield return null;
        Check(UI().dialogue.panel.activeSelf && UI().dialogue.line.text == story.Audition(CharacterClass.KagamineLen).after.lines[0].text, "Return from win automatically opens the correct after scene.");
        UI().dialogue.Finish();

        foreach(var opponent in new[] { CharacterClass.KagamineLen, CharacterClass.KagamineRin, CharacterClass.KAITO, CharacterClass.MEIKO, CharacterClass.MegurineLuka }){
            StartBattle(opponent); wait = WaitFor("Game"); while(wait.MoveNext()) yield return null;
            Check(Bridge().enemyClasses.First().identity == opponent, "Named opponent stays the primary stage class.");
            Result(0); Check(Bridge().ReturnToStory(), "Win can return to Story.");
            wait = WaitFor("Story"); while(wait.MoveNext()) yield return null;
            Check(UI().dialogue.panel.activeSelf, "Win opens an after scene."); UI().dialogue.Finish();
            if(opponent == CharacterClass.KagamineLen)
                Check(library.StoryClearCount(story) == 1 && library.StoryFirstClear(story, opponent) == firstClear && library.Owned.Count == owned, "Replay preserves first-clear date and does not duplicate rewards.");
        }
        Check(library.IsStoryCompleted(story) && UI().dialogue.panel.activeSelf && UI().dialogue.line.text == story.epilogue.lines[0].text, "Fifth saved win leads from the after scene to the ending.");
        UI().dialogue.Finish();
        Check(UI().mapPanel.activeSelf && UI().epilogue.interactable, "Ending returns to the completed map and can be replayed.");
        var reloaded = NewLibrary();
        Check(reloaded.IsStoryCompleted(story) && reloaded.HasViewedStoryDialogue(story, false) && reloaded.HasViewedStoryDialogue(story, true), "Clears and viewed scenes survive save reload.");
        Check(reloaded.Owned.Distinct().Count() == reloaded.Owned.Count, "Owned collection remains unique.");
    }

    private static void Finish(bool success, string message){
        SessionState.SetBool(Running, false);
        EditorApplication.update -= Tick;
        checks = null;
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/miku-story-checks.txt", (success ? "PASS\n" : "FAIL\n") + message);
        Debug.Log("[Miku check] " + (success ? "PASS " : "FAIL ") + message);
        if(Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
        else EditorApplication.ExitPlaymode();
    }
}
#endif
