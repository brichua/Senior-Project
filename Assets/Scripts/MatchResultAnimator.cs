using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VocaloidTCG.BoardUI
{
    public sealed class MatchResultAnimator : MonoBehaviour
    {
        public string defaultReturnScene = "Story", puzzleScene = "Puzzles";
        [Min(0.1f)] public float entranceSeconds = 0.9f;
        public bool showRestartOnDefeat = true;
        public TMP_FontAsset font;
        [Header("Screen effects")]
        public ConcertEffectSettings resultEffects = new ConcertEffectSettings { particleCount = 85 };
        public Color victoryColor = new Color(207f / 255f, 211f / 255f, 1f);
        public Color defeatColor = new Color(166f / 255f, 178f / 255f, 242f / 255f);
        [Tooltip("Optional one-shot result audio, using the Effects volume setting.")]
        public AudioClip victoryStinger, defeatStinger;
        private BoardUIController board;
        private GameplayBoardBridge bridge;
        private BoardSnapshot shown;
        private RectTransform root, portrait, content;
        private CanvasGroup contentGroup;
        private TMP_Text status;
        private Button back, retry, save;
        private bool won, lost;
        private float time;
        private Color accent, classColor;
        private string navigationError;

        public bool IsAvailable => isActiveAndEnabled && board && board.isActiveAndEnabled;
        public void Initialize(BoardUIController owner){
            board = owner; bridge = board.game as GameplayBoardBridge;
            if(bridge) bridge.resultPresenter = this;
        }

        public void Observe(BoardSnapshot state){
            if(!IsAvailable || !bridge || state == null || state.phase != RoundPhase.Finished){ Cancel(); return; }
            if(shown == state && root){ RefreshActions(); return; }
            Cancel(); shown = state;
            won = bridge.IsPuzzle ? bridge.PuzzleSucceeded : state.winnerId == state.localPlayerId;
            lost = bridge.IsPuzzle ? !bridge.PuzzleSucceeded : state.winnerId >= 0 && !won;
            accent = won ? victoryColor : defeatColor;
            var cls = board.setup.PlayerClass;
            classColor = cls ? cls.color : board.setup.player.textColor;
            Build(); RefreshActions();
        }

        private void Update(){
            if(!root) return;
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / Mathf.Max(0.1f, entranceSeconds));
            float ease = 1 - Mathf.Pow(1 - t, 3);
            portrait.anchoredPosition = new Vector2(0, Mathf.Lerp(20, 100, ease));
            portrait.localScale = Vector3.one * Mathf.Lerp(0.9f, 1, ease);
            content.anchoredPosition = new Vector2(0, Mathf.Lerp(-35, 0, ease));
            contentGroup.alpha = Mathf.SmoothStep(0.1f, 1, t);
            contentGroup.interactable = t >= 1;
        }

        private string SaveError => bridge.IsPuzzle ? bridge.PuzzleSaveError : bridge.StorySaveError;
        private void RefreshActions(){
            bool unsaved = !string.IsNullOrEmpty(SaveError);
            back.interactable = !unsaved; retry.interactable = !unsaved;
            retry.gameObject.SetActive(lost && showRestartOnDefeat && !unsaved);
            save.gameObject.SetActive(unsaved);
            status.text = unsaved ? "Your reward could not be saved.\n" + SaveError : navigationError ?? "";
        }

        private void Restart(){
            if(!lost || !string.IsNullOrEmpty(SaveError)) return;
            if(board.pausePanel) board.pausePanel.Close();
            bridge.StartMatch();
        }

        private void Return(){
            if(!string.IsNullOrEmpty(SaveError)) return;
            if(bridge.IsStory){
                if(!bridge.ReturnToStory()) { navigationError = "Unable to return to the story scene. Check its build settings."; RefreshActions(); }
                return;
            }
            string destination = bridge.IsPuzzle ?
                (!string.IsNullOrEmpty(bridge.PuzzleReturnScene) ? bridge.PuzzleReturnScene : puzzleScene) : defaultReturnScene;
            if(!Application.CanStreamedLevelBeLoaded(destination)){
                navigationError = "Add the return scene to the build scene list: " + destination;
                RefreshActions(); return;
            }
            bridge.SetLocalPause(false); SceneManager.LoadScene(destination);
        }

        private void RetrySave(){
            if(bridge.IsPuzzle) bridge.SavePuzzleReward();
            else bridge.RetryStorySave();
            RefreshActions();
        }

        private void Build(){
            var obj = new GameObject("Concert result", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            obj.transform.SetParent(transform, false); root = (RectTransform)obj.transform;
            var canvas = obj.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30001;
            var scaler = obj.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            ConcertBackdrop.Create(root, board);
            var cls = board.setup.PlayerClass;
            Sprite art = cls ? (cls.fullPlayerImage ? cls.fullPlayerImage : cls.playerArt ? cls.playerArt : cls.classIcon) : board.setup.player.avatar;
            portrait = Image(root, "Full player art", new Vector2(0, 20), new Vector2(1250, 970), art, art ? Color.white : Color.clear).rectTransform;
            portrait.GetComponent<Image>().preserveAspect = true;
            var particles = ConcertEffects.Create(root, board, classColor, accent,
                won ? ConcertEffectStyle.Victory : ConcertEffectStyle.Finish, resultEffects);
            if(won){
                particles.EmitBurst(new Vector2(-440, 80), classColor);
                particles.EmitBurst(new Vector2(440, 80), accent);
            }
            content = Rect(root, "Result and actions", Vector2.zero, new Vector2(1400, 1000));
            contentGroup = content.gameObject.AddComponent<CanvasGroup>(); contentGroup.alpha = 0; contentGroup.interactable = false;
            string title = won ? "VICTORY" : lost ? "DEFEAT" : "TIE";
            var titleText = Text(content, title, new Vector2(0, -175), new Vector2(1360, 200), 150, Color.white);
            titleText.characterSpacing = 8; titleText.enableVertexGradient = true;
            titleText.colorGradient = new VertexGradient(Color.white, Color.white, classColor, accent);
            var glow = titleText.gameObject.AddComponent<Shadow>(); glow.effectColor = new Color(classColor.r, classColor.g, classColor.b, 0.65f); glow.effectDistance = new Vector2(3, -3);
            back = Button(content, "RETURN", new Vector2(0, -345), Return);
            retry = Button(content, bridge.IsPuzzle ? "RETRY" : "RESTART", new Vector2(0, -425), Restart);
            save = Button(content, "RETRY SAVE", new Vector2(0, -425), RetrySave);
            status = Text(content, "", new Vector2(0, 425), new Vector2(1200, 130), 23, Color.white);
            var clip = won ? victoryStinger : defeatStinger;
            if(clip){
                var audio = obj.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = 0;
                obj.AddComponent<AudioCategorySource>().SetCategory(AudioCategory.Effects);
                audio.PlayOneShot(clip);
            }
        }

        private Button Button(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction action){
            var button = StoryUIElements.Button(parent, label, position, action);
            button.GetComponent<RectTransform>().sizeDelta = new Vector2(420, 65);
            ConcertBackdrop.StyleButton(button, board.setup, classColor);
            var text = button.GetComponentInChildren<TMP_Text>();
            if(font) text.font = font;
            else if(board.phaseAnimator && board.phaseAnimator.font) text.font = board.phaseAnimator.font;
            return button;
        }
        private static RectTransform Rect(Transform parent, string label, Vector2 position, Vector2 size){
            var rect = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f; rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static Image Image(Transform parent, string label, Vector2 position, Vector2 size, Sprite sprite, Color color){
            var image = Rect(parent, label, position, size).gameObject.AddComponent<Image>();
            image.sprite = sprite; image.color = color; image.raycastTarget = false; return image;
        }
        private TMP_Text Text(Transform parent, string value, Vector2 position, Vector2 size, float fontSize, Color color){
            var text = Rect(parent, value, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            if(font) text.font = font;
            else if(board.phaseAnimator && board.phaseAnimator.font) text.font = board.phaseAnimator.font;
            text.text = value; text.fontSize = fontSize; text.alignment = TextAlignmentOptions.Center; text.color = color; text.raycastTarget = false; return text;
        }
        public void Cancel(){
            if(root){ root.gameObject.SetActive(false); Destroy(root.gameObject); }
            root = null; shown = null; time = 0; navigationError = null;
        }
        private void OnDisable(){ Cancel(); }
    }
}
