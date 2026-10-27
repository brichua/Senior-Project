using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VocaloidTCG.BoardUI
{
    public sealed class CardInfoView : MonoBehaviour
    {
        public TMP_Text cardName, influence, cost, description;
        public Image art, icon;
        public GameObject influenceRoot;
        private KeywordTooltip keywordTooltip;
        private ScrollRect descriptionScroll;
        private string shownCardId;
        private bool resetDescriptionScroll;
        private bool descriptionTinted;
        private Color descriptionColor;
        private RectTransform descriptionArea;
        private float descriptionTop;
        private string layoutName;
        private Vector2 layoutSize;
        private RectTransform infoBackground;
        private readonly Vector3[] layoutCorners = new Vector3[4];
        private bool textSizingConfigured;
        private Rect layoutBackgroundBounds;
        private string measuredDescription;
        private Vector2 measuredViewport;

        private void OnEnable(){ resetDescriptionScroll = true; }

        public void Clear(){
            shownCardId = null;
            gameObject.SetActive(false);
        }

        public void Show(CardState card, SideArt side, BoardSetup setup){
            if(card == null || card.data == null){
                Clear(); return;
            }

            gameObject.SetActive(true);
            if(descriptionTinted && description){ description.color=descriptionColor; descriptionTinted=false; }
            Put(cardName, Escape(card.data.cardName));
            if(cardName) cardName.color = card.data.cardClass ? card.data.cardClass.color : side.textColor;
            Put(influence, card.hasInfluence ? card.currentInfluence.ToString() : "");
            Put(cost, card.currentCost.ToString());

            if(influenceRoot) influenceRoot.SetActive(card.hasInfluence);
            BoardUIController.SetImage(art, card.token && card.data.stageEffect ? side.avatar : card.data.iconImage);
            bool statlessStageEffect = card.data.stageEffect && !card.hasInfluence;
            BoardUIController.SetImage(icon, statlessStageEffect ? side.stageEffectInfoIcon : side.infoIcon);
            var body = new StringBuilder(card.token ? Format("Token. Costs " + card.currentCost + " energy. No card abilities." +
                (PerformerKeywords.Of(card).Count>0 ? "\n"+PerformerKeywords.Of(card)+"." : " No keywords."),setup) : Format(card.data.info, setup));

            if(card.activeEffects != null && card.activeEffects.Count > 0){
                body.Append("\n\n<b>Active effects</b>");
                foreach (var effect in card.activeEffects)
                    body.Append("\n• ").Append(Format(effect, setup));
            }
            if(!card.token && !string.IsNullOrEmpty(card.effectProgress)) body.Append("\n\n").Append(card.effectProgress);
            string bodyText = body.ToString();
            bool descriptionChanged = description && description.text != bodyText;
            resetDescriptionScroll |= shownCardId != card.instanceId || descriptionChanged;
            shownCardId = card.instanceId;
            Put(description, bodyText);
            if(description){
                description.richText = true;
                if(!keywordTooltip) keywordTooltip = description.GetComponent<KeywordTooltip>();
                if(!keywordTooltip) keywordTooltip = description.gameObject.AddComponent<KeywordTooltip>();
                keywordTooltip.Configure(description, setup);
            }
        }

        public RectTransform DescriptionViewport => descriptionScroll ? descriptionScroll.viewport : null;

        public void RefreshLayout(){
            UpdateNameSpacing();
            RefreshDescriptionLayout();
        }
        private void LateUpdate(){ RefreshLayout(); }

        private void RefreshDescriptionLayout(){
            if(!description || !descriptionScroll || !descriptionScroll.content || !descriptionScroll.viewport) return;
            var viewport=descriptionScroll.viewport;
            viewport.ForceUpdateRectTransforms();
            var size=viewport.rect.size;
            if(!resetDescriptionScroll && measuredDescription==description.text && measuredViewport==size) return;
            if(size.x<=0 || size.y<=0) return;
            bool reset=resetDescriptionScroll;
            float position=descriptionScroll.verticalNormalizedPosition;
            Canvas.ForceUpdateCanvases();
            size=viewport.rect.size;
            var textRect=description.rectTransform;
            textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,size.x);
            description.ForceMeshUpdate();
            float preferred=description.GetPreferredValues(description.text,size.x,float.PositiveInfinity).y;
            textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(size.y,preferred+8));
            description.ForceMeshUpdate();
            LayoutRebuilder.ForceRebuildLayoutImmediate(textRect);
            if(reset) descriptionScroll.StopMovement();
            descriptionScroll.verticalNormalizedPosition=reset ? 1 : position;
            measuredDescription=description.text; measuredViewport=size; resetDescriptionScroll=false;
        }

        public void UpdateNameSpacing(){
            if(!cardName || !description) return;
            if(!textSizingConfigured){
                ConfigureTextSizing(cardName);
                EnsureDescriptionScroll();
                var hud = GetComponentInParent<SideHUD>();
                if(hud && hud.infoBackground) infoBackground = hud.infoBackground.rectTransform;
                textSizingConfigured = true;
            }
            if(!descriptionArea){
                descriptionScroll = description.GetComponentInParent<ScrollRect>();
                descriptionArea = descriptionScroll ? (RectTransform)descriptionScroll.transform : description.rectTransform;
                descriptionTop = descriptionArea.offsetMax.y;
            }
            if(infoBackground){
                UpdateBackgroundLayout();
                return;
            }
            var size = ((RectTransform)transform).rect.size;
            if(layoutName == cardName.text && layoutSize == size) return;
            layoutName = cardName.text;
            layoutSize = size;
            var top = descriptionArea.offsetMax;
            top.y = descriptionTop;
            descriptionArea.offsetMax = top;
            cardName.ForceMeshUpdate();
            if(cardName.textInfo.characterCount == 0) return;
            var parent = descriptionArea.parent;
            var titleBottom = parent.InverseTransformPoint(cardName.transform.TransformPoint(
                new Vector3(0, cardName.textBounds.min.y, 0))).y;
            var bodyTop = parent.InverseTransformPoint(descriptionArea.TransformPoint(
                new Vector3(0, descriptionArea.rect.yMax, 0))).y;
            top.y -= Mathf.Max(0, bodyTop - titleBottom + 8);
            descriptionArea.offsetMax = top;
            resetDescriptionScroll = true;
        }

        private static void ConfigureTextSizing(TMP_Text text){
            if(!text.enableAutoSizing){
                text.fontSizeMax = text.fontSize;
                text.fontSizeMin = Mathf.Min(text.fontSizeMin, text.fontSize * 0.6f);
                text.enableAutoSizing = true;
            }
            text.alignment = TextAlignmentOptions.TopLeft;
        }

        private void EnsureDescriptionScroll(){
            descriptionScroll = description.GetComponentInParent<ScrollRect>();
            if(descriptionScroll && !descriptionScroll.transform.IsChildOf(transform)) descriptionScroll=null;
            if(!descriptionScroll){
                var textRect = description.rectTransform;
                var viewportObject = new GameObject("Description scroll", typeof(RectTransform),
                    typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
                viewportObject.layer = description.gameObject.layer;
                var viewport = (RectTransform)viewportObject.transform;
                viewport.SetParent(textRect.parent, false);
                viewport.SetSiblingIndex(textRect.GetSiblingIndex());
                viewport.anchorMin = textRect.anchorMin;
                viewport.anchorMax = textRect.anchorMax;
                viewport.pivot = textRect.pivot;
                viewport.sizeDelta = textRect.sizeDelta;
                viewport.anchoredPosition = textRect.anchoredPosition;
                viewport.localRotation = textRect.localRotation;
                viewport.localScale = textRect.localScale;
                viewportObject.GetComponent<Image>().color = Color.clear;
                textRect.SetParent(viewport, false);
                textRect.localScale = Vector3.one;
                textRect.localRotation = Quaternion.identity;
                textRect.anchorMin = new Vector2(0, 1);
                textRect.anchorMax = Vector2.one;
                textRect.pivot = new Vector2(0.5f, 1);
                textRect.anchoredPosition = Vector2.zero;
                textRect.sizeDelta = Vector2.zero;
                descriptionScroll = viewportObject.GetComponent<ScrollRect>();
                descriptionScroll.viewport = viewport;
                descriptionScroll.content = textRect;
            }
            descriptionScroll.horizontal = false;
            descriptionScroll.vertical = true;
            descriptionScroll.movementType = ScrollRect.MovementType.Clamped;
            descriptionScroll.scrollSensitivity = 24;
            if(!descriptionScroll.viewport) descriptionScroll.viewport=(RectTransform)descriptionScroll.transform;
            var view=descriptionScroll.viewport;
            if(!view.GetComponent<Mask>() && !view.GetComponent<RectMask2D>()) view.gameObject.AddComponent<RectMask2D>();
            var hitArea=view.GetComponent<Image>();
            if(!hitArea){ hitArea=view.gameObject.AddComponent<Image>(); hitArea.color=Color.clear; }
            hitArea.raycastTarget=true;
            var content=description.rectTransform;
            if(content.parent!=view) content.SetParent(view,false);
            content.anchorMin=new Vector2(0,1); content.anchorMax=Vector2.one; content.pivot=new Vector2(0.5f,1);
            content.localScale=Vector3.one; content.localRotation=Quaternion.identity;
            content.sizeDelta=Vector2.zero; content.anchoredPosition=Vector2.zero;
            descriptionScroll.content=content;
            description.enableAutoSizing = false;
            description.textWrappingMode=TextWrappingModes.Normal;
            description.alignment = TextAlignmentOptions.TopLeft;
            description.overflowMode = TextOverflowModes.Overflow;
            description.raycastTarget = true;
            description.maskable=true;
            var fitter=description.GetComponent<ContentSizeFitter>();
            if(fitter) fitter.enabled=false;
            resetDescriptionScroll=true;
        }

        private Rect BoundsIn(RectTransform rect, Transform parent){
            rect.GetWorldCorners(layoutCorners);
            var min = (Vector2)parent.InverseTransformPoint(layoutCorners[0]);
            var max = min;
            for(int i = 1; i < layoutCorners.Length; i++){
                var point = (Vector2)parent.InverseTransformPoint(layoutCorners[i]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void SetTextBounds(RectTransform rect, Rect bounds){
            var parent = (RectTransform)rect.parent;
            var min = (Vector2)parent.InverseTransformPoint(transform.TransformPoint(bounds.min));
            var max = (Vector2)parent.InverseTransformPoint(transform.TransformPoint(bounds.max));
            rect.localScale = Vector3.one;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.offsetMin = min - parent.rect.min;
            rect.offsetMax = max - parent.rect.min;
        }

        private void UpdateBackgroundLayout(){
            var bounds = BoundsIn(infoBackground, transform);
            float padding = Mathf.Min(16, bounds.width * 0.05f);
            float top = bounds.yMax - padding;
            if(art && art.gameObject.activeInHierarchy)
                top = Mathf.Min(top, BoundsIn(art.rectTransform, transform).yMin - 8);
            if(icon && icon.gameObject.activeInHierarchy)
                top = Mathf.Min(top, BoundsIn(icon.rectTransform, transform).yMin - 8);
            var available = Rect.MinMaxRect(bounds.xMin + padding, bounds.yMin + padding,
                bounds.xMax - padding, top);
            if(available.width <= 0 || available.height <= 16) return;
            if(layoutName == cardName.text && layoutBackgroundBounds == available) return;
            layoutName = cardName.text;
            layoutBackgroundBounds = available;
            float titleHeight = Mathf.Min(cardName.fontSizeMax * 2.4f, (available.height - 8) * 0.4f);
            SetTextBounds(cardName.rectTransform, Rect.MinMaxRect(available.xMin,
                available.yMax - titleHeight, available.xMax, available.yMax));
            cardName.ForceMeshUpdate();
            float usedHeight = Mathf.Min(titleHeight, cardName.preferredHeight);
            SetTextBounds(descriptionArea, Rect.MinMaxRect(available.xMin, available.yMin,
                available.xMax, available.yMax - usedHeight - 8));
            resetDescriptionScroll = true;
        }

        public void ShowEffect(CardState card, SideArt side, BoardSetup setup, string instruction){
            Show(card,side,setup);
            if(!description) return;
            descriptionColor=description.color; descriptionTinted=true;
            description.color=card.data.cardClass ? card.data.cardClass.color : side.textColor;
            description.text="<b>"+Escape(instruction)+"</b>\n\n"+description.text;
            resetDescriptionScroll=true;
        }

        public static void Put(TMP_Text label, string value){
            if(label) label.text = value;
        }

        private static string Escape(string value){
            return (value ?? "").Replace("<", "‹").Replace(">", "›");
        }
        
        public static string FormatDescription(string text, BoardSetup setup = null) => Format(text, setup);
        private static string Format(string text, BoardSetup setup){
            var literal = Escape(text);
            var definitions = CardKeywords.Descriptions(setup ? setup.keywordDescriptions : null);
            var words = ((setup ? setup.boldKeywords : null) ?? new string[0])
                .Concat(definitions.Where(d => d != null).Select(d => d.keyword))
                .Where(k => !string.IsNullOrWhiteSpace(k)).Distinct()
                .OrderByDescending(k => k.Length).Select(k => Regex.Escape(Escape(k))).ToArray();
            if(words.Length == 0) return literal;
            return Regex.Replace(literal, @"\b(?:" + string.Join("|", words) + @")\b",
                m => {
                    int index = System.Array.FindIndex(definitions, d => d != null &&
                        string.Equals(Escape(d.keyword), m.Value, System.StringComparison.OrdinalIgnoreCase));
                    string bold = "<b>" + m.Value + "</b>";
                    return index < 0 ? bold : "<link=\"keyword-" + index + "\">" + bold + "</link>";
                }, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }
}
