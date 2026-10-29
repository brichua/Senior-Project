using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VocaloidTCG.BoardUI
{
    public sealed class RevealedCardView : MonoBehaviour
    {
        public Image art;
        public TMP_Text cardName;

        public void Bind(Sprite sprite, string title)
        {
            if (art) { art.sprite = sprite; art.enabled = sprite; }
            if (cardName) { cardName.text = title; cardName.richText = false; }
        }
    }
}
