using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace VocaloidTCG.BoardUI
{
    [Serializable]
    public sealed class SideArt
    {
        public Sprite skill, barBackground, barFill, deckIcon, deck;
        public Sprite infoBackground, avatar, avatarBackground, infoIcon, cardBack;
        public Sprite stageEffectInfoIcon;
        public Sprite performerPopup;
        public Sprite assistance, cd;
        public Sprite[] energy = new Sprite[9];
        public RuntimeAnimatorController avatarAnimator;
        [FormerlySerializedAs("deckTextColor")]
        public Color textColor = Color.white;
    }

    [CreateAssetMenu(menuName = "Vocaloid TCG/Board Setup", fileName = "BoardSetup")]
    public sealed class BoardSetup : ScriptableObject
    {
        public Sprite background, topBar, endTurn, pause;
        public Sprite defaultTile, hoverTile, playerTile, enemyTile, contestedTile;
        public Sprite movingTile;
        public Sprite tiedPopup;
        public SideArt player = new SideArt();
        public SideArt enemy = new SideArt();
        public string[] boldKeywords = { "harmonize", "stage share" };
        [Header("Keyword tooltips")]
        public KeywordDefinition[] keywordDescriptions = {
            new KeywordDefinition { keyword = "harmonize" },
            new KeywordDefinition { keyword = "stage share" }
        };
        public Sprite keywordTooltipBackground;
        public string countdownParameter = "Countdown";

        private void OnValidate()
        {
            if(player == null) player = new SideArt();
            if(enemy == null) enemy = new SideArt();
            Array.Resize(ref player.energy, 9);
            Array.Resize(ref enemy.energy, 9);
        }
    }

    [Serializable]
    public sealed class KeywordDefinition
    {
        public string keyword;
        [TextArea(2, 6)] public string description = "n/a";
    }
}
