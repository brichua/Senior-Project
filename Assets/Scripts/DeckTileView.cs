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

        public void Bind(DeckRecord deck, DeckCatalog catalog, bool selected, Action choose, Action toggleFavorite){
            favorite = deck.favorite;
            DeckUI.Text(deckName, deck.deckName); DeckUI.Count(count, deck.Count);
            DeckUI.Classes(firstClass, secondClass, deck, catalog);
            DeckUI.Image(cover, catalog.Cover(deck)?.cardImage); DeckUI.Image(cardBack, catalog.Back(deck));
            
            if(selectedIcon) selectedIcon.SetActive(selected);
            SetFavorite(false);
            
            if(selectButton){
                selectButton.onClick.RemoveAllListeners(); selectButton.onClick.AddListener(() => choose());
            }

            if(favoriteButton){
                favoriteButton.onClick.RemoveAllListeners(); favoriteButton.onClick.AddListener(() => toggleFavorite());
                var hover = favoriteButton.GetComponent<FavoriteButtonHover>();
                if(!hover) hover = favoriteButton.gameObject.AddComponent<FavoriteButtonHover>();
                hover.Configure(SetFavorite);
            }
        }

        private void SetFavorite(bool hover){
            DeckUI.Image(favoriteImage, hover ? highlightedFavorite : favorite && favoritedSprite ? favoritedSprite : normalFavorite);
        }
    }
}
