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
        public RectTransform visual;
        public float hoverLift = 24f;
        private CanvasGroup group;
        private Vector2 rest;
        private BoardUIController board;
        private CardState card;
        private int column = -1, row = -1;
        private bool inHand, movable, dragging;

        private void Awake(){
            group = GetComponent<CanvasGroup>();
            if(visual) rest = visual.anchoredPosition;
        }

        public void Bind(BoardUIController owner, CardState state, Sprite sprite, bool hand, bool canMove, int x = -1, int y = -1){
            board = owner; card = state; inHand = hand; movable = canMove; column = x; row = y;
            BoardUIController.SetImage(art, sprite);
            gameObject.SetActive(state != null || sprite != null);
        }

        public void OnPointerClick(PointerEventData e){
            if(e.button == PointerEventData.InputButton.Left && !dragging && card != null)
                board.Select(card);
        }

        public void OnPointerEnter(PointerEventData e){
            if(inHand && card != null && !dragging && visual) visual.anchoredPosition = rest + Vector2.up * hoverLift;
        }

        public void OnPointerExit(PointerEventData e){
            if(visual) visual.anchoredPosition = rest;
        }

        public void OnBeginDrag(PointerEventData e){
            if(e.button != PointerEventData.InputButton.Left || !movable || card == null) return;
            dragging = board.BeginDrag(this, card, inHand, column, row, e);
            if(dragging)
            {
                group.alpha = 0; group.blocksRaycasts = false;
                if(visual) visual.anchoredPosition = rest;
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
            if(visual) visual.anchoredPosition = rest;
        }

        private void OnDisable(){
            if(dragging && board) board.CancelDrag();
            Restore();
        }
    }
}
