using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VocaloidTCG
{
    public sealed class DeckTileView : MonoBehaviour
    {
        public Button selectButton, favoriteButton;
        public GameObject selectedIcon;
        public TMP_Text deckName, count;
        public Image firstClass, secondClass, cover, cardBack, favoriteImage;
        public Sprite normalFavorite, highlightedFavorite;
        public Sprite favoritedSprite;
        private bool favorite;

        public void Bind(DeckRecord deck, DeckCatalog catalog, bool selected, Action choose, Action toggleFavorite, Action inspect = null){
            favorite = deck.favorite;
            DeckUI.Text(deckName, deck.deckName); DeckUI.Count(count, deck.Count);
            DeckUI.Classes(firstClass, secondClass, deck, catalog);
            DeckUI.Image(cover, catalog.Cover(deck)?.cardImage); DeckUI.Image(cardBack, catalog.Back(deck));
            
            bool buttonIsIndicator = selectButton && selectedIcon &&
                selectButton.transform.IsChildOf(selectedIcon.transform);
            if(selectedIcon && !buttonIsIndicator) selectedIcon.SetActive(selected);
            SetFavorite(false);
            
            if(selectButton){
                selectButton.gameObject.SetActive(true);
                selectButton.interactable = true;
                if(buttonIsIndicator){
                    var label = selectButton.GetComponentInChildren<TMP_Text>(true);
                    DeckUI.Text(label, selected ? "selected" : "select");
                }
                selectButton.onClick.RemoveAllListeners(); selectButton.onClick.AddListener(() => choose());
            }

            var preview = GetComponent<Button>();
            if(!preview) preview = gameObject.AddComponent<Button>();
            preview.targetGraphic = GetComponent<Graphic>();
            preview.transition = Selectable.Transition.None;
            preview.onClick.RemoveAllListeners(); preview.onClick.AddListener(() => (inspect ?? choose)?.Invoke());

            if(favoriteButton){
                if(favoriteButton.transition == Selectable.Transition.SpriteSwap) favoriteButton.transition = Selectable.Transition.None;
                favoriteButton.onClick.RemoveAllListeners(); favoriteButton.onClick.AddListener(() => toggleFavorite());
                var hover = favoriteButton.GetComponent<FavoriteButtonHover>();
                if(!hover) hover = favoriteButton.gameObject.AddComponent<FavoriteButtonHover>();
                hover.Configure(SetFavorite);
            }
        }

        private void SetFavorite(bool hover){
            var highlighted = highlightedFavorite ? highlightedFavorite : favoritedSprite ? favoritedSprite : normalFavorite;
            DeckUI.Image(favoriteImage, favorite || hover ? highlighted : normalFavorite);
        }
    }
}
