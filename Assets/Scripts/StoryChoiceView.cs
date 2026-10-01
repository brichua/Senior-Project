using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VocaloidTCG
{
    public sealed class StoryChoiceView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler
    {
        public Button button;
        public TMP_Text title, count;
        public Image art, classIcon, border, completionImage;
        public Sprite completedSprite;
        public Slider progress;
        public GameObject completed, highlight;
        private bool selected, hovered, focused;
        private UnityEngine.Events.UnityAction click;
        private Sprite defaultCompletion;
        private bool captured;

        public void Bind(Action action, bool interactable = true){
            if(!captured){
                defaultCompletion = completionImage ? completionImage.sprite : null;
                captured = true;
            }

            if(!button) button = GetComponent<Button>();
            if(button){
                if(click != null) button.onClick.RemoveListener(click);
                click = () => action?.Invoke();
                button.onClick.AddListener(click); button.interactable = interactable;
            }

            RefreshHighlight();
        }

        public void SetClass(CharacterClassData data){
            if(!data) return;
            DeckUI.Text(title, data.DisplayName);
            if(title) title.color = data.color;
            DeckUI.Image(classIcon, data.classIcon);
            DeckUI.Image(art, data.storyArt ? data.storyArt : data.puzzleArt);
            if(border) border.color = data.color;
        }

        public void SetProgress(int cleared, int total = 5){
            DeckUI.Text(count, cleared + "/" + total);
            if(progress){
                progress.minValue = 0;
                progress.maxValue = total;
                progress.interactable = false;
                progress.SetValueWithoutNotify(cleared);
            }

            bool done = cleared >= total;
            if(completed) completed.SetActive(done);
            if(completionImage) completionImage.sprite = done && completedSprite ? completedSprite : defaultCompletion;
        }

        public void SetSelected(bool value){
            selected = value;
            RefreshHighlight();
        }

        public void OnPointerEnter(PointerEventData e){
            hovered = true;
            RefreshHighlight();
        }

        public void OnPointerExit(PointerEventData e){
            hovered = false;
            RefreshHighlight();
        }

        public void OnSelect(BaseEventData e){
            focused = true;
            RefreshHighlight();
        }

        public void OnDeselect(BaseEventData e){
            focused = false;
            RefreshHighlight();
        }

        private void OnDisable(){
            hovered = focused = false;
            RefreshHighlight();
        }

        private void RefreshHighlight(){
            if(highlight) highlight.SetActive((selected || hovered || focused) && (!button || button.interactable));
        }
    }
}
