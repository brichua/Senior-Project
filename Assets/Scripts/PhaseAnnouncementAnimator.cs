using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VocaloidTCG.BoardUI
{
    public sealed class PhaseAnnouncementAnimator : MonoBehaviour
    {
        public Sprite leftLightstick, rightLightstick;
        public TMP_FontAsset font;
        [Min(0.05f)] public float enterSeconds = 0.45f, exitSeconds = 0.45f;
        [Min(0)] public float holdSeconds = 1.15f;
        [Range(0, 1)] public float overlayOpacity = 0.72f;
        public bool IsPlaying { get; private set; }

        private BoardUIController board;
        private GameplayBoardBridge bridge;
        private BoardSnapshot observed;
        private RoundPhase phase;
        private int round;
        private float elapsed;
        private CanvasGroup group;
        private Image shade, line;
        private RectTransform titleRoot;
        private TMP_Text title, subtitle;
        private readonly Image[] sticks = new Image[2];
        private readonly RectTransform[] motes = new RectTransform[28];
        private readonly CanvasGroup[] moteGroups = new CanvasGroup[28];
        private readonly Image[] sparkArms = new Image[28];
        private Color accent;

        public void Initialize(BoardUIController owner){
            board = owner; bridge = board.game as GameplayBoardBridge;
            if(!group) Build();
        }

        public void Observe(BoardSnapshot state){
            if(!isActiveAndEnabled || state == null) { Cancel(); return; }
            if(observed == state && phase == state.phase && round == state.roundNumber) return;
            Cancel();
            
            observed = state; phase = state.phase; round = state.roundNumber;
            if(phase != RoundPhase.Preparation && phase != RoundPhase.Performance && phase != RoundPhase.EndRound) return;
            accent = phase == RoundPhase.EndRound ? board.endRoundAnnouncementColor : StarterColor(state);
            title.text = phase == RoundPhase.EndRound ? "END OF ROUND" : phase.ToString().ToUpperInvariant();
            subtitle.text = "round " + round;
            subtitle.color = accent; line.color = accent;
            
            for(int i = 0; i < motes.Length; i++){
                motes[i].GetComponent<Image>().color = accent;
                sparkArms[i].color = accent;
                sparkArms[i].gameObject.SetActive(phase == RoundPhase.Performance);
            }
            
            for(int i = 0; i < sticks.Length; i++)
                sticks[i].gameObject.SetActive(phase == RoundPhase.EndRound && sticks[i].sprite);
            
            elapsed = 0; IsPlaying = true;
            if(bridge) bridge.SetPhaseAnimationPlaying(true);
            group.gameObject.SetActive(true); group.alpha = 0;
            board.ClearHandHover(); board.CancelDrag();
        }

        private void Update(){
            if(!IsPlaying || !board) return;
            if((bridge && (bridge.IsPaused || bridge.DrawAnimationPlaying)) ||
                (board.pausePanel && board.pausePanel.IsOpen)) { group.alpha = 0; return; }
            
            elapsed += Time.unscaledDeltaTime;
            float enter = Mathf.Max(0.05f, enterSeconds), exit = Mathf.Max(0.05f, exitSeconds);
            float total = enter + Mathf.Max(0, holdSeconds) + exit;
            
            if(elapsed >= total){ Cancel(); return; }
            float inT = Mathf.Clamp01(elapsed / enter);
            float outT = Mathf.Clamp01((elapsed - total + exit) / exit);
            float easeIn = 1 - Mathf.Pow(1 - inT, 3);
            float easeOut = outT * outT * (3 - 2 * outT);
            group.alpha = Mathf.SmoothStep(0, 1, inT) * (1 - easeOut);
            shade.color = new Color(0.015f, 0.02f, 0.055f, overlayOpacity);
            float punch = phase == RoundPhase.Performance ? Mathf.Sin(inT * Mathf.PI) * 0.08f : 0;
            titleRoot.localScale = Vector3.one * (Mathf.Lerp(0.87f, 1, easeIn) + punch + easeOut * 0.035f);
            titleRoot.anchoredPosition = new Vector2(0, (1 - easeIn) * -28 + easeOut * 20);
            line.rectTransform.localScale = new Vector3(easeIn * (1 - easeOut), 1, 1);
            
            for(int i = 0; i < motes.Length; i++) AnimateMote(i, easeIn, easeOut);
            for(int i = 0; i < sticks.Length; i++){
                float side = i == 0 ? -1 : 1;
                float textEdge = title.preferredWidth * titleRoot.localScale.x * 0.5f;
                float besideTitle = textEdge + 115;
                sticks[i].rectTransform.anchoredPosition = new Vector2(side * Mathf.Lerp(besideTitle + 30, besideTitle, easeIn), -15 - easeOut * 35);
                float tilt = 24 * easeIn + Mathf.Sin(elapsed * 3) * 3 + easeOut * 12;
                sticks[i].rectTransform.localRotation = Quaternion.Euler(0, 0, -side * tilt);
            }
        }

        private Color StarterColor(BoardSnapshot state){
            if(!board.setup) return Color.white;
            bool localStarts = state.roundStarterId == state.localPlayerId;
            var characterClass = localStarts ? board.setup.PlayerClass : board.setup.EnemyClass;
            if(characterClass) return characterClass.color;
            var art = localStarts ? board.setup.player : board.setup.enemy;
            return art != null ? art.textColor : Color.white;
        }

        private void AnimateMote(int i, float enter, float exit){
            float seed = i * 2.399963f;
            bool spark = phase == RoundPhase.Performance;
            float pulse = Mathf.Repeat(elapsed * (spark ? 1.3f : 0.3f) + i * 0.137f, 1);
            float radius = spark ? 1 + (1 - Mathf.Pow(1 - pulse, 4)) * 0.12f : 1;
            float x = Mathf.Cos(seed) * (spark ? 355 : 560) * radius;
            float y = Mathf.Sin(seed) * (spark ? 150 : 255) + (spark ? 0 : elapsed * (12 + i % 5 * 4));
            motes[i].anchoredPosition = new Vector2(x, y);
            float size = spark ? (1 - pulse) * (12 + i % 4 * 3) : 2 + i % 4;
            motes[i].sizeDelta = spark ? new Vector2(size * 0.22f, size) : Vector2.one * size;
            sparkArms[i].rectTransform.sizeDelta = new Vector2(size, size * 0.22f);
            motes[i].localRotation = Quaternion.Euler(0, 0, spark ? 0 : 45);
            moteGroups[i].alpha = enter * (1 - exit) * (spark ? 1 - pulse : 0.35f + 0.4f * Mathf.Sin(pulse * Mathf.PI));
        }

        private void Build(){
            var layer = new GameObject("Phase announcement", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            layer.transform.SetParent(transform, false);
            var canvas = layer.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            var scaler = layer.GetComponent<CanvasScaler>();
            
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            group = layer.GetComponent<CanvasGroup>(); group.interactable = false; group.blocksRaycasts = false;
            shade = MakeImage(layer.transform, "Dark overlay", Vector2.zero);
            shade.rectTransform.anchorMin = Vector2.zero; shade.rectTransform.anchorMax = Vector2.one;
            
            for(int i = 0; i < motes.Length; i++){
                var mote = MakeImage(layer.transform, "Light dust " + i, Vector2.one * 4);
                motes[i] = mote.rectTransform;
                moteGroups[i] = mote.gameObject.AddComponent<CanvasGroup>();
                sparkArms[i] = MakeImage(motes[i], "Spark cross", Vector2.one);
            }
            
            titleRoot = MakeRect(layer.transform, "Phase text", new Vector2(840, 200));
            title = MakeText(titleRoot, "Phase", 62, new Vector2(0, 28), new Vector2(840, 100));
            subtitle = MakeText(titleRoot, "Round", 30, new Vector2(0, -49), new Vector2(840, 50));
            line = MakeImage(titleRoot, "Accent line", new Vector2(240, 2));
            line.rectTransform.anchoredPosition = new Vector2(0, -12);
            
            for(int i = 0; i < sticks.Length; i++){
                sticks[i] = MakeImage(layer.transform, i == 0 ? "Left lightstick" : "Right lightstick", new Vector2(125, 235));
                sticks[i].sprite = i == 0 ? leftLightstick : rightLightstick;
                sticks[i].preserveAspect = true;
            }
            layer.SetActive(false);
        }

        private static RectTransform MakeRect(Transform parent, string label, Vector2 size){
            var rect = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.sizeDelta = size; return rect;
        }

        private static Image MakeImage(Transform parent, string label, Vector2 size){
            var image = MakeRect(parent, label, size).gameObject.AddComponent<Image>();
            image.raycastTarget = false; return image;
        }

        private TMP_Text MakeText(Transform parent, string label, float size, Vector2 position, Vector2 bounds){
            var rect = MakeRect(parent, label, bounds); rect.anchoredPosition = position;
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if(font) text.font = font;
            text.fontSize = size; text.alignment = TextAlignmentOptions.Center; text.color = Color.white;
            text.raycastTarget = false; return text;
        }

        public void Cancel(){
            IsPlaying = false;
            if(group) { group.alpha = 0; group.gameObject.SetActive(false); }
            if(bridge) bridge.SetPhaseAnimationPlaying(false);
        }

        private void OnDisable(){ Cancel(); }
    }
}
