using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace VocaloidTCG
{
    public sealed class MainMenu : MonoBehaviour
    {
        public DeckCatalog catalog;
        [FormerlySerializedAs("playButton")]
        public Button storyButton;
        [Tooltip("Unlocked after the tutorial or with Debug Unlock Buttons enabled; opens the lobby.")]
        public Button multiplayerButton;
        public Button decksButton, puzzlesButton;
        public Button settingsButton;
        [FormerlySerializedAs("gameScene")]
        public string storyScene = "Story";
        public string lobbyScene = "Multiplayer";
        [Tooltip("Gameplay scene used for online matches.")]
        public string multiplayerGameScene = "Game";
        public string deckScene = "Edit Deck";
        public string puzzleScene = "Puzzles";
        public string settingsScene = "Settings";
        public TMP_Text status;

        [Header("Tutorial access")]
        public bool tutorialCompleted;
        [Tooltip("Allow all menu buttons regardless of tutorial progress. Does not save tutorial completion.")]
        public bool debugUnlockButtons;

        [Header("Background music")]
        public AudioSource backgroundMusic;
        public TMP_Text musicTimeText;
        public TMP_Text musicTitleText;
        public Slider musicProgress;
        public bool loopMusic = true;

        private bool? previousAccess;
        private bool? previousTutorialCompletion;
        private bool HasCompletedTutorial => tutorialCompleted || TutorialProgress.Completed;
        private bool CanAccessModes => tutorialCompleted || TutorialProgress.Completed || debugUnlockButtons;
        
        private void Awake()
        {
            if (!multiplayerButton)
                multiplayerButton = FindButton("Multiplayer Button");
            if(storyButton) storyButton.onClick.AddListener(OpenStory);
            if(multiplayerButton) multiplayerButton.onClick.AddListener(OpenMultiplayer);
            if(decksButton) decksButton.onClick.AddListener(OpenDeckEditor);
            if(puzzlesButton) puzzlesButton.onClick.AddListener(OpenPuzzles);
            if(settingsButton) settingsButton.onClick.AddListener(OpenSettings);
            if (backgroundMusic)
            {
                backgroundMusic.loop = loopMusic;
                var channel = backgroundMusic.GetComponent<AudioCategorySource>();
                if (!channel) channel = backgroundMusic.gameObject.AddComponent<AudioCategorySource>();
                channel.SetCategory(AudioCategory.Music);
            }
            if (musicProgress)
            {
                musicProgress.minValue = 0f;
                musicProgress.maxValue = 1f;
                musicProgress.wholeNumbers = false;
                musicProgress.interactable = false;
            }
            try{
                DeckLibrary.Get(catalog);
            }
            catch(Exception ex){
                DeckUI.Text(status, ex.Message);
            }
        }

        private void Start() { RefreshAccess(); UpdateMusicProgress(); }

        private void Update()
        {
            if (previousAccess != CanAccessModes || previousTutorialCompletion != HasCompletedTutorial) RefreshAccess();
            UpdateMusicProgress();
        }

        public void RefreshAccess()
        {
            bool unlocked = CanAccessModes;
            SetLocked(storyButton, false);
            SetLocked(multiplayerButton, !unlocked);
            SetLocked(decksButton, !unlocked);
            SetLocked(puzzlesButton, !unlocked);
            previousAccess = unlocked;
            previousTutorialCompletion = HasCompletedTutorial;
        }

        private static void SetLocked(Button button, bool locked)
        {
            if (!button) return;
            var visual = button.GetComponent<MenuButtonVisual>();
            if (visual) visual.SetLocked(locked);
            else button.interactable = !locked;
        }

        public void MarkTutorialCompleted() { SetTutorialCompleted(true); }

        public void SetTutorialCompleted(bool completed)
        {
            tutorialCompleted = completed;
            TutorialProgress.SetCompleted(completed);
            RefreshAccess();
        }

        private bool CheckAccess()
        {
            if (CanAccessModes) return true;
            DeckUI.Text(status, "Complete the tutorial to unlock this mode.");
            return false;
        }

        private void UpdateMusicProgress()
        {
            AudioClip clip = backgroundMusic ? backgroundMusic.clip : null;
            float duration = clip ? clip.length : 0f;
            float elapsed = clip ? Mathf.Clamp(backgroundMusic.time, 0f, duration) : 0f;
            if (musicTimeText) musicTimeText.text = FormatTime(elapsed) + " / " + FormatTime(duration);
            if (musicProgress) musicProgress.SetValueWithoutNotify(duration > 0f ? elapsed / duration : 0f);
            if (musicTitleText) musicTitleText.text = clip ? clip.name : "";
        }

        private static string FormatTime(float time)
        {
            int seconds = Mathf.FloorToInt(time);
            return (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
        }

        public void OpenStory(){
            try{
                var library = DeckLibrary.Get(catalog); string error;
                if(!DeckRules.Validate(library.Selected, library.Catalog, library.Owned, out error)) { DeckUI.Text(status, error); return; }
                PuzzleLaunch.Clear();
                StorySession.Clear();
                SceneManager.LoadScene(storyScene);
            }catch(Exception ex){
                DeckUI.Text(status, ex.Message); Debug.LogError(ex.Message, this);
            }
        }

        private Button FindButton(string objectName)
        {
            Transform searchRoot = transform.root;
            foreach (Button candidate in searchRoot.GetComponentsInChildren<Button>(true))
                if (candidate.name == objectName)
                    return candidate;
            return null;
        }

        public void Play(){
            OpenStory();
        }
        
        public void OpenMultiplayer()
        {
            if (!CanAccessModes)
            {
                DeckUI.Text(status, "Complete the tutorial to unlock multiplayer.");
                return;
            }
            try { MultiplayerSession.Open(catalog, SceneManager.GetActiveScene().name, deckScene, lobbyScene).GameScene = multiplayerGameScene; }
            catch(Exception ex) { DeckUI.Text(status, ex.Message); }
        }
        
        public void OpenDeckEditor(){
            if (CheckAccess()) OpenScene(deckScene);
        }
        
        public void OpenPuzzles(){
            if (CheckAccess()) OpenScene(puzzleScene);
        }
        
        public void OpenSettings(){
            OpenScene(settingsScene);
        }
        
        private void OpenScene(string scene){
            try{
                if(!Application.CanStreamedLevelBeLoaded(scene)) throw new InvalidOperationException("Add \"" + scene + "\" to the build's scene list.");
                SceneManager.LoadScene(scene);
            }catch(Exception ex){
                DeckUI.Text(status, ex.Message);
                Debug.LogError(ex.Message, this);
            }
        }
        
        private void OnDestroy(){
            if(storyButton) storyButton.onClick.RemoveListener(OpenStory);
            if(multiplayerButton) multiplayerButton.onClick.RemoveListener(OpenMultiplayer);
            if(decksButton) decksButton.onClick.RemoveListener(OpenDeckEditor);
            if(puzzlesButton) puzzlesButton.onClick.RemoveListener(OpenPuzzles);
            if(settingsButton) settingsButton.onClick.RemoveListener(OpenSettings);
        }
    }
}
