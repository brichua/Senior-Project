using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VocaloidTCG.BoardUI
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class CardPointer : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler,
        IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Image art;
        public Image influencePopup;
        public TMP_Text cardName, influence, cost;
        public GameObject detailsRoot;
        public RectTransform visual;
        public float hoverLift = 24f;
        private CanvasGroup group;
        private Vector2 rest;
        private BoardUIController board;
        private CardState card;
        private int column = -1, row = -1;
        private bool inHand, movable, dragging;
        private Canvas handCanvas;
        private bool originalOverrideSorting;
        private int originalSortingOrder, originalSortingLayer;

        private void Awake(){
            group = GetComponent<CanvasGroup>();
            if(visual) rest = visual.anchoredPosition;
        }

        public void Bind(BoardUIController owner, CardState state, Sprite sprite, bool hand, bool canMove, int x = -1, int y = -1){
            board = owner; card = state; inHand = hand; movable = canMove; column = x; row = y;
            if(!art) art = GetComponent<Image>();
            BoardUIController.SetImage(art, sprite);
            RenderDetails(state);
            if(inHand && state != null) PrepareHandHover();
            gameObject.SetActive(state != null || sprite != null);
        }

        private void PrepareHandHover(){
            if(handCanvas || !visual || visual == transform) return;
            handCanvas = visual.GetComponent<Canvas>();
            if(!handCanvas) handCanvas = visual.gameObject.AddComponent<Canvas>();
            originalOverrideSorting = handCanvas.overrideSorting;
            originalSortingOrder = handCanvas.sortingOrder;
            originalSortingLayer = handCanvas.sortingLayerID;

            // Keep mouse targets in the layout's original order and position.
            // Raising the visual must not steal the next card's hover strip.
            if(!GetComponent<Graphic>()){
                var hitTarget = gameObject.AddComponent<Image>();
                hitTarget.color = Color.clear;
                hitTarget.raycastTarget = true;
            }
            foreach(var graphic in visual.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }

        public void ResetHandHover(){
            if(visual) visual.anchoredPosition = rest;
            if(!handCanvas) return;
            handCanvas.sortingLayerID = originalSortingLayer;
            handCanvas.sortingOrder = originalSortingOrder;
            handCanvas.overrideSorting = originalOverrideSorting;
        }

        private void RenderDetails(CardState state){
            bool revealed = state != null && state.data;
            bool performer = revealed && state.data.performer;
            if(detailsRoot){
                bool containsArt = art && art.transform.IsChildOf(detailsRoot.transform);
                detailsRoot.SetActive(revealed || containsArt);
            }
            PutDetail(cardName, revealed ? state.data.cardName : "", revealed);
            PutDetail(cost, revealed ? state.currentCost.ToString() : "", revealed);
            if(influencePopup){
                influencePopup.gameObject.SetActive(performer);
                influencePopup.sprite = state != null && state.data ? state.data.popupImage : null;
            }
            PutDetail(influence, performer ? state.currentInfluence.ToString() : "", performer);
        }

        private static void PutDetail(TMP_Text label, string text, bool visible){
            if(!label) return;
            label.richText = false;
            CardInfoView.Put(label, text ?? "");
            label.gameObject.SetActive(visible);
        }

        public void MakeDragPreview(){
            if(!group) group = GetComponent<CanvasGroup>();
            group.interactable = false; group.blocksRaycasts = false;
            foreach(var graphic in GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        }

        public void OnPointerClick(PointerEventData e){
            if(e.button == PointerEventData.InputButton.Left && !dragging && card != null)
                board.Select(card);
        }

        public void OnPointerEnter(PointerEventData e){
            if(!inHand || card == null || dragging || !visual || !board || !board.CanHoverHand) return;
            board.ClearHandHover();
            visual.anchoredPosition = rest + Vector2.up * hoverLift;
            if(handCanvas){
                var parentCanvas = transform.GetComponentInParent<Canvas>();
                if(parentCanvas){
                    handCanvas.overrideSorting = true;
                    handCanvas.sortingLayerID = parentCanvas.sortingLayerID;
                    handCanvas.sortingOrder = parentCanvas.sortingOrder + 1;
                }
            }
        }

        public void OnPointerExit(PointerEventData e){
            ResetHandHover();
        }

        public void OnBeginDrag(PointerEventData e){
            if(e.button != PointerEventData.InputButton.Left || !movable || card == null) return;
            dragging = board.BeginDrag(this, card, inHand, column, row, e);
            if(dragging)
            {
                group.alpha = 0; group.blocksRaycasts = false;
                ResetHandHover();
            }
        }

        public void OnDrag(PointerEventData e){
            if(dragging) board.MoveGhost(e);
        }

        public void OnEndDrag(PointerEventData e){
            if(dragging) board.CancelDrag();
        }
        
        public void Restore(){
            dragging = false;
            if(group){
                group.alpha = 1; group.blocksRaycasts = true;
            }
            ResetHandHover();
        }

        private void OnDisable(){
            if(dragging && board) board.CancelDrag();
            Restore();
        }
    }
}
