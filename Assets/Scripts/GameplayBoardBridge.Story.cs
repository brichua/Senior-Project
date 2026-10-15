using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VocaloidTCG.BoardUI
{
    public sealed partial class GameplayBoardBridge
    {
        private StoryData story;
        private CharacterClass storyOpponent;
        private DeckRecord storyPlayerDeck;
        private string storyReturnScene;
        private string storyLaunchError;
        public bool IsStory => story;
        public string StorySaveError { get; private set; }

        private void ConsumeStoryLaunch(){
            if(!StorySession.PendingBattle) return;
            var issues = new System.Collections.Generic.List<string>();
            StoryDiagnostics.Require(issues, ("StorySession.Story", StorySession.Story),
                ("StorySession.EnemyDeck", StorySession.EnemyDeck), ("StorySession.Catalog", StorySession.Catalog));
            if(StorySession.PlayerDeck == null) issues.Add("StorySession.PlayerDeck: no selected player deck was passed from StoryUI.");
            if(StorySession.Story && StorySession.Story.Audition(StorySession.Opponent) == null)
                issues.Add("StorySession.Opponent: no audition for " + StorySession.Opponent + " on story \"" + StorySession.Story.name + "\".");
            StoryDiagnostics.Scene(issues, "StorySession.ReturnScene", StorySession.ReturnScene);
            storyLaunchError = string.Join("\n", issues);
            StoryDiagnostics.Report(this, issues);
            if(!StorySession.Story){
                StorySession.PendingBattle = false;
                return;
            }

            story = StorySession.Story; storyOpponent = StorySession.Opponent;
            storyPlayerDeck = StorySession.PlayerDeck?.Copy(); storyReturnScene = StorySession.ReturnScene;
            enemyDeck = StorySession.EnemyDeck; deckCatalog = StorySession.Catalog;
            useSelectedDeck = true; puzzle = null; StorySession.PendingBattle = false;
            if(!enemyAI) enemyAI = gameObject.AddComponent<BasicEnemyAITest>();
            enemyAI.game = this; enemyAI.enabled = true;
            var ui = GetComponent<StoryBattleUI>();
            if(!ui) ui = gameObject.AddComponent<StoryBattleUI>();
            ui.bridge = this;
        }

        public void SaveStoryReward(){
            if(!IsStory || state == null || state.phase != RoundPhase.Finished || state.winnerId != state.localPlayerId) return;
            try{
                var library = DeckLibrary.Get(deckCatalog);
                library.CompleteAudition(story, storyOpponent, out var error);
                StorySaveError = error;
            }
            catch(Exception ex){
                StorySaveError = ex.Message;
            }

            if(!string.IsNullOrEmpty(StorySaveError))
                Debug.LogError("[Story Save] " + story.name + " > " + storyOpponent + ": " + StorySaveError + " Use Retry Save on the result panel.", this);
        }

        public void RetryStorySave(){
            SaveStoryReward();
            Publish();
        }

        public bool ReturnToStory(){
            if(!IsStory || !string.IsNullOrEmpty(StorySaveError)) return false;
            if(!Application.CanStreamedLevelBeLoaded(storyReturnScene)){
                Debug.LogError("Add the story scene to the build scene list: " + storyReturnScene, this);
                return false;
            }

            StorySession.PlayAfterBattle = state != null && state.phase == RoundPhase.Finished &&
                state.winnerId == state.localPlayerId && string.IsNullOrEmpty(MatchSetupError);
            SetLocalPause(false); SceneManager.LoadScene(storyReturnScene); return true;
        }
    }
}
