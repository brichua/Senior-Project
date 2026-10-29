using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VocaloidTCG.BoardUI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace VocaloidTCG
{
    /// <summary>A persistent instance of the authored tutorial message prefab.</summary>
    public sealed class TutorialMessageView : MonoBehaviour
    {
        private RectTransform surface, panel, highlight, arrow;
        private TMP_Text body, continueLabel;
        private string continueText;
        private Image portrait;
        private Action advance;
        private float visible;
        private int characterCount;
        private bool typing, hasMessage;
        private int shownFrame;
        private Vector2 authoredSize;
        private Canvas overlay;
        public bool IsTyping => typing;

        public static TutorialMessageView Create(BoardUIController board, GameObject prefab, Action onAdvance){
            if(!prefab) throw new InvalidOperationException("Assign Tutorial Message Prefab on the Tutorial story asset.");
            // A separate canvas cannot inherit a board/opening panel's fade, mask, or inactive state.
            var obj = new GameObject("Tutorial overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var view = obj.AddComponent<TutorialMessageView>();
            view.surface = (RectTransform)obj.transform; view.advance = onAdvance;
            view.overlay = obj.GetComponent<Canvas>();
            view.overlay.enabled = false;
            view.overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            view.overlay.sortingOrder = 31000; // Above the opening presentation (30000).
            var scaler = obj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            var instance = Instantiate(prefab, view.surface);
            view.panel = instance.GetComponent<RectTransform>();
            // The prefab also contains an authored "click to continue" label.
            // Bind dialogue explicitly so hierarchy order cannot select that label instead.
            foreach(var label in instance.GetComponentsInChildren<TMP_Text>(true)){
                if(label.name.IndexOf("continue", StringComparison.OrdinalIgnoreCase) >= 0){
                    view.continueLabel = label; view.continueText = label.text;
                }else if(string.Equals(label.name, "Text (TMP)", StringComparison.OrdinalIgnoreCase) || !view.body) view.body = label;
            }
            foreach(var image in instance.GetComponentsInChildren<Image>(true))
                if(string.Equals(image.name, "art", StringComparison.OrdinalIgnoreCase)) { view.portrait = image; break; }
            if(!view.panel || !view.body || !view.portrait){
                Destroy(obj);
                throw new InvalidOperationException("The tutorial message prefab needs a RectTransform, a TMP text label, and an Image named art.");
            }
            instance.SetActive(false);
            view.authoredSize = view.panel.rect.size;
            view.panel.anchorMin = view.panel.anchorMax = view.panel.pivot = new Vector2(0.5f, 0.5f);
            // Preserve the prefab's artwork, colors, child positions, and proportions.
            // Long instructions may shrink within its existing text area.
            view.body.enableAutoSizing = true;
            view.body.fontSizeMax = view.body.fontSize;
            view.body.fontSizeMin = Mathf.Min(view.body.fontSize, 7.5f);
            view.body.richText = false;
            // Keep the prefab's own continue label. All tutorial graphics allow clicks through.
            foreach(var graphic in instance.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            var button = instance.GetComponent<Button>();
            if(button) button.enabled = false; // Screen input below is the single advance source.
            view.highlight = StoryUIElements.Rect(view.surface, "Tutorial focus", Vector2.zero, Vector2.zero);
            Edge(view.highlight, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 4));
            Edge(view.highlight, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 4));
            Edge(view.highlight, new Vector2(0, 0), new Vector2(0, 1), new Vector2(4, 0));
            Edge(view.highlight, new Vector2(1, 0), new Vector2(1, 1), new Vector2(4, 0));
            var arrow = StoryUIElements.Text(view.surface, "Look here", "▼", Vector2.zero, new Vector2(40, 35));
            arrow.fontSize = 26; arrow.color = new Color(0.4f, 1, 0.85f); view.arrow = arrow.rectTransform;
            view.highlight.gameObject.SetActive(false);
            view.arrow.gameObject.SetActive(false);
            view.panel.SetAsLastSibling();
            return view;
        }
        private static void Edge(RectTransform parent, Vector2 min, Vector2 max, Vector2 size){
            var edge = StoryUIElements.Rect(parent, "Focus edge", Vector2.zero, size);
            edge.anchorMin = min; edge.anchorMax = max;
            var graphic = edge.gameObject.AddComponent<Image>(); graphic.color = new Color(0.4f, 1f, 0.85f); graphic.raycastTarget = false;
        }
        public void Show(string text, bool canContinue, int expression, StoryData story, Sprite fallback){
            shownFrame = Time.frameCount;
            gameObject.SetActive(true);
            if(panel){
                panel.gameObject.SetActive(true);
                foreach(var group in panel.GetComponentsInChildren<CanvasGroup>(true)){
                    group.alpha = 1; group.interactable = true; group.blocksRaycasts = true;
                }
            }
            // Tutorial dialogue is not a timed status notification.
            var fade = body.GetComponent<StatusTextFade>();
            if(fade) fade.enabled = false;
            body.gameObject.SetActive(true); body.enabled = true; body.alpha = 1;
            body.canvasRenderer.SetAlpha(1);
            body.text = text ?? ""; body.maxVisibleCharacters = int.MaxValue;
            body.ForceMeshUpdate(true, true);
            characterCount = body.textInfo.characterCount;
            visible = 0; typing = characterCount > 0;
            body.maxVisibleCharacters = typing ? 0 : int.MaxValue;
            if(continueLabel) continueLabel.text = canContinue ? continueText : "complete action to continue";
            if(portrait){
                portrait.gameObject.SetActive(true); portrait.enabled = true;
                portrait.sprite = story && story.tutorialExpressions != null && expression >= 0 && expression < story.tutorialExpressions.Length && story.tutorialExpressions[expression]
                    ? story.tutorialExpressions[expression] : fallback;
                portrait.canvasRenderer.SetAlpha(1);
            }
            // Reveal only after the authored placeholder text and art have been replaced.
            hasMessage = true;
            if(overlay) overlay.enabled = true;
        }
        // false means this click was consumed finishing the current line.
        public bool Reveal(){
            if(!typing) return true;
            typing = false; visible = characterCount; body.maxVisibleCharacters = int.MaxValue;
            return false;
        }
        public void ShowError(string error){
            typing = false; body.text = "Unable to save: " + error + "\nClick to retry."; body.maxVisibleCharacters = int.MaxValue;
        }
        private void Update(){ TickTyping(Time.unscaledDeltaTime); }
        private void LateUpdate(){
            // Process release after UI actions, so a card click cannot also dismiss a new line.
            if(advance == null || !overlay || !overlay.enabled || Time.frameCount <= shownFrame) return;
#if ENABLE_INPUT_SYSTEM
            bool clicked = Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
            clicked |= Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame;
#else
            bool clicked = Input.GetMouseButtonUp(0);
            for(int i = 0; i < Input.touchCount; i++) clicked |= Input.GetTouch(i).phase == TouchPhase.Ended;
#endif
            if(clicked) advance.Invoke();
        }
        private void TickTyping(float seconds){
            if(!typing) return;
            visible = Mathf.Min(characterCount, visible + Mathf.Max(0, seconds) * 45);
            if(visible >= characterCount){ Reveal(); return; }
            body.maxVisibleCharacters = Mathf.FloorToInt(visible);
        }
        public void SetPaused(bool paused){ if(overlay) overlay.enabled = hasMessage && !paused; }
        private Vector2 OnSurface(Vector3 world, RectTransform target){
            var canvas = target.GetComponentInParent<Canvas>();
            var camera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var screen = RectTransformUtility.WorldToScreenPoint(camera, world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(surface, screen, null, out var point);
            return point;
        }
        private RectTransform FocusTarget(BoardUIController board, string focus, string cardId, int x, int y){
            switch(focus){
                case "energy": return board.playerHUD && board.playerHUD.energy ? board.playerHUD.energy.rectTransform : null;
                case "score": return board.playerHUD && board.playerHUD.score ? board.playerHUD.score.rectTransform : null;
                case "deck": return board.playerHUD && board.playerHUD.deckCount ? board.playerHUD.deckCount.rectTransform : null;
                case "info": return InfoTarget(board.playerInfo, board.playerHUD);
                case "enemyInfo": return InfoTarget(board.enemyInfo, board.enemyHUD);
                case "venue": return board.enemyHUD && board.enemyHUD.venueEffect ? board.enemyHUD.venueEffect.rectTransform : null;
                case "endTurn": return board.endTurn ? board.endTurn.transform as RectTransform : null;
                case "openingClasses": return board.openingAnimator ? board.openingAnimator.PlayerDeckClassesFocus : null;
                case "opening": return null;
            }
            // Explicit destinations take priority over the card still in the player's hand.
            if(x >= 0 && y >= 0){
                var tile = board.GetEffectTileView(y * 5 + x);
                if(!tile) return null;
                if(focus == "popup"){
                    if(tile.soloPopup && tile.soloPopup.gameObject.activeInHierarchy) return tile.soloPopup.rectTransform;
                    if(tile.duoPopup && tile.duoPopup.gameObject.activeInHierarchy) return tile.duoPopup.rectTransform;
                }
                return tile.tile ? tile.tile.rectTransform : tile.transform as RectTransform;
            }
            var card = cardId != null ? board.GetHandView(cardId) : null;
            return card ? card.transform as RectTransform : null;
        }
        private static RectTransform InfoTarget(CardInfoView info, SideHUD hud){
            // "info card player" is a centered container; its visible panel uses the HUD background.
            if(hud && hud.infoBackground) return hud.infoBackground.rectTransform;
            if(info && info.art) return info.art.rectTransform;
            if(info && info.cardName) return info.cardName.rectTransform;
            return info ? info.transform as RectTransform : null;
        }
        public void SetFocus(BoardUIController board, string focus, string cardId, int x, int y){
            var target = FocusTarget(board, focus, cardId, x, y);
            if(surface.rect.width <= 32 || surface.rect.height <= 32) return;
            float width = Mathf.Min(600, surface.rect.width - 32);
            float scale = width / Mathf.Max(1, authoredSize.x);
            float height = authoredSize.y * scale;
            panel.localScale = new Vector3(scale, scale, 1);
            Vector2 position = new Vector2(surface.rect.xMax - width * 0.5f - 20, surface.rect.yMax - height * 0.5f - 20);
            bool hasTarget = target && target.gameObject.activeInHierarchy;
            highlight.gameObject.SetActive(hasTarget); arrow.gameObject.SetActive(hasTarget);
            if(hasTarget){
                var corners = new Vector3[4]; target.GetWorldCorners(corners);
                Vector2 a = OnSurface(corners[0], target), b = a;
                for(int i = 1; i < corners.Length; i++){
                    Vector2 point = OnSurface(corners[i], target);
                    a = Vector2.Min(a, point); b = Vector2.Max(b, point);
                }
                highlight.anchoredPosition = (a + b) * 0.5f; highlight.sizeDelta = b - a + new Vector2(8, 8);
                arrow.anchoredPosition = new Vector2((a.x + b.x) * 0.5f, b.y + 24 + Mathf.Sin(Time.unscaledTime * 4) * 7);
                float right = b.x + 20 + width * 0.5f;
                float left = a.x - 20 - width * 0.5f;
                position = new Vector2(right + width * 0.5f < surface.rect.xMax ? right : left, (a.y + b.y) * 0.5f);
                if(focus == "hand") position.y = b.y + height * 0.5f + 24;
                if(focus == "board" || focus == "popup") position.y = surface.rect.yMax - height * 0.5f - 20;
            }
            position.x = Mathf.Clamp(position.x, surface.rect.xMin + width * 0.5f + 16, surface.rect.xMax - width * 0.5f - 16);
            position.y = Mathf.Clamp(position.y, surface.rect.yMin + height * 0.5f + 16, surface.rect.yMax - height * 0.5f - 16);
            panel.anchoredPosition = position;
        }
    }
}
