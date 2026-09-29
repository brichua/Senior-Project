using System;
using UnityEngine;

namespace VocaloidTCG
{
    public enum AudioCategory { Effects, Voices, Music }

    public static class GameAudioSettings
    {
        private const string Prefix = "VocaloidTCG.Audio.";
        private static readonly float[] volumes = new float[3];
        private static readonly bool[] muted = new bool[3];
        private static bool loaded;
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { loaded = false; Changed = null; }

        private static void Load()
        {
            if (loaded) return;
            for (int i = 0; i < volumes.Length; i++)
            {
                string key = Prefix + (AudioCategory)i;
                volumes[i] = Mathf.Clamp01(PlayerPrefs.GetFloat(key, 1f));
                muted[i] = PlayerPrefs.GetInt(key + ".Muted", 0) != 0;
            }
            loaded = true;
        }

        public static float Volume(AudioCategory category) { Load(); return volumes[(int)category]; }
        public static bool IsMuted(AudioCategory category) { Load(); return muted[(int)category]; }
        public static float EffectiveVolume(AudioCategory category) { return IsMuted(category) ? 0f : Volume(category); }

        public static void SetVolume(AudioCategory category, float value)
        {
            Load();
            value = Mathf.Clamp01(value);
            if (volumes[(int)category] == value) return;
            volumes[(int)category] = value;
            Changed?.Invoke();
        }

        public static void ToggleMute(AudioCategory category)
        {
            Load();
            muted[(int)category] = !muted[(int)category];
            Changed?.Invoke();
        }

        public static void ResetToDefaults()
        {
            Load();
            for (int i = 0; i < volumes.Length; i++) { volumes[i] = 1f; muted[i] = false; }
            Changed?.Invoke();
        }

        public static void Save()
        {
            Load();
            for (int i = 0; i < volumes.Length; i++)
            {
                string key = Prefix + (AudioCategory)i;
                PlayerPrefs.SetFloat(key, volumes[i]);
                PlayerPrefs.SetInt(key + ".Muted", muted[i] ? 1 : 0);
            }
            PlayerPrefs.Save();
        }
    }
}
