using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VocaloidTCG
{
    public sealed class PuzzleCardView : MonoBehaviour
    {
        public Button button;
        public TMP_Text number, puzzleName, description, rewardName;
        public GameObject clearedImage;
        public Image rewardCardBack;
        public Image rewardCardArt;

        public void Bind(PuzzleData puzzle, bool completed, Action clicked){
            if (!button) button = GetComponent<Button>();
            foreach (var selectable in GetComponentsInChildren<Selectable>(true))
                if (selectable != button) selectable.enabled = false;
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = button && graphic.gameObject == button.gameObject;
            DeckUI.Text(number, "puzzle " + puzzle.number);
            DeckUI.Text(puzzleName, puzzle.puzzleName); DeckUI.Text(description, puzzle.shortDescription);
            DeckUI.Text(rewardName, puzzle.rewardCard ? puzzle.rewardCard.cardName : "");
            var cls = puzzle.rewardCard ? puzzle.rewardCard.cardClass : null;
            if(rewardName){ rewardName.color = cls ? cls.color : Color.white; rewardName.enableVertexGradient = false; }
            DeckUI.Image(rewardCardBack, cls && cls.specialCardBack ? cls.specialCardBack.image : null);
            if (!rewardCardArt)
            {
                var preview = transform.Find("card preview");
                if (preview) rewardCardArt = preview.GetComponent<Image>();
            }
            DeckUI.Image(rewardCardArt, puzzle.rewardCard ? puzzle.rewardCard.cardImage : null);
            
            if(clearedImage) clearedImage.SetActive(completed);
            if(button) { button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => clicked?.Invoke()); }
        }
    }
}
