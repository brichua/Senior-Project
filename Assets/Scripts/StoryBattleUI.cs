using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VocaloidTCG.BoardUI;

namespace VocaloidTCG
{
    public sealed class StoryBattleUI : MonoBehaviour
    {
        public GameplayBoardBridge bridge;
        private GameObject panel;
        private TMP_Text message;
        private Button retry, back, save;

        private void Start(){
            if(!bridge || !bridge.IsStory) return;
            var canvasObject = new GameObject("Story battle controls", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            panel = StoryUIElements.Panel(canvas.transform, "Audition result", new Vector2(780, 360));
            message = StoryUIElements.Text(panel.transform, "Result", "", new Vector2(0, 95), new Vector2(720, 120));
            retry = StoryUIElements.Button(panel.transform, "Restart audition", new Vector2(-230, -105), () => { bridge.StartMatch(); });
            back = StoryUIElements.Button(panel.transform, "Back to audition", new Vector2(0, -105), () => bridge.ReturnToStory());
            save = StoryUIElements.Button(panel.transform, "Retry save", new Vector2(230, -105), bridge.RetryStorySave);
            Refresh();
        }

        private void Update(){
            if(panel) Refresh();
        }

        private void Refresh(){
            bool finished = bridge.Snapshot != null && bridge.Snapshot.phase == RoundPhase.Finished;
            bool visible = finished || !string.IsNullOrEmpty(bridge.MatchSetupError);
            if(finished && bridge.resultPresenter && bridge.resultPresenter.IsAvailable) visible = false;
            panel.SetActive(visible);
            if(!visible) return;
            bool failedSave = !string.IsNullOrEmpty(bridge.StorySaveError);
            message.text = !string.IsNullOrEmpty(bridge.MatchSetupError) ? bridge.MatchSetupError :
                failedSave ? "Your win could not be saved.\n" + bridge.StorySaveError :
                bridge.Snapshot.winnerId == bridge.Snapshot.localPlayerId ? "Audition passed!" :
                bridge.Snapshot.winnerId < 0 ? "Draw" : "Audition failed";
            retry.interactable = back.interactable = !failedSave; save.gameObject.SetActive(failedSave);
        }
    }
}
