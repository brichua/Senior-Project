using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VocaloidTCG
{
    public sealed class DeckEditorUI : MonoBehaviour
    {
        [Serializable] public sealed class ClassButton { public CharacterClassData characterClass; public Button button; }
        public DeckCatalog catalog;
        public DeckSelectUI deckSelect;
        public GameObject editorRoot, selectRoot;
        public TMP_InputField nameInput;
        public Button saveName, back, saveDeck, clearDeck, minus, plus, addToDeck, previousBack, nextBack;
        public ClassButton[] classButtons;
        public Image classSelectionImage;
        public Sprite zeroClasses, oneClass, twoClasses;
        public Transform collectionRoot, deckCardsRoot;
        public CardPreviewView collectionPrefab;
        public DeckCardView deckRowPrefab;
        public Image vipBorder, vipIcon, vipArt;
        public TMP_Text vipName, vipCount, vipText;
        [Header("Card back selection")]
        public Button[] cardBackButtons;
        public Image selectedCardBack;
        public TMP_Text deckCount, deckStats, status;
        public GameObject cardInformation;
        public TMP_Text cardType, cardName, cardClass, cardCost, cardInfluence, cardDescription, copiesText, addCount;
        public Image cardArt, cardVIP, costImage, influenceImage;
        public Slider copiesSlider;
        private DeckLibrary library;
        private DeckRecord draft;
        private CardData selected;
        private int pendingCopies, backOffset;
        private readonly List<CardPreviewView> collectionViews = new List<CardPreviewView>();
        private readonly List<DeckCardView> deckViews = new List<DeckCardView>();
        private bool initialized;
        private Sprite emptyVIPIcon;
        private Color emptyVIPBorderColor;
        private Color emptyVIPTextColor;

        private void Awake() {
            Initialize();
        }

        private bool Initialize(){
            if(initialized) return library != null;
            initialized = true;
            StatusTextFade.Show(status, "");
            emptyVIPIcon = vipIcon ? vipIcon.sprite : null;
            emptyVIPBorderColor = vipBorder ? vipBorder.color : Color.white;
            emptyVIPTextColor = vipText ? vipText.color : Color.white;
            RenderVIP(null);

            try{
                library = DeckLibrary.Get(catalog); catalog = library.Catalog;
            }
            catch(Exception ex){
                StatusTextFade.Show(status, ex.Message); Debug.LogError(ex.Message, this); return false;
            }

            if(nameInput) nameInput.characterLimit = 15;
            Hook(saveName, SaveName); Hook(back, Back); Hook(saveDeck, Save); Hook(clearDeck, Clear);
            Hook(minus, () => Adjust(-1)); Hook(plus, () => Adjust(1)); Hook(addToDeck, ApplyCopies);
            Hook(previousBack, () => PageBacks(-3)); Hook(nextBack, () => PageBacks(3));

            if(copiesSlider) {
                copiesSlider.wholeNumbers = true; copiesSlider.minValue = 0;
                copiesSlider.onValueChanged.AddListener(v => { pendingCopies = Mathf.RoundToInt(v); RenderCard(); });
            }

            foreach(var entry in classButtons ?? new ClassButton[0]){
                var captured = entry;
                if(captured != null && captured.characterClass) Hook(captured.button, () => ToggleClass(captured.characterClass.identity));
            }

            for(int i = 0; i < (cardBackButtons?.Length ?? 0); i++){
                int slot = i; Hook(cardBackButtons[i], () => ChooseBack(slot));
            }
            return true;
        }
        private static void Hook(Button button, UnityEngine.Events.UnityAction action) { if(button) button.onClick.AddListener(action); }

        public void OpenNew(){
            Open(null);
        }

        public void Open(DeckRecord deck){
            if(!Initialize()) return;
            draft = deck?.Copy() ?? new DeckRecord { cardBackId = catalog.defaultCardBacks.First(b => b).id };
            selected = null; backOffset = 0;
            if(nameInput) nameInput.SetTextWithoutNotify(draft.deckName);
            if(editorRoot) editorRoot.SetActive(true); else gameObject.SetActive(true);
            if(selectRoot) selectRoot.SetActive(false);
            StatusTextFade.Show(status, ""); Render();
        }

        public void SaveName(){
            if(draft == null) return;
            draft.deckName = nameInput ? nameInput.text.Trim() : draft.deckName;
            if(draft.deckName.Length > 15) draft.deckName = draft.deckName.Substring(0, 15);
            if(nameInput) nameInput.SetTextWithoutNotify(draft.deckName);
            StatusTextFade.Show(status, "Name applied. Save deck to keep changes.");
        }

        public void Save(){
            if(draft == null) return;
            SaveName(); string error;
            if(!library.SaveDeck(draft, out error)){
                StatusTextFade.Show(status, error); return;
            }
            Back();
        }
        
        public void Back(){
            draft = null;
            if(selectRoot) selectRoot.SetActive(true);
            if(deckSelect) deckSelect.Refresh();
            if(editorRoot) editorRoot.SetActive(false); else gameObject.SetActive(false);
        }

        public void Clear(){
            if(draft == null) return;
            draft.cardIds.Clear(); draft.vipCardId = ""; pendingCopies = 0; Render();
        }

        public void ToggleClass(CharacterClass identity){
            if(draft == null) return;

            if(draft.classes.Contains(identity)){
                draft.classes.Remove(identity);
                draft.cardIds.RemoveAll(id => !catalog.Card(id) || catalog.Card(id).cardClass.identity == identity);
                var vip = catalog.Card(draft.vipCardId);
                if(vip && vip.cardClass.identity == identity) draft.vipCardId = "";
                selected = null;
            }else{
                if(draft.classes.Count >= 2){
                    StatusTextFade.Show(status, "Only two classes can be selected.");
                    return;
                }
                draft.classes.Add(identity);
            }
            var backs = catalog.Backs(draft.classes);
            if(!backs.Any(b => b.id == draft.cardBackId)) draft.cardBackId = backs[0].id;
            backOffset = 0; StatusTextFade.Show(status, "");
            Render();
        }

        private int Maximum => !selected ? 0 : !selected.vip ? 3 : string.IsNullOrEmpty(draft.vipCardId) || draft.vipCardId == selected.id ? 1 : 0;

        private void SelectCard(CardData card){
            selected = card; pendingCopies = draft.Copies(card); RenderCard();
        }

        private void Adjust(int change){
            if(draft == null) return; pendingCopies = Mathf.Clamp(pendingCopies + change, 0, Maximum); RenderCard();
        }

        private void ApplyCopies(){
            if(draft == null || !selected) return;
            if(!DeckRules.SetCopies(draft, selected, pendingCopies, library.Owned)) StatusTextFade.Show(status, "Only one VIP may be added. Remove the current VIP first.");
            else StatusTextFade.Show(status, "");
            Render();
        }

        private void RenderCard(){
            if(cardInformation) cardInformation.SetActive(selected);
            if(!selected) return;

            DeckUI.Text(cardType, selected.stageEffect ? "Stage Effect" : "Performer");
            var cls = selected.cardClass;
            DeckUI.Text(cardName, selected.cardName); DeckUI.Text(cardClass, cls ? cls.DisplayName : "");
            if(cardClass) cardClass.color = cls ? cls.color : Color.white;
            DeckUI.Text(cardCost, "cost: " + selected.cost); DeckUI.Text(cardInfluence, "inf: " + selected.influence);
            DeckUI.Text(cardDescription, selected.info); DeckUI.Image(cardArt, selected.cardImage);
            DeckUI.Image(cardVIP, selected.vip && cls ? cls.vipImage : null);
            DeckUI.Image(costImage, cls ? cls.costImage : null);
            DeckUI.Image(influenceImage, cls ? cls.popupImage : null);
            pendingCopies = Mathf.Clamp(pendingCopies, 0, Maximum);
            DeckUI.Text(copiesText, draft.Copies(selected) + (selected.vip ? "/1 in deck" : "/3 in deck"));
            DeckUI.Text(addCount, pendingCopies.ToString());

            if(copiesSlider) { copiesSlider.maxValue = selected.vip ? 1 : 3; copiesSlider.SetValueWithoutNotify(pendingCopies); copiesSlider.interactable = Maximum > 0; }
            if(minus) minus.interactable = pendingCopies > 0;
            if(plus) plus.interactable = pendingCopies < Maximum;
            if(addToDeck) addToDeck.interactable = Maximum > 0 || draft.Copies(selected) > 0;
        }

        private void PageBacks(int direction){
            if(draft == null) return;
            int count = catalog.Backs(draft.classes).Count;
            backOffset = ((backOffset + direction) % count + count) % count; RenderBacks();
        }
        
        private void ChooseBack(int slot){
            if(draft == null) return;
            var backs = catalog.Backs(draft.classes); draft.cardBackId = backs[(backOffset + slot) % backs.Count].id; RenderBacks();
        }

        private void RenderBacks(){
            var backs = catalog.Backs(draft.classes);
            for(int i = 0; i < (cardBackButtons?.Length ?? 0); i++){
                if(cardBackButtons[i]) DeckUI.Image(cardBackButtons[i].image, backs[(backOffset + i) % backs.Count].image);
            }
            DeckUI.Image(selectedCardBack, catalog.Back(draft));
        }

        private void RenderVIP(CardData vip){
            var cls = vip ? vip.cardClass : null;
            if(vipText) vipText.color = cls ? cls.color : emptyVIPTextColor;
            
            if(vipBorder){
                vipBorder.gameObject.SetActive(true);
                vipBorder.color = cls ? cls.color : emptyVIPBorderColor;
            }
            
            if(vipIcon){
                vipIcon.gameObject.SetActive(true);
                vipIcon.enabled = true;
                vipIcon.sprite = cls && cls.vipImage ? cls.vipImage : emptyVIPIcon;
            }

            DeckUI.Image(vipArt, vip ? vip.iconImage : null);
            if(vipArt) vipArt.gameObject.SetActive(vip != null);
            DeckUI.Text(vipName, vip ? vip.cardName : "");
            if(vipName) vipName.gameObject.SetActive(vip != null);
            if(vipCount) vipCount.gameObject.SetActive(vip != null);
        }

        private static void ClearViews<T>(List<T> views) where T : MonoBehaviour{
            foreach(var view in views){
                if(view){
                    view.gameObject.SetActive(false); Destroy(view.gameObject);
                }
            }
            views.Clear();
        }

        private void Render(){
            if(draft == null) return;

            foreach(var entry in classButtons ?? new ClassButton[0]) if(entry != null && entry.button && entry.characterClass)
                DeckUI.Image(entry.button.image, draft.classes.Contains(entry.characterClass.identity) ? entry.characterClass.selectedButtonImage : entry.characterClass.buttonImage);
            
            DeckUI.Image(classSelectionImage, draft.classes.Count == 0 ? zeroClasses : draft.classes.Count == 1 ? oneClass : twoClasses);
            ClearViews(collectionViews);
            
            if(collectionPrefab && collectionRoot) foreach(var card in catalog.cards.Where(c => DeckRules.Allowed(c, draft, library.Owned))){
                var view = Instantiate(collectionPrefab, collectionRoot); view.Bind(card, draft.Copies(card), () => SelectCard(card)); collectionViews.Add(view);
            }

            ClearViews(deckViews);
            if(deckRowPrefab && deckCardsRoot) foreach(var id in draft.cardIds.Distinct()){
                var card = catalog.Card(id); if(!card || card.vip) continue;
                var view = Instantiate(deckRowPrefab, deckCardsRoot); view.Bind(card, draft.Copies(card)); deckViews.Add(view);
            }

            RenderVIP(catalog.Card(draft.vipCardId));
            DeckUI.Count(deckCount, draft.Count); DeckUI.Text(deckStats, DeckRules.Stats(draft, catalog, false));
            if(saveDeck) saveDeck.interactable = draft.Count <= 30;
            RenderBacks(); RenderCard();
        }
    }
}
