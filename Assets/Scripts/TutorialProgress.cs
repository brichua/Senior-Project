using UnityEngine;

namespace VocaloidTCG
{
    public static class TutorialProgress
    {
        private const string Key = "VocaloidTCG.TutorialCompleted";
        public static bool Completed => PlayerPrefs.GetInt(Key, 0) != 0;

        public static void SetCompleted(bool completed = true){
            PlayerPrefs.SetInt(Key, completed ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
