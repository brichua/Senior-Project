using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace VocaloidTCG
{
    public sealed class FavoriteButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Action<bool> changed;
        private bool hovered;
        public void Configure(Action<bool> callback) { changed = callback; changed?.Invoke(hovered); }
        public void OnPointerEnter(PointerEventData eventData) { hovered = true; changed?.Invoke(true); }
        public void OnPointerExit(PointerEventData eventData) { hovered = false; changed?.Invoke(false); }
        private void OnDisable() { hovered = false; changed?.Invoke(false); }
    }
}
