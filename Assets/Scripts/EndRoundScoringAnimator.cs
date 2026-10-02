using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VocaloidTCG.BoardUI
{
    public sealed class EndRoundScoringAnimator : MonoBehaviour
    {
        [Min(0.1f)] public float soloSeconds = 0.65f, contestSeconds = 1.2f, scoreSeconds = 0.85f;
        [Range(0, 1)] public float contestedDim = 0.7f;
        private BoardUIController board;
        private GameplayBoardBridge bridge;
        private BoardSnapshot snapshot;
        private CanvasGroup group;
        private RectTransform layer;
        private Image shade, glow;
        private TMP_Text score;
        private TileView focus;
        private RectTransform popup;
        private Vector3 popupScale;
        private readonly List<RectTransform> sticks = new List<RectTransform>();
        private readonly List<Quaternion> stickRotations = new List<Quaternion>();
        private readonly Image[] sparks = new Image[12];
        private int currentRow = -1, stage, winner, points;
        private bool contested;
        private float elapsed;
        private Color accent;
        private Vector2 focusPosition, scorePosition;
        private Texture2D glowTexture;
        private Sprite glowSprite;

        public bool IsAvailable => isActiveAndEnabled && board && board.isActiveAndEnabled;

        public void Initialize(BoardUIController owner){
            board = owner; bridge = board.game as GameplayBoardBridge;
            if(bridge) bridge.scoringPresenter = this;
            Build();
        }

        private void Update(){
            if(!IsAvailable || !bridge) return;
            if(snapshot != board.State){ Cancel(); snapshot = board.State; }
            if(!bridge.RoundResolutionPending){ Cancel(); return; }
            if(bridge.IsPaused || bridge.PhaseAnimationPlaying || bridge.DrawAnimationPlaying){
                group.alpha = 0; return;
            }
            
            if(currentRow < 0){
                if(bridge.ScoringRow >= 5){ bridge.FinishRoundResolution(); return; }
                var tile = snapshot.tiles[bridge.ScoringRow * 5 + 2];
                if(tile.side0 == null && tile.side1 == null){ bridge.ResolveNextScoringTile(); return; }
                BeginTile(tile);
            }
            
            group.gameObject.SetActive(true); group.alpha = 1;
            elapsed += Time.unscaledDeltaTime;
            if(stage == 0){
                float duration = Mathf.Max(0.1f, contested ? contestSeconds : soloSeconds);
                float t = Mathf.Clamp01(elapsed / duration);
                float swell = Mathf.Sin(t * Mathf.PI);
                shade.color = new Color(0.015f, 0.02f, 0.04f, (contested ? contestedDim : 0.12f) * Mathf.SmoothStep(0, 1, t * 4));
                glow.color = new Color(accent.r, accent.g, accent.b, 0.35f + swell * 0.45f);
                glow.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.15f, Mathf.SmoothStep(0, 1, t));
                if(popup) popup.localScale = popupScale * (1 + 0.35f * swell);
                for(int i = 0; i < sticks.Count; i++)
                    sticks[i].localRotation = stickRotations[i] * Quaternion.Euler(0, 0,
                        (i % 2 == 0 ? -1 : 1) * Mathf.Sin(t * Mathf.PI * 3) * 22 * swell);
                Burst(t);
                if(t >= 1 && bridge.ResolveNextScoringTile()){
                    stage = 1; elapsed = 0;
                    score.text = points > 0 ? "+" + points : contested ? "Tie" : "+0";
                    score.color = accent;
                    scorePosition = points > 0 ? AwardPosition(winner) : focusPosition;
                    score.gameObject.SetActive(true);
                }
            }
            else{
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, scoreSeconds));
                float fade = 1 - Mathf.SmoothStep(0.45f, 1, t);
                shade.color = new Color(0.015f, 0.02f, 0.04f, (contested ? contestedDim : 0.12f) * (1 - t));
                if(focus) focus.GetComponent<CanvasGroup>().alpha = 1 - Mathf.Clamp01(t * 3);
                glow.color = new Color(accent.r, accent.g, accent.b, (1 - t) * 0.4f);
                foreach(var spark in sparks) spark.color = Color.clear;
                score.rectTransform.anchoredPosition = scorePosition + Vector2.up * (t * 60);
                score.rectTransform.localScale = Vector3.one * (1 + 0.22f * Mathf.Sin(t * Mathf.PI));
                score.color = new Color(accent.r, accent.g, accent.b, fade);
                if(t >= 1) ClearTile();
            }
        }

        private void BeginTile(TileState tile){
            currentRow = bridge.ScoringRow; stage = 0; elapsed = 0;
            contested = tile.side0 != null && tile.side1 != null;
            int difference = tile.total0 - tile.total1;
            winner = difference > 0 ? 0 : difference < 0 ? 1 : -1;
            points = Mathf.Abs(difference);
            bool local = winner == snapshot.localPlayerId;
            var cls = local ? board.setup.PlayerClass : board.setup.EnemyClass;
            accent = winner < 0 ? board.endRoundAnnouncementColor : cls ? cls.color : Color.white;
            var source = board.GetMiddleTileView(currentRow);
            
            if(!source) return;
            focus = Instantiate(source, layer);
            focus.name = "Scoring tile spotlight";
            focus.enabled = false;
            
            foreach(var graphic in focus.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            var canvasGroup = focus.GetComponent<CanvasGroup>();
            if(!canvasGroup) canvasGroup = focus.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.interactable = false; canvasGroup.blocksRaycasts = false;
            var rect = (RectTransform)focus.transform;
            var sourceRect = (RectTransform)source.transform;
            var corners = new Vector3[4]; sourceRect.GetWorldCorners(corners);
            Vector2 bottom = InLayer(corners[0], sourceRect), top = InLayer(corners[2], sourceRect);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.sizeDelta = sourceRect.rect.size;
            rect.localScale = new Vector3((top.x - bottom.x) / Mathf.Max(1, sourceRect.rect.width),
                (top.y - bottom.y) / Mathf.Max(1, sourceRect.rect.height), 1);
            rect.anchoredPosition = focusPosition = (top + bottom) * 0.5f;
            var layout = focus.GetComponent<LayoutElement>();
            
            if(layout) layout.ignoreLayout = true;
            popup = (contested ? focus.duoPopup : focus.soloPopup)?.rectTransform;
            if(popup) popupScale = popup.localScale;
            if(contested) foreach(var image in focus.GetComponentsInChildren<Image>(true)){
                if(!image.name.ToLowerInvariant().Contains("lightstick")) continue;
                sticks.Add(image.rectTransform); stickRotations.Add(image.rectTransform.localRotation);
            }
            glow.rectTransform.anchoredPosition = focusPosition;
            glow.rectTransform.sizeDelta = (top - bottom) * 2.2f;
            score.transform.SetAsLastSibling();
        }

        private Vector2 AwardPosition(int actor){
            bool local = actor == snapshot.localPlayerId;
            var anchor = local ? board.playerScorePopupAnchor : board.enemyScorePopupAnchor;
            var hud = local ? board.playerHUD : board.enemyHUD;
            if(!anchor && hud && hud.score) anchor = hud.score.rectTransform;
            return anchor ? InLayer(anchor.TransformPoint(anchor.rect.center), anchor) : focusPosition;
        }

        private Vector2 InLayer(Vector3 world, RectTransform source){
            var canvas = source.GetComponentInParent<Canvas>();
            Camera camera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 result;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer,
                RectTransformUtility.WorldToScreenPoint(camera, world), null, out result);
            return result;
        }

        private void Burst(float t){
            for(int i = 0; i < sparks.Length; i++){
                float angle = i * Mathf.PI * 2 / sparks.Length;
                float radius = Mathf.Lerp(25, 100, 1 - Mathf.Pow(1 - t, 3));
                sparks[i].rectTransform.anchoredPosition = focusPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                sparks[i].rectTransform.localRotation = Quaternion.Euler(0, 0, 45 + t * 90);
                sparks[i].color = new Color(accent.r, accent.g, accent.b, Mathf.Sin(t * Mathf.PI));
            }
        }

        private void Build(){
            if(group) return;
            var root = new GameObject("Round score presentation", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            root.transform.SetParent(transform, false); layer = (RectTransform)root.transform;
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 90;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            group = root.GetComponent<CanvasGroup>(); group.interactable = false; group.blocksRaycasts = false;
            shade = Image("Dim board", Vector2.zero);
            shade.rectTransform.anchorMin = Vector2.zero; shade.rectTransform.anchorMax = Vector2.one;
            glow = Image("Soft spotlight", new Vector2(300, 300));
            glowTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            glowTexture.wrapMode = TextureWrapMode.Clamp;
            
            for(int y = 0; y < 64; y++) for(int x = 0; x < 64; x++){
                float distance = Vector2.Distance(new Vector2(x, y), Vector2.one * 31.5f) / 31.5f;
                glowTexture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - distance), 2)));
            }
            glowTexture.Apply(); glowSprite = Sprite.Create(glowTexture, new Rect(0, 0, 64, 64), Vector2.one * 0.5f);
            glow.sprite = glowSprite;
            
            for(int i = 0; i < sparks.Length; i++) sparks[i] = Image("Score spark", Vector2.one * (4 + i % 3 * 2));
            var textRoot = new GameObject("Score award", typeof(RectTransform)); textRoot.transform.SetParent(layer, false);
            score = textRoot.AddComponent<TextMeshProUGUI>();
            score.rectTransform.sizeDelta = new Vector2(240, 90);
            score.fontSize = 48; score.alignment = TextAlignmentOptions.Center; score.raycastTarget = false;
            if(board.phaseAnimator && board.phaseAnimator.font) score.font = board.phaseAnimator.font;
            Cancel();
        }

        private Image Image(string label, Vector2 size){
            var image = new GameObject(label, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(layer, false); image.rectTransform.sizeDelta = size;
            image.raycastTarget = false; return image;
        }

        private void ClearTile(){
            if(focus){ focus.gameObject.SetActive(false); Destroy(focus.gameObject); }
            focus = null; popup = null; sticks.Clear(); stickRotations.Clear(); currentRow = -1;
            if(score) score.gameObject.SetActive(false);
            if(group) group.gameObject.SetActive(false);
        }

        public void Cancel(){
            ClearTile();
        }
        
        private void OnDisable(){
            Cancel();
        }
        
        private void OnDestroy(){
            if(glowSprite) Destroy(glowSprite);
            if(glowTexture) Destroy(glowTexture);
        }
    }
}
