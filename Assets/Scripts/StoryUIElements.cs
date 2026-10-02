using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace VocaloidTCG
{
    public static class StoryUIElements
    {
        public static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size){
            var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform; rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }

        public static GameObject Panel(Transform parent, string name, Vector2 size){
            var rect = Rect(parent, name, Vector2.zero, size);
            rect.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.09f, 0.14f, 0.97f); return rect.gameObject;
        }

        public static TMP_Text Text(Transform parent, string name, string value, Vector2 position, Vector2 size){
            var text = Rect(parent, name, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value; text.fontSize = 24; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            return text;
        }

        public static Button Button(Transform parent, string label, Vector2 position, UnityAction action){
            var rect = Rect(parent, label, position, new Vector2(220, 60));
            rect.gameObject.AddComponent<Image>().color = new Color(0.23f, 0.34f, 0.45f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
            Text(rect, "Label", label, Vector2.zero, new Vector2(210, 55));
            if(action != null) button.onClick.AddListener(action);
            return button;
        }
    }
}
