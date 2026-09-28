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

        public void Bind(PuzzleData puzzle, bool completed, Action clicked){
            DeckUI.Text(number, "puzzle " + puzzle.number);
            DeckUI.Text(puzzleName, puzzle.puzzleName); DeckUI.Text(description, puzzle.shortDescription);
            DeckUI.Text(rewardName, puzzle.rewardCard ? puzzle.rewardCard.cardName : "");
            var cls = puzzle.rewardCard ? puzzle.rewardCard.cardClass : null;
            DeckUI.Image(rewardCardBack, cls && cls.specialCardBack ? cls.specialCardBack.image : null);
            
            if(clearedImage) clearedImage.SetActive(completed);
            if(button) { button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => clicked?.Invoke()); }
        }
    }
}
