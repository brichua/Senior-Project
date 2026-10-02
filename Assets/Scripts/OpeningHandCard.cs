using UnityEngine;
using UnityEngine.EventSystems;

namespace VocaloidTCG.BoardUI
{
    public sealed class OpeningHandCard : MonoBehaviour, IBeginDragHandler, IDragHandler,
        IEndDragHandler, IPointerClickHandler, IPointerDownHandler
    {
        public MatchOpeningAnimator owner;
        public int index;
        public bool opponent, selected;
        public CardPointer view;
        public RectTransform Rect => (RectTransform)transform;
        private bool dragged;

        public void OnPointerDown(PointerEventData e){ dragged = false; }
        public void OnBeginDrag(PointerEventData e){
            if(opponent || !owner.CanSelect || e.button != PointerEventData.InputButton.Left) return;
            dragged = true; transform.SetAsLastSibling();
        }
        public void OnDrag(PointerEventData e){
            if(!dragged || !owner.CanSelect) return;
            Vector2 point;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform.parent,
                e.position, e.pressEventCamera, out point);
            Rect.anchoredPosition = point;
        }
        public void OnEndDrag(PointerEventData e){
            if(!dragged || !owner.CanSelect) return;
            owner.SelectCard(this, RectTransformUtility.RectangleContainsScreenPoint(owner.RedrawZone, e.position, e.pressEventCamera));
        }
        public void OnPointerClick(PointerEventData e){
            if(!dragged && !opponent && owner.CanSelect && e.button == PointerEventData.InputButton.Left)
                owner.SelectCard(this, !selected);
        }
    }
}
