using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VocaloidTCG.BoardUI;

namespace VocaloidTCG
{
    public sealed class PuzzleResultUI : MonoBehaviour
    {
        public GameplayBoardBridge bridge;
        public GameObject panel;
        public TMP_Text result;
        public Button retry, back, retrySave;
        public string puzzleScene = "Puzzles";
        
        private void Start(){
            if(!bridge) bridge = FindAnyObjectByType<GameplayBoardBridge>();
            if(bridge) bridge.Changed += Refresh;
            if(retry) retry.onClick.AddListener(Retry);
            if(back) back.onClick.AddListener(Back);
            if(retrySave) retrySave.onClick.AddListener(Save);
            Refresh();
        }

        private void OnDestroy(){
            if(bridge) bridge.Changed -= Refresh;
        }
        
        private void Retry(){
            if(bridge) bridge.StartMatch();
        }
        
        private void Save(){
            if(bridge) bridge.SavePuzzleReward();
        }
        
        private void Back(){
            string destination = bridge && !string.IsNullOrEmpty(bridge.PuzzleReturnScene) ? bridge.PuzzleReturnScene : puzzleScene;
            if(!Application.CanStreamedLevelBeLoaded(destination)) { DeckUI.Text(result, "Add the puzzle scene to the build's scene list."); return; }
            SceneManager.LoadScene(destination);
        }
        
        private void Refresh(){
            bool visible = bridge && bridge.IsPuzzle && (bridge.Snapshot != null && bridge.Snapshot.phase == RoundPhase.Finished || !string.IsNullOrEmpty(bridge.MatchSetupError));
            if(bridge && bridge.Snapshot != null && bridge.Snapshot.phase == RoundPhase.Finished &&
                bridge.resultPresenter && bridge.resultPresenter.IsAvailable) visible = false;
            if(panel) panel.SetActive(visible);
            if(!visible) return;
            
            DeckUI.Text(result, !string.IsNullOrEmpty(bridge.MatchSetupError) ? bridge.MatchSetupError : bridge.Snapshot.roundSummary);
            bool unsaved = !string.IsNullOrEmpty(bridge.PuzzleSaveError);
            
            if(retrySave) retrySave.gameObject.SetActive(unsaved);
            if(retry) retry.interactable = !unsaved;
            if(back) back.interactable = !unsaved;
        }
    }
}
