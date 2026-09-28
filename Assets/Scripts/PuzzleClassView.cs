using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VocaloidTCG
{
    public sealed class PuzzleClassView : MonoBehaviour
    {
        public Button button;
        public Image classIcon, art;
        public TMP_Text className, count;
        public Slider progress;
        public GameObject completedImage;

        public void Bind(CharacterClassData cls, int completed, int total, Action clicked){
            DeckUI.Image(classIcon, cls.classIcon); DeckUI.Image(art, cls.puzzleArt);
            DeckUI.Text(className, cls.DisplayName);

            if(className) className.color = cls.color;
            DeckUI.Text(count, completed + "/" + total);
            if(progress){
                progress.minValue = 0; progress.maxValue = Mathf.Max(1, total);
                progress.wholeNumbers = true; progress.SetValueWithoutNotify(completed);
                progress.interactable = false;
            }
            
            if(completedImage) completedImage.SetActive(total > 0 && completed == total);
            if(button){
                button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => clicked?.Invoke());
            }
        }
    }
}
