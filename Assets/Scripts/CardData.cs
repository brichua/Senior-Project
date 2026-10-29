using System.Collections.Generic;
using UnityEngine;

namespace VocaloidTCG
{
    [CreateAssetMenu(menuName = "Vocaloid TCG/Card", fileName = "New Card")]
    public sealed class CardData : ScriptableObject
    {
        [Header("Identity")]
        public string cardName;
        public string id;
        public CharacterClassData cardClass;
        public bool vip;
        [TextArea(3, 8)] public string info;

        [Header("Type (choose exactly one)")]
        public bool performer = true;
        public bool stageEffect;

        [Header("Gameplay")]
        [Min(0)] public int cost;
        [Min(0)] public int influence;
        [Tooltip("Enemy AI hints, e.g. near_allies, ally_cluster, front_pressure, support. See Tools/QA/EnemyPlacementFlags.md.")]
        public List<string> flags = new List<string>();
        [Tooltip("Basic stage effect: permanently change a friendly performer's influence by this amount.")]
        public int stageInfluenceChange = 1;
        [Header("Special effects")]
        public CardEffect effect;
        [Tooltip("Fan score needed for this card's score-dependent effect. 0 disables the threshold until it is configured.")]
        [Min(0)] public int scoreThreshold;
        [Min(0)] public int harmonize, soloist;
        public bool stageShare, encore, nimble;
        [Tooltip("Allow this Stage Effect to be played during Performance as well as Preparation. Normal energy and target requirements still apply.")]
        public bool cheerEffect;
        public bool venueEffect;
        [Tooltip("Restore 1 influence at the end of your turn, up to original influence.")]
        public bool regen;
        [Tooltip("Protected X: separate starting shield influence. 0 means no shield.")]
        [UnityEngine.Serialization.FormerlySerializedAs("shielded")]
        [Min(0)] public int protectedInfluence;

        [Header("Art and Audio")]
        public Sprite cardImage;
        public Sprite characterImage;
        public Sprite iconImage;
        public Sprite popupImage;
        public AudioClip playSfx;

        private void OnValidate()
        {
#if UNITY_EDITOR
            var path = UnityEditor.AssetDatabase.GetAssetPath(this);
            if(!string.IsNullOrEmpty(path)) id = UnityEditor.AssetDatabase.AssetPathToGUID(path);
#endif
            if(string.IsNullOrWhiteSpace(id)) id = System.Guid.NewGuid().ToString("N");
            if (performer == stageEffect)
                stageEffect = !performer;
        }
    }
}
