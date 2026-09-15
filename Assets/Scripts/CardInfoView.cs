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

        public void Clear(){
            gameObject.SetActive(false);
        }

        public void Show(CardState card, SideArt side, BoardSetup setup){
            if(card == null || card.data == null){
                Clear(); return;
            }

            gameObject.SetActive(true);
            Put(cardName, Escape(card.data.cardName));
            Put(influence, card.hasInfluence ? card.currentInfluence.ToString() : "");
            Put(cost, card.currentCost.ToString());

            if(influenceRoot) influenceRoot.SetActive(card.hasInfluence);
            BoardUIController.SetImage(art, card.data.iconImage);
            bool statlessStageEffect = card.data.stageEffect && !card.hasInfluence;
            BoardUIController.SetImage(icon, statlessStageEffect ? side.stageEffectInfoIcon : side.infoIcon);
            var body = new StringBuilder(Format(card.data.info, setup.boldKeywords));

            if(card.activeEffects != null && card.activeEffects.Count > 0){
                body.Append("\n\n<b>Active effects</b>");
                foreach (var effect in card.activeEffects)
                    body.Append("\n• ").Append(Format(effect, setup.boldKeywords));
            }
            Put(description, body.ToString());
        }

        public static void Put(TMP_Text label, string value){
            if(label) label.text = value;
        }

        private static string Escape(string value){
            return (value ?? "").Replace("<", "‹").Replace(">", "›");
        }
        
        private static string Format(string text, string[] keywords){
            var literal = Escape(text);
            if(keywords == null) return literal;
            var words = keywords.Where(k => !string.IsNullOrWhiteSpace(k)).Select(Regex.Escape).ToArray();
            if(words.Length == 0) return literal;
            return Regex.Replace(literal, @"\b(?:" + string.Join("|", words) + @")\b",
                m => "<b>" + m.Value + "</b>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }
}
