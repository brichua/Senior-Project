using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VocaloidTCG.BoardUI
{
    public sealed class KeywordTooltip : MonoBehaviour
    {
        private TMP_Text source, label;
        private BoardSetup setup;
        private RectTransform panel, bounds;
        private Image background;
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        private PointerEventData pointer;
        private EventSystem pointerSystem;

        public void Configure(TMP_Text text, BoardSetup theme){
            source = text;
            setup = theme;
            source.raycastTarget = true;
            Hide();
        }

        private void LateUpdate(){
            if(!source || !setup || !EventSystem.current ||
                !KeywordTooltipPanel.Pointer(out Vector2 position) || !source.isActiveAndEnabled){ Hide(); return; }
            if(pointer == null || pointerSystem != EventSystem.current){
                pointerSystem = EventSystem.current;
                pointer = new PointerEventData(EventSystem.current);
            }
            pointer.Reset();
            pointer.position = position;
            hits.Clear();
            EventSystem.current.RaycastAll(pointer, hits);
            if(hits.Count == 0 || hits[0].gameObject != source.gameObject){ Hide(); return; }
            var canvas = source.canvas;
            var camera = canvas && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.rootCanvas.worldCamera : null;
            int link = TMP_TextUtilities.FindIntersectingLink(source, position, camera);
            if(link < 0){ Hide(); return; }
            string id = source.textInfo.linkInfo[link].GetLinkID();
            if(!id.StartsWith("keyword-") || !int.TryParse(id.Substring(8), out int index) ||
                setup.keywordDescriptions == null || index < 0 || index >= setup.keywordDescriptions.Length){ Hide(); return; }
            var definition = setup.keywordDescriptions[index];
            if(definition == null){ Hide(); return; }
            Show(definition, position);
        }

        private void Show(KeywordDefinition definition, Vector2 position){
            if(!panel){
                panel = KeywordTooltipPanel.Create(transform, "Keyword tooltip", out bounds);
                if(!panel) return;
                background = panel.gameObject.AddComponent<Image>();
                background.raycastTarget = false;
                background.type = Image.Type.Sliced;
                var child = new GameObject("Description", typeof(RectTransform), typeof(TextMeshProUGUI));
                child.layer = panel.gameObject.layer;
                child.transform.SetParent(panel, false);
                label = child.GetComponent<TextMeshProUGUI>();
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(14, 12);
                label.rectTransform.offsetMax = new Vector2(-14, -12);
                label.font = source.font;
                label.fontSize = 22;
                label.color = Color.white;
                label.richText = false;
                label.raycastTarget = false;
                label.enableAutoSizing = true;
                label.fontSizeMin = 10;
                label.fontSizeMax = 22;
            }
            panel.gameObject.SetActive(true);
            background.sprite = setup.keywordTooltipBackground;
            background.color = background.sprite ? Color.white : new Color(0.106f, 0.141f, 0.239f, .98f);
            label.text = definition.keyword + "\n" +
                (string.IsNullOrWhiteSpace(definition.description) ? "n/a" : definition.description);
            float width = Mathf.Min(340, bounds.rect.width - 16);
            float height = Mathf.Min(label.GetPreferredValues(label.text, Mathf.Max(1, width - 28), Mathf.Infinity).y + 24,
                bounds.rect.height - 16);
            panel.sizeDelta = new Vector2(Mathf.Max(1, width), Mathf.Max(1, height));
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(bounds, position,
                KeywordTooltipPanel.CameraFor(bounds), out Vector2 point))
                KeywordTooltipPanel.Place(panel, bounds, point + new Vector2(width * .5f + 16, height * .5f + 16));
        }

        private void Hide(){ if(panel) panel.gameObject.SetActive(false); }
        private void OnDisable(){ Hide(); }
        private void OnDestroy(){ if(panel) Destroy(panel.gameObject); }
    }
}
