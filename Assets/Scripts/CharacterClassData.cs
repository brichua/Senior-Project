using UnityEngine;
using UnityEngine.Serialization;

namespace VocaloidTCG
{
    public enum CharacterClass { HatsuneMiku, KagamineLen, KagamineRin, KAITO, MEIKO, MegurineLuka }

    [CreateAssetMenu(menuName = "Vocaloid TCG/Character Class")]
    public sealed class CharacterClassData : ScriptableObject
    {
        public CharacterClass identity;
        public string displayName;
        public Color color = Color.white;
        [Header("Deck and card artwork")]
        public Sprite classIcon;
        public Sprite puzzleArt;
        [FormerlySerializedAs("deckIcon")] public Sprite playerDeckIcon;
        public Sprite enemyDeckIcon;
        public Sprite popupImage, costImage, performerBorder, stageEffectBorder, vipImage;
        public Sprite buttonImage, selectedButtonImage;
        public CardBackData specialCardBack;
        [Tooltip("Energy icons indexed by energy amount, from 0 through 8.")]
        [FormerlySerializedAs("energyIcons")] public Sprite[] playerEnergyIcons = new Sprite[9];
        public Sprite[] enemyEnergyIcons = new Sprite[9];
        [FormerlySerializedAs("skillImage")] public Sprite playerSkillImage;
        public Sprite enemySkillImage;
        [Tooltip("End Turn button artwork when this is the board's player class.")]
        public Sprite endTurnImage;
        public Sprite cdImage;
        public Sprite hoverTile, tile;
        public Sprite stageEffectInfoIcon, infoIcon, avatar, avatarBackground, infoBackground;
        public Sprite barBackground, barFill;
        public Sprite deckIcon => playerDeckIcon;
        public Sprite DeckIcon(bool enemySide) => enemySide && enemyDeckIcon ? enemyDeckIcon : playerDeckIcon;

        public void ApplyTo(BoardUI.SideArt side, bool enemySide){
            side.name = DisplayName;
            side.textColor = color;
            side.deckIcon = DeckIcon(enemySide);
            side.energy = new Sprite[9];

            for(int i = 0; i < side.energy.Length; i++){
                var playerIcon = playerEnergyIcons != null && i < playerEnergyIcons.Length ? playerEnergyIcons[i] : null;
                var enemyIcon = enemyEnergyIcons != null && i < enemyEnergyIcons.Length ? enemyEnergyIcons[i] : null;
                side.energy[i] = enemySide && enemyIcon ? enemyIcon : playerIcon;
            }
            
            side.skill = enemySide && enemySkillImage ? enemySkillImage : playerSkillImage;
            side.cd = cdImage;
            side.hoverTile = hoverTile;
            side.tile = tile;
            side.stageEffectInfoIcon = stageEffectInfoIcon;
            side.infoIcon = infoIcon;
            side.avatar = avatar;
            side.avatarBackground = avatarBackground;
            side.infoBackground = infoBackground;
            side.barBackground = barBackground;
            side.barFill = barFill;
            side.performerPopup = popupImage;
        }

        private void OnValidate(){
            System.Array.Resize(ref playerEnergyIcons, 9);
            System.Array.Resize(ref enemyEnergyIcons, 9);
        }

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ?
            new[] { "Hatsune Miku", "Kagamine Len", "Kagamine Rin", "KAITO", "MEIKO", "Megurine Luka" }[(int)identity] : displayName;
    }
}
