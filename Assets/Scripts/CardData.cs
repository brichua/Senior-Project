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
        [Tooltip("Influence added by this card when it is used as a showdown assist.")]
        public int assistInfluence;

        [Header("Art and Audio")]
        public Sprite cardImage;
        public Sprite characterImage;
        public Sprite iconImage;
        public AudioClip playSfx;

        private void OnValidate()
        {
            if (performer == stageEffect)
                stageEffect = !performer;
        }
    }
}
