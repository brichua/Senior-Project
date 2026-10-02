using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace VocaloidTCG.BoardUI
{
    public sealed class ConcertBackdrop : MonoBehaviour
    {
        private Image current, next;
        private Sprite[] frames;
        private BoardSetup setup;
        private GameplayBoardBridge bridge;
        private float frame;
        public float Speed { get; set; } = 1;

        public static ConcertBackdrop Create(Transform parent, BoardUIController board){
            var rect = Rect(parent, "Concert background", Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            var result = rect.gameObject.AddComponent<ConcertBackdrop>();
            result.setup = board.setup; result.bridge = board.game as GameplayBoardBridge;
            result.frames = board.setup.concertBackgroundFrames == null ? new Sprite[0] :
                board.setup.concertBackgroundFrames.Where(s => s).ToArray();
            result.current = FullImage(rect, "Background art");
            result.next = FullImage(rect, "Next background frame");
            result.current.raycastTarget = true;
            result.ShowFrame();
            return result;
        }

        public void ShowRedraw(){
            Speed = 1;
            if(!setup.redrawBackground) return;
            frames = new[] { setup.redrawBackground }; frame = 0; ShowFrame();
        }

        private void Update(){
            if(bridge && bridge.IsPaused) return;
            frame += Time.unscaledDeltaTime * Mathf.Max(0.1f, setup.backgroundFramesPerSecond) * Speed;
            ShowFrame();
        }

        private void ShowFrame(){
            var fallback = setup.concertBackground ? setup.concertBackground : setup.background;
            current.sprite = frames.Length > 0 ? frames[Mathf.FloorToInt(frame) % frames.Length] : fallback;
            current.color = current.sprite ? Color.white : new Color(0.025f, 0.045f, 0.08f);
            next.enabled = frames.Length > 1;
            if(next.enabled){
                next.sprite = frames[(Mathf.FloorToInt(frame) + 1) % frames.Length];
                next.color = new Color(1, 1, 1, Mathf.SmoothStep(0, 1, Mathf.Repeat(frame, 1)));
            }
        }

        private static Image FullImage(Transform parent, string name){
            var rect = Rect(parent, name, Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            var image = rect.gameObject.AddComponent<Image>(); image.raycastTarget = false;
            return image;
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size){
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }

        public static void StyleButton(Button button, BoardSetup setup, Color accent){
            if(setup.concertButtonImage){ button.image.sprite = setup.concertButtonImage; button.image.color = Color.white; }
            else button.image.color = new Color(0.025f, 0.055f, 0.09f, 0.94f);
            var outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(accent.r, accent.g, accent.b, 0.75f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }
    }
}
