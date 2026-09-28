using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VocaloidTCG
{
    public sealed class CardPreviewView : MonoBehaviour
    {
        public Button button;
        public Image art, border, vipIcon;
        public TMP_Text amount, cost, influence;

        public void Bind(CardData card, int copies, Action clicked = null){
            gameObject.SetActive(card != null);
            if(!card) return;

            var cls = card.cardClass;
            DeckUI.Image(art, card.cardImage);
            DeckUI.Image(border, cls ? (card.stageEffect ? cls.stageEffectBorder : cls.performerBorder) : null);

            if(border) border.color = Color.white;
            DeckUI.Image(vipIcon, card.vip && cls ? cls.vipImage : null);

            if(vipIcon) vipIcon.gameObject.SetActive(card.vip);
            DeckUI.Text(amount, copies.ToString());
            DeckUI.Text(cost, card.cost.ToString());
            DeckUI.Text(influence, card.influence.ToString());
            
            if(influence) influence.gameObject.SetActive(!card.stageEffect);
            if(button){
                button.onClick.RemoveAllListeners();
                if(clicked != null) button.onClick.AddListener(() => clicked());
            }
        }
    }
}
