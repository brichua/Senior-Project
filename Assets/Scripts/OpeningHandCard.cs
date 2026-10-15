using UnityEngine;
using UnityEngine.EventSystems;

namespace VocaloidTCG.BoardUI
{
    public sealed class OpeningHandCard : MonoBehaviour, IBeginDragHandler, IDragHandler,
        IEndDragHandler, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        public MatchOpeningAnimator owner;
        public int index;
        public bool opponent, selected;
        public CardPointer view;
        public RectTransform Rect => (RectTransform)transform;
        private bool dragged;
        private bool pressed;

        public void OnPointerEnter(PointerEventData e){ if(owner && !pressed) owner.ShowCardInfo(this); }
        public void OnPointerExit(PointerEventData e){ if(owner) owner.HideCardInfo(this); }
        public void OnPointerDown(PointerEventData e){
            if(e.button != PointerEventData.InputButton.Left) return;
            dragged = false; pressed = true;
            if(owner) owner.HideCardInfo(this);
        }
        public void OnPointerUp(PointerEventData e){ if(e.button == PointerEventData.InputButton.Left) pressed = false; }
        public void OnBeginDrag(PointerEventData e){
            if(opponent || !owner.CanSelect || e.button != PointerEventData.InputButton.Left) return;
            dragged = true; transform.SetAsLastSibling();
            owner.BeginCardDrag(this);
        }
        public void OnDrag(PointerEventData e){
            if(!dragged || !owner.CanSelect) return;
            Vector2 point;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform.parent,
                e.position, e.pressEventCamera, out point);
            Rect.anchoredPosition = point;
        }
        public void OnEndDrag(PointerEventData e){
            owner.EndCardDrag(this);
            if(!dragged || !owner.CanSelect) return;
            owner.SelectCard(this, RectTransformUtility.RectangleContainsScreenPoint(owner.RedrawZone, e.position, e.pressEventCamera));
        }
        public void OnPointerClick(PointerEventData e){
            if(!dragged && !opponent && owner.CanSelect && e.button == PointerEventData.InputButton.Left)
                owner.SelectCard(this, !selected);
        }
        private void OnDisable(){
            pressed = dragged = false;
            if(owner){ owner.HideCardInfo(this); owner.EndCardDrag(this); }
        }
    }
}
