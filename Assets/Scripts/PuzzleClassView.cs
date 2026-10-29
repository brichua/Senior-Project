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
        public CharacterClassData Class { get; private set; }

        public void Bind(CharacterClassData cls, int completed, int total, Action clicked){
            Class = cls;
            if (!button) button = GetComponent<Button>();
            // Decorative border buttons and the progress slider must not consume class clicks.
            foreach (var selectable in GetComponentsInChildren<Selectable>(true))
                if (selectable != button) selectable.enabled = false;
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = button && graphic.gameObject == button.gameObject;
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
                button.interactable = true;
                button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => clicked?.Invoke());
            }
        }

        public void SetSelected(bool selected)
        {
            if (!button) return;
            var colors = button.colors;
            colors.normalColor = selected ? new Color(0.82f, 0.88f, 1f) : Color.white;
            button.colors = colors;
        }
    }
}
