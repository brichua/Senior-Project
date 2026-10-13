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
            Put(influence, card.hasInfluence ? card.currentInfluence.ToString() : "");
            Put(cost, card.currentCost.ToString());

            if(influenceRoot) influenceRoot.SetActive(card.hasInfluence);
            BoardUIController.SetImage(art, card.data.iconImage);
            bool statlessStageEffect = card.data.stageEffect && !card.hasInfluence;
            BoardUIController.SetImage(icon, statlessStageEffect ? side.stageEffectInfoIcon : side.infoIcon);
            var body = new StringBuilder(card.token ? "Token. No effects or keywords. Costs 0 energy." : Format(card.data.info, setup));

            if(card.activeEffects != null && card.activeEffects.Count > 0){
                body.Append("\n\n<b>Active effects</b>");
                foreach (var effect in card.activeEffects)
                    body.Append("\n• ").Append(Format(effect, setup));
            }
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

        private void LateUpdate(){
            if(!resetDescriptionScroll || !description) return;
            if(!descriptionScroll) descriptionScroll = description.GetComponentInParent<ScrollRect>();
            resetDescriptionScroll = false;
            if(!descriptionScroll || !descriptionScroll.content) return;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(descriptionScroll.content);
            descriptionScroll.StopMovement();
            descriptionScroll.verticalNormalizedPosition = 1;
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
        
        private static string Format(string text, BoardSetup setup){
            var literal = Escape(text);
            var definitions = CardKeywords.Descriptions(setup.keywordDescriptions);
            var words = (setup.boldKeywords ?? new string[0])
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
