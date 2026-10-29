using TMPro;
using UnityEngine;

namespace VocaloidTCG.BoardUI
{
    public sealed class RevealedCardsView : MonoBehaviour
    {
        public RectTransform panel, cardsRoot;
        public CanvasGroup group;
        public TMP_Text message;
        [Tooltip("The editable card layout. Runtime reveals make one copy per revealed card.")]
        public RevealedCardView cardTemplate;
        [Min(0)] public float visibleSeconds = 5f;
        [Min(0.01f)] public float enterSeconds = 0.18f;
        [Min(0.01f)] public float exitSeconds = 0.2f;

        public void Bind(string text, Sprite[] art, string[] names)
        {
            if (message) { message.text = string.IsNullOrWhiteSpace(text) ? "Revealed cards" : text; message.richText = false; }
            cardTemplate.gameObject.SetActive(false);
            for (int i = 0; i < art.Length; i++)
            {
                var card = Instantiate(cardTemplate, cardsRoot);
                card.Bind(art[i], names[i]);
                card.gameObject.SetActive(true);
            }
            group.interactable = false; group.blocksRaycasts = false;
            foreach (var graphic in GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) graphic.raycastTarget = false;
        }
    }
}
