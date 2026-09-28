using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace VocaloidTCG
{
    public sealed class DeckSelectUI : MonoBehaviour
    {
        public DeckCatalog catalog;
        public DeckEditorUI editor;
        public Button allDecks, favorites, newDeck, editDeck, deleteDeck, back;
        [Header("All Decks button artwork")]
        [FormerlySerializedAs("filterNormal")] public Sprite allDecksNormal;
        [FormerlySerializedAs("filterSelected")] public Sprite allDecksSelected;
        public Sprite favoritesNormal, favoritesSelected;
        public TMP_Text allDecksText, favoritesText;
        public Color normalFilterTextColor = Color.white;
        public Color highlightedFilterTextColor = Color.yellow;
        public TMP_Text allCount, favoritesCount, status;
        public Transform gridRoot, detailCardsRoot;
        public DeckTileView deckPrefab;
        public DeckCardIconView detailCardPrefab;
        public GameObject information;
        public TMP_Text deckName, count, vipName, vipText, stats;
        public Image cover, firstClass, secondClass, vipArt, vipBorder, vipIcon;
        [SerializeField, HideInInspector, FormerlySerializedAs("vipClass")]
        private TMP_Text legacyVIPClassText;
        [SerializeField, HideInInspector, FormerlySerializedAs("deckClasses")]
        private TMP_Text legacyDeckClassesText;
        public string mainMenuScene = "Main Menu";
        private DeckLibrary library;
        private bool favoritesOnly;
        private readonly List<DeckTileView> tiles = new List<DeckTileView>();
        private readonly List<DeckCardIconView> cards = new List<DeckCardIconView>();
        private Color defaultVIPTextColor;
        private void Awake()
        {
            StatusTextFade.Show(status, "");
            defaultVIPTextColor = vipText ? vipText.color : Color.white;
            if(legacyVIPClassText) { legacyVIPClassText.text = ""; legacyVIPClassText.enabled = false; }
            if(legacyDeckClassesText) { legacyDeckClassesText.text = ""; legacyDeckClassesText.enabled = false; }
            RenderVIPBorder(null);
            SetDeckActionsVisible(false);

            try{
                library = DeckLibrary.Get(catalog); catalog = library.Catalog;
            }catch(Exception ex){
                StatusTextFade.Show(status, ex.Message); Debug.LogError(ex.Message, this); return;
            }
            if(allDecks) allDecks.onClick.AddListener(() => { favoritesOnly = false; Refresh(); });
            if(favorites) favorites.onClick.AddListener(() => { favoritesOnly = true; Refresh(); });
            if(newDeck) newDeck.onClick.AddListener(() => { if(editor) editor.OpenNew(); });
            if(editDeck) editDeck.onClick.AddListener(() => { if(editor && library.Selected != null) editor.Open(library.Selected); });
            if(deleteDeck) deleteDeck.onClick.AddListener(Delete);
            if(back) back.onClick.AddListener(() => SceneManager.LoadScene(mainMenuScene));
        }

        private void OnEnable(){
            if(library != null) { library.Changed += Refresh; Refresh();
            }
        }

        private void OnDisable(){
            if(library != null) library.Changed -= Refresh;
        }

        private void Choose(string id){
            string error; library.Select(id, out error); StatusTextFade.Show(status, error);
        }
        
        private void Favorite(string id){
            string error; library.Favorite(id, out error); StatusTextFade.Show(status, error);
        }
        
        private void Delete(){
            if(library.Selected == null) return; string error; library.Delete(library.Selected.id, out error); StatusTextFade.Show(status, error);
        }
       
       private void SetDeckActionsVisible(bool selected){
            if(editDeck){
                editDeck.interactable = selected; editDeck.gameObject.SetActive(selected);
            }
            if(deleteDeck){
                deleteDeck.interactable = selected;
                deleteDeck.gameObject.SetActive(selected);
            }
        }

        private void RenderVIPBorder(CardData vip){
            var cls = vip ? vip.cardClass : null;
            if(vipBorder){
                vipBorder.color = cls ? cls.color : Color.white;
                vipBorder.gameObject.SetActive(vip != null);
            }

            DeckUI.Image(vipIcon, cls ? cls.vipImage : null);
            if(vipIcon) vipIcon.gameObject.SetActive(vip != null);
            if(vipText) vipText.color = cls ? cls.color : defaultVIPTextColor;
        }

        public void Refresh(){
            if(library == null){
                SetDeckActionsVisible(false); RenderVIPBorder(null); return;
            }

            var decks = library.Decks; var selected = library.Selected;
            DeckUI.Text(allCount, decks.Count.ToString()); DeckUI.Text(favoritesCount, decks.Count(d => d.favorite).ToString());

            if(allDecks) DeckUI.Image(allDecks.image, favoritesOnly ? allDecksNormal : allDecksSelected);
            if(favorites) DeckUI.Image(favorites.image, favoritesOnly ? favoritesSelected : favoritesNormal);
            
            var allColor = favoritesOnly ? normalFilterTextColor : highlightedFilterTextColor;
            var favoritesColor = favoritesOnly ? highlightedFilterTextColor : normalFilterTextColor;
            
            if(allDecksText) allDecksText.color = allColor;
            if(allCount) allCount.color = allColor;
            if(favoritesText) favoritesText.color = favoritesColor;
            if(favoritesCount) favoritesCount.color = favoritesColor;
            
            foreach(var tile in tiles) if(tile){
                tile.gameObject.SetActive(false); Destroy(tile.gameObject);
            }
            tiles.Clear();
            if(deckPrefab && gridRoot) foreach(var deck in decks.Where(d => !favoritesOnly || d.favorite)){
                var tile = Instantiate(deckPrefab, gridRoot); tile.gameObject.SetActive(true);
                tile.Bind(deck, catalog, selected != null && selected.id == deck.id, () => Choose(deck.id), () => Favorite(deck.id)); tiles.Add(tile);
            }

            if(newDeck) newDeck.transform.SetAsLastSibling();
            SetDeckActionsVisible(selected != null);
            
            if(information) information.SetActive(selected != null);
            foreach(var card in cards) if(card){
                card.gameObject.SetActive(false); Destroy(card.gameObject);
            }
            cards.Clear();
            if(selected == null){
                RenderVIPBorder(null); return;
            }
            
            DeckUI.Text(deckName, selected.deckName); DeckUI.Count(count, selected.Count);
            DeckUI.Classes(firstClass, secondClass, selected, catalog); DeckUI.Image(cover, catalog.Cover(selected)?.cardImage);
            var vip = catalog.Card(selected.vipCardId);
            DeckUI.Image(vipArt, vip ? (vip.iconImage ? vip.iconImage : vip.cardImage) : null);
            DeckUI.Text(vipName, vip ? vip.cardName : "");
            RenderVIPBorder(vip);
            DeckUI.Text(stats, DeckRules.Stats(selected, catalog, true));
            
            if(detailCardPrefab && detailCardsRoot) foreach(var id in selected.cardIds.Concat(new[] { selected.vipCardId }).Distinct()){
                var card = catalog.Card(id); if(!card) continue;
                var view = Instantiate(detailCardPrefab, detailCardsRoot); view.Bind(card, selected.Copies(card)); cards.Add(view);
            }
        }
    }
}
