using UnityEngine;
using UnityEngine.SceneManagement;

namespace VocaloidTCG
{
    public static class StorySession
    {
        public static StoryData Story;
        public static CharacterClass Opponent;
        public static string DeckId;
        public static StoryDifficulty? Difficulty;
        public static string ReturnScene;
        public static bool EditingDeck;
        public static bool PendingBattle;
        public static DeckRecord PlayerDeck;
        public static DeckData EnemyDeck;
        public static DeckCatalog Catalog;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear(){
            Story = null; DeckId = null; Difficulty = null; ReturnScene = null;
            EditingDeck = PendingBattle = false; PlayerDeck = null; EnemyDeck = null; Catalog = null;
        }

        public static bool ReturnFromEditor(){
            if(!EditingDeck || !Story) return false;
            if(!Application.CanStreamedLevelBeLoaded(ReturnScene)){
                Debug.LogError("Add the story scene to the build scene list: " + ReturnScene);
                return true;
            }

            SceneManager.LoadScene(ReturnScene);
            EditingDeck = false;
            return true;
        }

        public static StoryDifficulty AutomaticDifficulty(int cleared) => cleared >= 4 ? StoryDifficulty.Hard :
            cleared >= 2 ? StoryDifficulty.Medium : StoryDifficulty.Easy;
    }
}
