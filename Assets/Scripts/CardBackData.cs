using UnityEngine;

namespace VocaloidTCG
{
    [CreateAssetMenu(menuName = "Vocaloid TCG/Card Back")]
    public sealed class CardBackData : ScriptableObject
    {
        public string id;
        public Sprite image;
        public Sprite deckImage;

        public void ApplyTo(BoardUI.SideArt side){
            side.cardBack = image;
            side.deck = deckImage;
        }

        private void OnValidate(){
#if UNITY_EDITOR
            var path = UnityEditor.AssetDatabase.GetAssetPath(this);
            if(!string.IsNullOrEmpty(path)) id = UnityEditor.AssetDatabase.AssetPathToGUID(path);
#endif
            if(string.IsNullOrWhiteSpace(id)) id = System.Guid.NewGuid().ToString("N");
        }
    }
}
