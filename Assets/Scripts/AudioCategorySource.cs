using UnityEngine;

namespace VocaloidTCG
{
    [DisallowMultipleComponent, RequireComponent(typeof(AudioSource))]
    public sealed class AudioCategorySource : MonoBehaviour
    {
        public AudioCategory category = AudioCategory.Effects;
        private AudioSource source;
        private float baseVolume;
        private bool baseMute;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            baseVolume = source.volume;
            baseMute = source.mute;
        }

        private void OnEnable() { GameAudioSettings.Changed += Apply; Apply(); }
        private void OnDisable()
        {
            GameAudioSettings.Changed -= Apply;
            if (source) { source.volume = baseVolume; source.mute = baseMute; }
        }

        public void SetCategory(AudioCategory value) { category = value; Apply(); }

        private void Apply()
        {
            if (!source) return;
            source.volume = baseVolume * GameAudioSettings.Volume(category);
            source.mute = baseMute || GameAudioSettings.IsMuted(category);
        }
    }
}
