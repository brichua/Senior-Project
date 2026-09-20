using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace VocaloidTCG.BoardUI
{
    internal static class KeywordTooltipPanel
    {
        public static RectTransform Create(Transform owner, string name, out RectTransform bounds){
            var canvas = owner.GetComponentInParent<Canvas>();
            bounds = canvas ? canvas.rootCanvas.transform as RectTransform : null;
            if(!bounds) return null;
            var panel = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            panel.layer = bounds.gameObject.layer;
            var rect = (RectTransform)panel.transform;
            rect.SetParent(bounds, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            var layer = panel.GetComponent<Canvas>();
            layer.overrideSorting = true;
            layer.sortingLayerID = canvas.rootCanvas.sortingLayerID;
            layer.sortingOrder = canvas.rootCanvas.sortingOrder + 100;
            var group = panel.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            return rect;
        }

        public static bool Pointer(out Vector2 position){
            position = Vector2.zero;
            if(!Application.isFocused) return false;
#if ENABLE_INPUT_SYSTEM
            if(Mouse.current == null) return false;
            position = Mouse.current.position.ReadValue();
            return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
            position = Input.mousePosition;
            return Input.mousePresent;
#else
            return false;
#endif
        }

        public static Camera CameraFor(RectTransform bounds){
            var canvas = bounds.GetComponent<Canvas>();
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        public static void Place(RectTransform panel, RectTransform bounds, Vector2 point){
            var area = bounds.rect;
            var half = panel.rect.size * .5f;
            panel.localPosition = new Vector3(
                Mathf.Clamp(point.x, area.xMin + half.x, area.xMax - half.x),
                Mathf.Clamp(point.y, area.yMin + half.y, area.yMax - half.y), 0);
        }
    }
}
