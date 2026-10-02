using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace VocaloidTCG.BoardUI
{
    public sealed class PausePanel : MonoBehaviour
    {
        public GameObject overlay;
        public Button resume;
        [Header("Story pause buttons (scene objects)")]
        public GameObject storyButtons;
        public Button storyRestart, storyReturn;
        [Header("Puzzle pause buttons (scene objects)")]
        public GameObject puzzleButtons;
        public Button puzzleRestart, puzzleReturn;
        [Header("Audio")]
        public Slider effects, voices, music;
        public AudioMixer mixer;
        public string effectsParameter = "EffectsVolume", voicesParameter = "VoiceVolume", musicParameter = "MusicVolume";
        public bool IsOpen { get; private set; }
        private BoardGameBridge game;
        private BoardUIController board;
        private bool pausedGame;
        private bool audioDirty;
        private Button restartMode, returnMode;
        public string puzzleScene = "Puzzles";

        public void Initialize(BoardUIController owner, BoardGameBridge bridge){
            board = owner; game = bridge;
            ConfigureModeActions();
            Close();
        }

        private void ConfigureModeActions(){
            if(restartMode) restartMode.onClick.RemoveListener(RestartMode);
            if(returnMode) returnMode.onClick.RemoveListener(ReturnMode);
            var gameplay = game as GameplayBoardBridge;
            bool story = gameplay && gameplay.IsStory;
            bool puzzle = gameplay && !story && gameplay.IsPuzzle;
            if(storyButtons) storyButtons.SetActive(story);
            if(puzzleButtons) puzzleButtons.SetActive(puzzle);
            restartMode = story ? storyRestart : puzzle ? puzzleRestart : null;
            returnMode = story ? storyReturn : puzzle ? puzzleReturn : null;
            if(restartMode) restartMode.onClick.AddListener(RestartMode);
            if(returnMode) returnMode.onClick.AddListener(ReturnMode);
            if(!gameplay) return;
            if(gameplay.IsPuzzle && !string.IsNullOrEmpty(gameplay.PuzzleReturnScene))
                puzzleScene = gameplay.PuzzleReturnScene;
        }

        private bool CanLeaveMode(){
            var gameplay = game as GameplayBoardBridge;
            return gameplay && string.IsNullOrEmpty(gameplay.IsStory ? gameplay.StorySaveError : gameplay.PuzzleSaveError);
        }

        private void RestartMode(){
            if(!CanLeaveMode()) return;
            Close();
            ((GameplayBoardBridge)game).StartMatch();
        }

        private void ReturnMode(){
            if(!CanLeaveMode()) return;
            var gameplay = (GameplayBoardBridge)game;
            if(gameplay.IsStory){ gameplay.ReturnToStory(); return; }
            if(!Application.CanStreamedLevelBeLoaded(puzzleScene)){
                Debug.LogError("Add the puzzle scene to the build scene list: " + puzzleScene, this);
                return;
            }
            Close();
            SceneManager.LoadScene(puzzleScene);
        }
        
        private void Start(){
            Setup(effects, SetEffects);
            Setup(voices, SetVoices);
            Setup(music, SetMusic);
            RefreshAudio();
            if(resume) resume.onClick.AddListener(Close);
        }

        private void OnEnable() { GameAudioSettings.Changed += RefreshAudio; }

        private void Setup(Slider slider, UnityEngine.Events.UnityAction<float> changed){
            if(!slider) return;
            slider.minValue = 0; slider.maxValue = 1; slider.wholeNumbers = false;
            slider.onValueChanged.AddListener(changed);
        }

        public void Open(){
            if(IsOpen || !overlay || !game || game.Snapshot == null) return;
            ConfigureModeActions();
            IsOpen = true; if(board) board.CancelDrag(); overlay.SetActive(true);
            RefreshAudio();
            if(restartMode) restartMode.interactable = CanLeaveMode();
            if(returnMode) returnMode.interactable = CanLeaveMode();
            if(board && board.pause) board.pause.gameObject.SetActive(false);
            pausedGame = !game.Snapshot.multiplayer;
            if(pausedGame) game.SetLocalPause(true);
        }

        public void Close(){
            IsOpen = false; if(overlay) overlay.SetActive(false);
            if(board && board.pause) board.pause.gameObject.SetActive(true);
            if(pausedGame && game) game.SetLocalPause(false);
            pausedGame = false;
            if (audioDirty) { GameAudioSettings.Save(); audioDirty = false; }
        }

        private void Apply(string parameter, AudioCategory category){
            float linear = GameAudioSettings.EffectiveVolume(category);
            if(mixer && !mixer.SetFloat(parameter, linear <= 0.0001f ? -80f : 20f * Mathf.Log10(linear)))
                Debug.LogWarning("Expose an AudioMixer parameter named " + parameter, this);
        }

        private void RefreshAudio()
        {
            if (effects) effects.SetValueWithoutNotify(GameAudioSettings.Volume(AudioCategory.Effects));
            if (voices) voices.SetValueWithoutNotify(GameAudioSettings.Volume(AudioCategory.Voices));
            if (music) music.SetValueWithoutNotify(GameAudioSettings.Volume(AudioCategory.Music));
            Apply(effectsParameter, AudioCategory.Effects);
            Apply(voicesParameter, AudioCategory.Voices);
            Apply(musicParameter, AudioCategory.Music);
        }

        private void SetEffects(float value){
            audioDirty = true;
            GameAudioSettings.SetVolume(AudioCategory.Effects, value);
        }

        private void SetVoices(float value){
            audioDirty = true;
            GameAudioSettings.SetVolume(AudioCategory.Voices, value);
        }

        private void SetMusic(float value){
            audioDirty = true;
            GameAudioSettings.SetVolume(AudioCategory.Music, value);
        }

        private void OnDisable(){
            GameAudioSettings.Changed -= RefreshAudio;
            Close();
        }

        private void OnDestroy(){
            if(restartMode) restartMode.onClick.RemoveListener(RestartMode);
            if(returnMode) returnMode.onClick.RemoveListener(ReturnMode);
            if(resume) resume.onClick.RemoveListener(Close);
            if(effects) effects.onValueChanged.RemoveListener(SetEffects);
            if(voices) voices.onValueChanged.RemoveListener(SetVoices);
            if(music) music.onValueChanged.RemoveListener(SetMusic);
        }
    }
}
