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
        [Tooltip("Character portrait shown in the concert-style match introduction for this class, on either side.")]
        public Sprite playerArt;
        [Tooltip("Transparent full character artwork for the centered match result screen.")]
        public Sprite fullPlayerImage;
        [Tooltip("Character artwork for victory and defeat. Falls back to Full Player Image when unassigned.")]
        public Sprite victoryImage, defeatImage;
        [Tooltip("Border artwork for this class on either side of the game HUD.")]
        public Sprite borderImage;
        [Header("Deck and card artwork")]
        public Sprite classIcon;
        public Sprite puzzleArt;
        [Tooltip("Background for the first-clear reward card notification.")]
        public Sprite rewardCardBackground;
        public Sprite storyArt;
        [Tooltip("Character artwork used by rival cards and map icons in Story mode.")]
        public Sprite rivalArt;
        [FormerlySerializedAs("deckIcon")] public Sprite playerDeckIcon;
        public Sprite enemyDeckIcon;
        public Sprite popupImage, costImage, performerBorder, stageEffectBorder, vipImage;
        public Sprite buttonImage, selectedButtonImage;
        [Header("Story chapter buttons")]
        public Sprite prologueButtonImage, epilogueButtonImage;
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
        [Header("Avatar animation and voiceline subtitles")]
        public RuntimeAnimatorController avatarAnimator;
        [TextArea] public string victoryVoiceline, defeatVoiceline, winTileVoiceline, loseTileVoiceline;
        [TextArea] public string excitedVoiceline, concernedVoiceline, idleVoiceline;
        [TextArea] public string multiplayerVoiceline;
        [Tooltip("Subtitles in exactly the same order as this character's idle audio array in VoicelinesManager. All use the Idle animation.")]
        [TextArea] public string[] idleVoicelines = new string[0];
        [Tooltip("Optional subtitles matching the Win Tile and Lose Tile audio array order.")]
        [TextArea] public string[] winTileVoicelines = new string[0], loseTileVoicelines = new string[0];

        public string VoicelineText(VoicelineEvent reaction, int clipIndex = 0){
            switch(reaction){
                case VoicelineEvent.Victory: return victoryVoiceline;
                case VoicelineEvent.Defeat: return defeatVoiceline;
                case VoicelineEvent.Win_Tile: return IndexedSubtitle(winTileVoicelines,clipIndex,winTileVoiceline);
                case VoicelineEvent.Lose_Tile: return IndexedSubtitle(loseTileVoicelines,clipIndex,loseTileVoiceline);
                case VoicelineEvent.Excited: return excitedVoiceline;
                case VoicelineEvent.Concerned: return concernedVoiceline;
                case VoicelineEvent.Idle: return IndexedSubtitle(idleVoicelines,clipIndex,idleVoiceline);
                case VoicelineEvent.Multiplayer: return multiplayerVoiceline;
                default: return "";
            }
        }
        private static string IndexedSubtitle(string[] lines,int index,string legacy){
            if(lines!=null && index>=0 && index<lines.Length) return lines[index] ?? "";
            return index==0 ? legacy ?? "" : "";
        }
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
            side.border = borderImage;
            side.hoverTile = hoverTile;
            side.tile = tile;
            side.stageEffectInfoIcon = stageEffectInfoIcon;
            side.infoIcon = infoIcon;
            side.avatar = avatar;
            side.avatarAnimator = avatarAnimator;
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
