using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VocaloidTCG.BoardUI
{
    public sealed class ConcertPanelView : MonoBehaviour
    {
        public TMP_Text title, message;
        public Button primary, secondary, retrySave;
        public TMP_Text primaryLabel;
        public CanvasGroup group;
        [Header("Puzzle results (optional)")]
        public TMP_Text puzzleNumber, puzzleName;
        public string passText = "PASS", failText = "FAIL";
    }
}
