using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VocaloidTCG
{
    public sealed class DeckCardView : MonoBehaviour
    {
        public Image art;
        public TMP_Text cardName, amount;
        public void Bind(CardData card, int copies){
            if(!card) { gameObject.SetActive(false); return; }
            gameObject.SetActive(true);
            DeckUI.Image(art, card.iconImage);
            DeckUI.Text(cardName, card.cardName);
            DeckUI.Text(amount, "x" + copies);
        }
    }

    public static class DeckUI
    {
        public static void Text(TMP_Text target, string value){
            if(target){
                target.richText = false; target.text = value ?? "";
            }
        }
        
        public static void Image(Image target, Sprite value){
            if(target){
                target.sprite = value; target.enabled = value;
            }
        }
        
        public static void Classes(Image first, Image second, DeckRecord deck, DeckCatalog catalog){
            Image(first, deck.classes.Count > 0 ? catalog.Class(deck.classes[0])?.classIcon : null);
            if(second) second.gameObject.SetActive(deck.classes.Count > 1);
            Image(second, deck.classes.Count > 1 ? catalog.Class(deck.classes[1])?.classIcon : null);
        }
        
        public static void Count(TMP_Text target, int count){
            if(!target) return;
            target.richText = true;
            target.text = (count > 30 ? "<color=#FF9999>" + count + "</color>" : count.ToString()) + "/30";
        }
    }
}
