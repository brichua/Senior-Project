using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace VocaloidTCG.BoardUI
{
    public sealed class PausePanel : MonoBehaviour
    {
        public GameObject overlay;
        public Button resume;
        public Slider effects, voices, music;
        public AudioMixer mixer;
        public string effectsParameter = "EffectsVolume", voicesParameter = "VoiceVolume", musicParameter = "MusicVolume";
        public bool IsOpen { get; private set; }
        private BoardGameBridge game;
        private BoardUIController board;
        private bool pausedGame;

        public void Initialize(BoardUIController owner, BoardGameBridge bridge){
            board = owner; game = bridge; Close();
        }
        
        private void Start(){
            Setup(effects, "Effects", SetEffects);
            Setup(voices, "Voices", SetVoices);
            Setup(music, "Music", SetMusic);
            if(resume) resume.onClick.AddListener(Close);
        }

        private void Setup(Slider slider, string key, UnityEngine.Events.UnityAction<float> changed){
            if(!slider) return;
            slider.minValue = 0; slider.maxValue = 1; slider.wholeNumbers = false;
            slider.SetValueWithoutNotify(PlayerPrefs.GetFloat("VocaloidTCG.Audio." + key, 1));
            changed(slider.value); slider.onValueChanged.AddListener(changed);
        }

        public void Open(){
            if(IsOpen || !overlay || !game || game.Snapshot == null) return;
            IsOpen = true; if(board) board.CancelDrag(); overlay.SetActive(true);
            if(board && board.pause) board.pause.gameObject.SetActive(false);
            pausedGame = !game.Snapshot.multiplayer;
            if(pausedGame) game.SetLocalPause(true);
        }

        public void Close(){
            IsOpen = false; if(overlay) overlay.SetActive(false);
            if(board && board.pause) board.pause.gameObject.SetActive(true);
            if(pausedGame && game) game.SetLocalPause(false);
            pausedGame = false; PlayerPrefs.Save();
        }

        private void Apply(string parameter, string key, float linear){
            linear = Mathf.Clamp01(linear);
            if(mixer && !mixer.SetFloat(parameter, linear <= 0.0001f ? -80f : 20f * Mathf.Log10(linear)))
                Debug.LogWarning("Expose an AudioMixer parameter named " + parameter, this);
            PlayerPrefs.SetFloat("VocaloidTCG.Audio." + key, linear);
        }

        private void SetEffects(float value){
            Apply(effectsParameter, "Effects", value);
        }

        private void SetVoices(float value){
            Apply(voicesParameter, "Voices", value);
        }

        private void SetMusic(float value){
            Apply(musicParameter, "Music", value);
        }

        private void OnDisable(){
            Close();
        }

        private void OnDestroy(){
            if(resume) resume.onClick.RemoveListener(Close);
            if(effects) effects.onValueChanged.RemoveListener(SetEffects);
            if(voices) voices.onValueChanged.RemoveListener(SetVoices);
            if(music) music.onValueChanged.RemoveListener(SetMusic);
        }
    }
}
