using System.Collections.Generic;
using UnityEngine;

namespace VocaloidTCG
{
    [CreateAssetMenu(menuName = "Vocaloid TCG/Card", fileName = "New Card")]
    public sealed class CardData : ScriptableObject
    {
        [Header("Identity")]
        public string cardName;
        [TextArea(3, 8)] public string info;

        [Header("Type (choose exactly one)")]
        public bool performer = true;
        public bool stageEffect;

        [Header("Gameplay")]
        [Min(0)] public int cost;
        [Min(0)] public int influence;
        public List<string> flags = new List<string>();
        [Tooltip("Basic stage effect: permanently change a friendly performer's influence by this amount.")]
        public int stageInfluenceChange = 1;

        [Header("Art and Audio")]
        public Sprite cardImage;
        public Sprite characterImage;
        public Sprite iconImage;
        public Sprite popupImage;
        public AudioClip playSfx;

        private void OnValidate()
        {
            if (performer == stageEffect)
                stageEffect = !performer;
        }
    }
}
