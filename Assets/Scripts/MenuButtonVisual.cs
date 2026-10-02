using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VocaloidTCG
{
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class MenuButtonVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Image buttonImage;
        public Sprite normalSprite, lockedSprite;
        [Tooltip("A separate child object, not the button itself.")]
        public GameObject hoverImage;

        [Header("Hover movement")]
        [Tooltip("The element that moves when hovered. Defaults to this button's RectTransform.")]
        public RectTransform movingTransform;
        [Min(0f)] public float hoverOffset = 12f;
        [Min(0f)] public float hoverMoveSpeed = 100f;

        [Header("Locked state")]
        [Tooltip("The button's main text. Its original color is restored when unlocked.")]
        public TMP_Text buttonText;
        public Color lockedTextColor = new Color32(0x59, 0x5D, 0x8A, 0xFF);
        public GameObject lockedSubText;
        public GameObject lockedIcon;

        private Button button;
        private Vector2 originalPosition;
        private Color originalTextColor = Color.white;
        private bool hovered;
        private bool locked;

        private void Awake()
        {
            button = GetComponent<Button>();
            if (!buttonImage) buttonImage = button.image;
            if (!normalSprite && buttonImage) normalSprite = buttonImage.sprite;
            if (!movingTransform) movingTransform = transform as RectTransform;
            if (movingTransform) originalPosition = movingTransform.anchoredPosition;
            if (!buttonText) buttonText = GetComponentInChildren<TMP_Text>(true);
            if (buttonText) originalTextColor = buttonText.color;
            // SpriteSwap would otherwise replace the locked or normal sprite.
            if (button.transition == Selectable.Transition.SpriteSwap)
                button.transition = Selectable.Transition.None;
            if (hoverImage && hoverImage != gameObject)
                foreach (Graphic graphic in hoverImage.GetComponentsInChildren<Graphic>(true))
                    graphic.raycastTarget = false;
            locked = !button.interactable;
            RefreshVisuals();
        }

        private void Update()
        {
            if (!movingTransform) return;

            Vector2 target = originalPosition + (hovered && !locked ? Vector2.left * hoverOffset : Vector2.zero);
            if (hoverMoveSpeed <= 0f)
                movingTransform.anchoredPosition = target;
            else
                movingTransform.anchoredPosition = Vector2.MoveTowards(
                    movingTransform.anchoredPosition, target, hoverMoveSpeed * Time.unscaledDeltaTime);
        }

        public void SetLocked(bool locked)
        {
            if (!button) return;
            this.locked = locked;
            if (locked) hovered = false;
            button.interactable = !locked;
            if (buttonImage)
                buttonImage.sprite = locked && lockedSprite ? lockedSprite : normalSprite;
            RefreshVisuals();
        }

        public void OnPointerEnter(PointerEventData eventData) { hovered = !locked; RefreshVisuals(); }
        public void OnPointerExit(PointerEventData eventData) { hovered = false; RefreshVisuals(); }
        private void OnDisable()
        {
            hovered = false;
            if (movingTransform) movingTransform.anchoredPosition = originalPosition;
            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            if (hoverImage && hoverImage != gameObject)
                hoverImage.SetActive(isActiveAndEnabled && hovered && !locked);
            if (buttonText)
                buttonText.color = locked ? lockedTextColor : originalTextColor;
            if (lockedSubText && lockedSubText != gameObject)
                lockedSubText.SetActive(locked);
            if (lockedIcon && lockedIcon != gameObject)
                lockedIcon.SetActive(locked);
        }
    }
}
