using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VocaloidTCG
{
    public sealed class DeckCardIconView : MonoBehaviour
    {
        public Image art, popupImage;
        public TMP_Text amount;

        public void Bind(CardData card, int copies){
            gameObject.SetActive(card != null);
            if(!card) return;
            DeckUI.Image(art, card.iconImage);
            DeckUI.Image(popupImage, card.cardClass ? card.cardClass.popupImage : null);
            DeckUI.Text(amount, copies.ToString());
        }
    }
}
