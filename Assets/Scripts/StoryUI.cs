using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VocaloidTCG
{
    public sealed class StoryUI : MonoBehaviour
    {
        [Serializable] public sealed class LocationPin { public CharacterClass characterClass; public Button button; }
        public StoryCatalog catalog;
        public string mainScene = "Main", gameScene = "Game", deckScene = "Edit Deck";
        public GameObject classPanel, mapPanel, auditionPanel, storyInfo, selectedDeckPanel;
        public Transform classRoot, rivalRoot, mapRoot, deckRoot;
        public StoryChoiceView classPrefab, tutorialPrefab, rivalPrefab, mapPrefab, deckPrefab;
        public LocationPin[] locationPins;
        public StoryChoiceView[] difficultyButtons = new StoryChoiceView[3];
        public GameObject difficultyGroup, difficultyLocked, afterLocked, afterUnlocked, auditionPassed;
        public Button begin, classBack, mapBack, auditionBack, prologue, epilogue, before, after, editDeck, start;
        public Image selectedClassImage, auditionClassImage, rewardArt, deckClass, deckSecondClass, cardBack, epilogueImage;
        public Sprite epilogueLockedSprite, epilogueUnlockedSprite;
        public TMP_Text className, storyHeader, storyCount, overallCount, mapCount, auditionClassName, deckDescription,
            location, auditionText, rewardName, selectedDeckName, deckCount, firstClear, status;
        public Slider storySlider, overallSlider, mapSlider;
        public StoryDialogueUI dialogue;
        [Range(0f, 1f)] public float lockedClassOpacity = 0.4f;
        [Tooltip("Bypass the tutorial requirement for story selection without changing saved tutorial progress.")]
        public bool debugTutorialCompleted;
        private DeckLibrary library;
        private StoryData selected;
        private StoryAudition audition;
        private string selectedDeckId;
        private StoryDifficulty? chosenDifficulty;
        private bool lastDebugTutorialCompleted;

        private readonly List<StoryChoiceView> classViews = new List<StoryChoiceView>();
        private readonly List<StoryChoiceView> rivalViews = new List<StoryChoiceView>();
        private readonly List<StoryChoiceView> mapViews = new List<StoryChoiceView>();
        private readonly List<StoryChoiceView> deckViews = new List<StoryChoiceView>();

        private void Awake(){
            if(!status) status = StoryUIElements.Text(transform, "Story status", "", new Vector2(0, -485), new Vector2(1500, 60));
            Active(storyInfo, false);
            if(rivalRoot) Active(rivalRoot.gameObject, false);
        }

        private void Start(){
            try{
                ValidateSetup();
                if(!catalog) throw new InvalidOperationException("Assign a Story Catalog on StoryUI.");
                if(!catalog.Validate(out var error)) throw new InvalidOperationException(error);
                library = DeckLibrary.Get(catalog.deckCatalog);
                if(library.IsStoryCompleted(catalog.Tutorial)) TutorialProgress.SetCompleted();

                Hook(begin, BeginStory); Hook(classBack, BackToMain); Hook(mapBack, ShowClasses); Hook(auditionBack, ShowMap);
                Hook(prologue, () => PlayDialogue(selected?.prologue, false));
                Hook(epilogue, PlayEnding);
                Hook(before, () => PlayDialogue(audition?.before, true));
                Hook(after, () => { if(audition != null && library.IsAuditionCompleted(selected, audition.opponent)) PlayDialogue(audition.after, true); });
                Hook(editDeck, EditDeck); Hook(start, StartAudition);

                foreach(var pin in locationPins ?? new LocationPin[0]){
                    if(pin == null) continue;
                    var captured = pin.characterClass; Hook(pin.button, () => SelectAudition(captured));
                }

                for(int i = 0; i < (difficultyButtons?.Length ?? 0); i++){
                    int choice = i;
                    if(difficultyButtons[i]) difficultyButtons[i].Bind(() => {
                        if(!library.IsStoryCompleted(selected)) return;
                        chosenDifficulty = (StoryDifficulty)choice; Remember(); RefreshDifficulty();
                    });
                }

                BuildClasses();
                if(StorySession.Story && catalog.stories.Contains(StorySession.Story) && CanPlay(StorySession.Story)){
                    selectedDeckId = StorySession.DeckId; chosenDifficulty = StorySession.Difficulty;
                    SelectStory(StorySession.Story);
                    SelectAudition(StorySession.Opponent);
                    bool playAfter = StorySession.PlayAfterBattle;
                    StorySession.PlayAfterBattle = false;
                    if(playAfter && audition != null && library.IsAuditionCompleted(selected, audition.opponent))
                        PlayDialogue(audition.after, true, () => {
                            if(library.IsStoryCompleted(selected) && !library.HasViewedStoryDialogue(selected, true)) PlayEnding();
                            else SelectAudition(audition.opponent);
                        });
                }
                else ShowClasses();
            }
            catch(Exception ex){
                DeckUI.Text(status, ex.Message);
                Debug.LogError("[Story Setup] StoryUI initialization failed: " + ex.Message, this);
                if(start) start.interactable = false;
            }
        }

        [ContextMenu("Validate Story UI Setup")]
        public void ValidateSetup(){
            StoryDiagnostics.CheckUI(this);
        }

        private static void Hook(Button button, UnityEngine.Events.UnityAction action){
            if(button) button.onClick.AddListener(action);
        }

        private static void Active(GameObject obj, bool value){
            if(obj) obj.SetActive(value);
        }

        private void Report(string message){
            DeckUI.Text(status, message);
            if(!string.IsNullOrEmpty(message)) Debug.LogWarning(message, this);
        }

        private bool CanPlay(StoryData story) => story && HasDialogue(story.prologue) &&
            (story.tutorial || debugTutorialCompleted || TutorialProgress.Completed || library.IsStoryCompleted(catalog.Tutorial));

        private void Update(){
            if(library == null || !catalog || lastDebugTutorialCompleted == debugTutorialCompleted) return;
            BuildClasses();
            if(selected && !CanPlay(selected)){
                selected = null; audition = null;
                ShowClasses();
            }
        }

        private static void Clear(List<StoryChoiceView> views){
            foreach(var view in views) if(view){
                view.gameObject.SetActive(false);
                Destroy(view.gameObject);
            }

            views.Clear();
        }

        private static StoryChoiceView Spawn(StoryChoiceView prefab, Transform root, List<StoryChoiceView> views){
            if(!prefab || !root) return null;
            var view = Instantiate(prefab, root); view.gameObject.SetActive(true); views.Add(view); return view;
        }

        private void BuildClasses(){
            lastDebugTutorialCompleted = debugTutorialCompleted;
            Clear(classViews);

            foreach(var story in catalog.stories.OrderByDescending(s => s.tutorial)){
                var view = Spawn(story.tutorial && tutorialPrefab ? tutorialPrefab : classPrefab, classRoot, classViews);
                if(!view) continue;
                bool unlocked = CanPlay(story);
                view.Bind(() => SelectStory(story), unlocked);
                var group = view.GetComponent<CanvasGroup>();
                if(!group) group = view.gameObject.AddComponent<CanvasGroup>();
                group.alpha = unlocked ? 1f : lockedClassOpacity;
                if(!story.tutorial) view.SetClass(library.Catalog.Class(story.Class));
                view.SetProgress(library.StoryClearCount(story));
            }

            Progress(overallCount, overallSlider, catalog.stories.Sum(library.StoryClearCount), 35);
        }

        private static void Progress(TMP_Text text, Slider slider, int count, int total = 5){
            DeckUI.Text(text, count + "/" + total);
            if(slider){
                slider.minValue = 0;
                slider.maxValue = total;
                slider.interactable = false;
                slider.SetValueWithoutNotify(count);
            }
        }

        private void Panels(GameObject show){
            Active(classPanel, show == classPanel);
            Active(mapPanel, show == mapPanel);
            Active(auditionPanel, show == auditionPanel);
            if(dialogue) Active(dialogue.panel, false);
        }

        public void ShowClasses(){
            Panels(classPanel);
            Active(storyInfo, selected);
            if(rivalRoot) Active(rivalRoot.gameObject, selected);
        }

        public void SelectStory(StoryData story){
            if(!CanPlay(story)) return;
            if(selected && selected != story){
                selectedDeckId = null;
                chosenDifficulty = null;
            }

            selected = story; audition = null;
            var data = library.Catalog.Class(story.Class);
            DeckUI.Text(className, story.tutorial ? "Tutorial" : data.DisplayName); if(className) className.color = data.color;
            DeckUI.Text(storyHeader, story.header); DeckUI.Image(selectedClassImage, data.selectedButtonImage);
            Progress(storyCount, storySlider, library.StoryClearCount(story));
            Clear(rivalViews);

            foreach(var other in story.auditions){
                var view = Spawn(rivalPrefab, rivalRoot, rivalViews);
                if(view) view.SetClass(library.Catalog.Class(other.opponent));
            }

            ShowClasses();
        }

        public void ShowMap(){
            if(!selected) return;
            Panels(mapPanel); Clear(mapViews);

            foreach(var pin in locationPins ?? new LocationPin[0])
                if(pin != null && pin.button) pin.button.gameObject.SetActive(pin.characterClass != selected.Class);
            foreach(var other in selected.auditions){
                var view = Spawn(mapPrefab, mapRoot, mapViews);
                if(!view) continue;
                view.Bind(() => SelectAudition(other.opponent)); view.SetClass(library.Catalog.Class(other.opponent));
                view.SetProgress(library.IsAuditionCompleted(selected, other.opponent) ? 1 : 0, 1);
            }

            Progress(mapCount, mapSlider, library.StoryClearCount(selected));
            bool completed = library.IsStoryCompleted(selected);
            if(epilogue) epilogue.interactable = completed;
            if(epilogueImage) epilogueImage.sprite = completed ? epilogueUnlockedSprite : epilogueLockedSprite;
        }

        private void BeginStory(){
            if(!selected) return;
            if(library.HasViewedStoryDialogue(selected, false) || !HasDialogue(selected.prologue)) { ShowMap(); return; }
            PlayDialogue(selected.prologue, false, () => {
                ShowMap();
                if(!library.MarkStoryDialogueViewed(selected, false, out var error)) Report(error);
            });
        }

        private void PlayEnding(){
            if(!library.IsStoryCompleted(selected)) return;
            PlayDialogue(selected.epilogue, false, () => {
                ShowMap();
                if(!library.MarkStoryDialogueViewed(selected, true, out var error)) Report(error);
            });
        }

        private static bool HasDialogue(StoryDialogue script) => script?.lines != null &&
            script.lines.Any(entry => entry != null && !string.IsNullOrWhiteSpace(entry.text));

        public void SelectAudition(CharacterClass opponent){
            if(!selected || !CanPlay(selected)) return;
            audition = selected.Audition(opponent); if(audition == null){
                ShowMap();
                return;
            }

            Panels(auditionPanel);
            var data = library.Catalog.Class(opponent);
            DeckUI.Image(auditionClassImage, data.selectedButtonImage);
            DeckUI.Text(auditionClassName, data.DisplayName); if(auditionClassName) auditionClassName.color = data.color;
            DeckUI.Text(deckDescription, audition.deckDescription); DeckUI.Text(location, audition.location);
            DeckUI.Text(auditionText, audition.auditionText);
            DeckUI.Text(rewardName, audition.firstClearReward ? audition.firstClearReward.cardName : "Reward not assigned");
            DeckUI.Image(rewardArt, audition.firstClearReward ?
                (audition.firstClearReward.iconImage ? audition.firstClearReward.iconImage : audition.firstClearReward.cardImage) : null);
            bool cleared = library.IsAuditionCompleted(selected, opponent);
            Active(auditionPassed, cleared); Active(afterLocked, !cleared); Active(afterUnlocked, cleared);
            if(after) after.interactable = cleared;
            string stamp = library.StoryFirstClear(selected, opponent);
            DeckUI.Text(firstClear, DateTime.TryParse(stamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)
                ? "first clear " + date.ToLocalTime().ToString("MM/dd/yyyy", CultureInfo.InvariantCulture) : "");
            RefreshDifficulty(); BuildDecks(); Remember();
        }

        private void RefreshDifficulty(){
            bool unlocked = library.IsStoryCompleted(selected);
            Active(difficultyGroup, unlocked); Active(difficultyLocked, !unlocked);
            var difficulty = unlocked && chosenDifficulty.HasValue ? chosenDifficulty.Value : StorySession.AutomaticDifficulty(library.StoryClearCount(selected));

            for(int i = 0; i < (difficultyButtons?.Length ?? 0); i++) if(difficultyButtons[i]){
                difficultyButtons[i].gameObject.SetActive(unlocked);
                difficultyButtons[i].SetSelected(i == (int)difficulty);
            }
        }

        private void BuildDecks(){
            Clear(deckViews);
            var decks = library.Decks.Where(d => d.classes.Contains(selected.Class)).ToList();
            if(!decks.Any(d => d.id == selectedDeckId)) selectedDeckId = null;

            foreach(var deck in decks){
                var view = Spawn(deckPrefab, deckRoot, deckViews); if(!view) continue;
                view.Bind(() => {
                    selectedDeckId = deck.id;
                    BuildDecks();
                    Remember();
                });
                DeckUI.Text(view.title, deck.deckName);
                var cover = library.Catalog.Card(deck.vipCardId);
                Sprite art = cover ? cover.cardImage : null;
                if(!art) art = deck.cardIds.Select(library.Catalog.Card).Where(c => c).Select(c => c.cardImage).FirstOrDefault(s => s);
                DeckUI.Image(view.art, art); view.SetSelected(deck.id == selectedDeckId);
            }

            var chosen = decks.Find(d => d.id == selectedDeckId);
            Active(selectedDeckPanel, chosen != null);
            if(start) start.interactable = chosen != null;
            DeckUI.Text(selectedDeckName, chosen?.deckName ?? ""); DeckUI.Text(deckCount, chosen != null ? chosen.Count + "/30" : "");
            if(chosen != null){
                Active(deckClass ? deckClass.gameObject : null, true);
                DeckUI.Classes(deckClass, deckSecondClass, chosen, library.Catalog); DeckUI.Image(cardBack, library.Catalog.Back(chosen));
            }
            else{
                DeckUI.Image(cardBack, null);
                Active(deckClass ? deckClass.gameObject : null, false);
                Active(deckSecondClass ? deckSecondClass.gameObject : null, false);
            }

            Report(decks.Count == 0 ? "Create a deck containing " + library.Catalog.Class(selected.Class).DisplayName + " to begin." : "");
        }

        private void Remember(){
            StorySession.Story = selected; StorySession.Opponent = audition != null ? audition.opponent : selected.Class;
            StorySession.DeckId = selectedDeckId; StorySession.Difficulty = chosenDifficulty;
            StorySession.ReturnScene = gameObject.scene.name;
        }

        private void PlayDialogue(StoryDialogue script, bool returnToAudition, Action onFinished = null){
            if(!selected) return;
            if(!dialogue){
                Debug.LogError("[Story Setup] StoryUI.dialogue: assign StoryDialogueUI before opening a dialogue.", this);
                return;
            }

            Panels(null);
            dialogue.Play(script, () => {
                if(onFinished != null) onFinished();
                else if(returnToAudition && audition != null) SelectAudition(audition.opponent);
                else ShowMap();
            });
        }

        private bool SceneAvailable(string scene){
            if(Application.CanStreamedLevelBeLoaded(scene)) return true;
            string error = "Scene \"" + scene + "\": missing or disabled in the build scene list.";
            DeckUI.Text(status, error); Debug.LogError("[Story Setup] " + error, this); return false;
        }

        public void EditDeck(){
            if(!selected || audition == null || !SceneAvailable(deckScene)) return;
            if(selectedDeckId != null && !library.Select(selectedDeckId, out var error)){
                Report(error);
                return;
            }

            Remember(); StorySession.EditingDeck = true; SceneManager.LoadScene(deckScene);
        }

        public void StartAudition(){
            if(!selected || audition == null || !CanPlay(selected)) return;
            var deck = library.Decks.FirstOrDefault(d => d.id == selectedDeckId);
            if(deck == null || !deck.classes.Contains(selected.Class)){
                Report("Select a deck containing the story class.");
                return;
            }

            if(!DeckRules.Validate(deck, library.Catalog, library.Owned, out var error)){
                Report(error);
                return;
            }

            var difficulty = library.IsStoryCompleted(selected) && chosenDifficulty.HasValue ? chosenDifficulty.Value :
                StorySession.AutomaticDifficulty(library.StoryClearCount(selected));
            var enemy = audition.Deck(difficulty);
            if(!enemy || enemy.classes == null || !enemy.classes.Any(c => c && c.identity == audition.opponent) || enemy.cards == null || enemy.cards.Count == 0 || enemy.cards.Any(c => !c)){
                string message = "auditions (" + audition.opponent + ") > " + difficulty + " Deck: " +
                    (!enemy ? "missing DeckData asset." : enemy.classes == null || !enemy.classes.Any(c => c && c.identity == audition.opponent) ?
                    "deck \"" + enemy.name + "\" must include the opponent's class." : "deck \"" + enemy.name + "\" has empty or missing cards.");
                DeckUI.Text(status, message); StoryDiagnostics.Report(selected, new[] { message }); return;
            }

            if(!audition.firstClearReward || library.Catalog.Card(audition.firstClearReward.id) != audition.firstClearReward){
                string message = "auditions (" + audition.opponent + ") > firstClearReward: " +
                    (!audition.firstClearReward ? "missing CardData asset." : "\"" + audition.firstClearReward.name + "\" is not registered in DeckCatalog.cards.");
                DeckUI.Text(status, message); StoryDiagnostics.Report(selected, new[] { message }); return;
            }

            if(!SceneAvailable(gameScene)) return;
            Action launch = () => {
                Remember(); PuzzleLaunch.Clear(); StorySession.PlayerDeck = deck.Copy(); StorySession.EnemyDeck = enemy;
                StorySession.Catalog = library.Catalog; StorySession.PendingBattle = true; StorySession.PlayAfterBattle = false;
                SceneManager.LoadScene(gameScene);
            };
            if(HasDialogue(audition.before)) PlayDialogue(audition.before, true, launch);
            else launch();
        }

        public void BackToMain(){
            if(SceneAvailable(mainScene)){
                StorySession.Clear();
                SceneManager.LoadScene(mainScene);
            }
        }
    }
}
