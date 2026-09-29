using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VocaloidTCG
{
    public sealed class SettingsUI : MonoBehaviour
    {
        [Serializable]
        public sealed class ChannelControls
        {
            public Slider volume;
            public Button muteButton;
            [Tooltip("Image that displays the current mute state. Defaults to the button's Image.")]
            public Image muteImage;
            public Sprite mutedSprite, unmutedSprite;

            internal void Refresh(AudioCategory category)
            {
                if (volume) volume.SetValueWithoutNotify(GameAudioSettings.Volume(category));
                Image image = muteImage ? muteImage : (muteButton ? muteButton.image : null);
                Sprite sprite = GameAudioSettings.IsMuted(category) ? mutedSprite : unmutedSprite;
                if (image && sprite) image.sprite = sprite;
            }
        }

        public string mainScene = "Main";
        public Button backButton, resetButton, saveButton;
        public ChannelControls sfx = new ChannelControls();
        public ChannelControls voicelines = new ChannelControls();
        public ChannelControls music = new ChannelControls();
        public TMP_Text status;

        private void Awake()
        {
            Bind(sfx, SetSfx, ToggleSfx);
            Bind(voicelines, SetVoices, ToggleVoices);
            Bind(music, SetMusic, ToggleMusic);
            if (backButton) backButton.onClick.AddListener(Back);
            if (resetButton) resetButton.onClick.AddListener(ResetToDefaults);
            if (saveButton) saveButton.onClick.AddListener(SaveSettings);
        }

        private void OnEnable() { GameAudioSettings.Changed += Refresh; Refresh(); }
        private void OnDisable() { GameAudioSettings.Changed -= Refresh; }

        private static void Bind(ChannelControls controls, UnityEngine.Events.UnityAction<float> volume, UnityEngine.Events.UnityAction mute)
        {
            if (controls.volume)
            {
                controls.volume.minValue = 0f;
                controls.volume.maxValue = 1f;
                controls.volume.wholeNumbers = false;
                controls.volume.onValueChanged.AddListener(volume);
            }
            if (controls.muteButton)
            {
                if ((!controls.muteImage || controls.muteImage == controls.muteButton.image) &&
                    controls.muteButton.transition == Selectable.Transition.SpriteSwap)
                    controls.muteButton.transition = Selectable.Transition.None;
                controls.muteButton.onClick.AddListener(mute);
            }
        }

        private static void Unbind(ChannelControls controls, UnityEngine.Events.UnityAction<float> volume, UnityEngine.Events.UnityAction mute)
        {
            if (controls.volume) controls.volume.onValueChanged.RemoveListener(volume);
            if (controls.muteButton) controls.muteButton.onClick.RemoveListener(mute);
        }

        private void Refresh()
        {
            sfx.Refresh(AudioCategory.Effects);
            voicelines.Refresh(AudioCategory.Voices);
            music.Refresh(AudioCategory.Music);
            if (status) status.text = "";
        }

        public void SetSfx(float value) { GameAudioSettings.SetVolume(AudioCategory.Effects, value); }
        public void SetVoices(float value) { GameAudioSettings.SetVolume(AudioCategory.Voices, value); }
        public void SetMusic(float value) { GameAudioSettings.SetVolume(AudioCategory.Music, value); }
        public void ToggleSfx() { GameAudioSettings.ToggleMute(AudioCategory.Effects); }
        public void ToggleVoices() { GameAudioSettings.ToggleMute(AudioCategory.Voices); }
        public void ToggleMusic() { GameAudioSettings.ToggleMute(AudioCategory.Music); }
        public void ResetToDefaults()
        {
            GameAudioSettings.ResetToDefaults();
            if (status) status.text = "Defaults restored. Press Save Settings to keep them.";
        }
        public void SaveSettings()
        {
            GameAudioSettings.Save();
            if (status) status.text = "Settings saved.";
        }

        public void Back()
        {
            if (!Application.CanStreamedLevelBeLoaded(mainScene))
            {
                string message = "Add \"" + mainScene + "\" to the build's scene list.";
                if (status) status.text = message;
                Debug.LogError(message, this);
                return;
            }
            SceneManager.LoadScene(mainScene);
        }

        private void OnDestroy()
        {
            Unbind(sfx, SetSfx, ToggleSfx);
            Unbind(voicelines, SetVoices, ToggleVoices);
            Unbind(music, SetMusic, ToggleMusic);
            if (backButton) backButton.onClick.RemoveListener(Back);
            if (resetButton) resetButton.onClick.RemoveListener(ResetToDefaults);
            if (saveButton) saveButton.onClick.RemoveListener(SaveSettings);
        }
    }
}
