using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace VocaloidTCG.BoardUI
{
    [Serializable]
    public sealed class SideArt
    {
        public string name = "";
        public Sprite skill, barBackground, barFill, deckIcon, deck;
        public Sprite infoBackground, avatar, avatarBackground, infoIcon, cardBack;
        public Sprite stageEffectInfoIcon;
        public Sprite hoverTile, tile;
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
        [Header("Character artwork sources")]
        public CharacterClassData playerClass;
        public CharacterClassData enemyClass;
        [Header("Default card backs (selected decks override these)")]
        public CardBackData playerCardBack;
        public CardBackData enemyCardBack;
        [SerializeField, HideInInspector, FormerlySerializedAs("playerClasses")]
        private System.Collections.Generic.List<CharacterClassData> legacyPlayerClasses;
        [SerializeField, HideInInspector, FormerlySerializedAs("enemyClasses")]
        private System.Collections.Generic.List<CharacterClassData> legacyEnemyClasses;
        public CharacterClassData PlayerClass => playerClass ? playerClass : FirstClass(legacyPlayerClasses);
        public CharacterClassData EnemyClass => enemyClass ? enemyClass : FirstClass(legacyEnemyClasses);
        [Header("Shared board artwork")]
        public Sprite background, topBar, pause, tiedPopup;
        public Sprite defaultTile, contestedTile, movingTile, hoverTile, playerTile, enemyTile;
        public Sprite endTurn;
        [Tooltip("Class and card-back artwork is supplied by the assets above. Keep animator and assistance artwork here.")]
        public SideArt player = new SideArt { name = "Player" };
        [Tooltip("Class and card-back artwork is supplied by the assets above. Keep animator and assistance artwork here.")]
        public SideArt enemy = new SideArt { name = "Enemy" };
        public string[] boldKeywords = { "harmonize", "stage share" };
        [Header("Keyword tooltips")]
        public KeywordDefinition[] keywordDescriptions = {
            new KeywordDefinition { keyword = "harmonize" },
            new KeywordDefinition { keyword = "stage share" }
        };
        public Sprite keywordTooltipBackground;
        public string countdownParameter = "Countdown";

        private static CharacterClassData FirstClass(System.Collections.Generic.List<CharacterClassData> classes)
        {
            return classes == null ? null : classes.Find(c => c);
        }

        public bool Matches(System.Collections.Generic.List<CharacterClassData> players,
            System.Collections.Generic.List<CharacterClassData> enemies)
        {
            return PlayerClass && EnemyClass && players != null && enemies != null &&
                players.Exists(c => c && c.identity == PlayerClass.identity) &&
                enemies.Exists(c => c && c.identity == EnemyClass.identity);
        }

        public BoardSetup CreateRuntimeSetup(CardBackData playerBackOverride = null, CardBackData enemyBackOverride = null)
        {
            var result = Instantiate(this);
            if(result.player == null) result.player = new SideArt();
            if(result.enemy == null) result.enemy = new SideArt();
            if(PlayerClass) {
                PlayerClass.ApplyTo(result.player, false);
                result.playerTile = PlayerClass.tile;
                result.hoverTile = PlayerClass.hoverTile;
                if(PlayerClass.endTurnImage) result.endTurn = PlayerClass.endTurnImage;
            }
            if(EnemyClass) {
                EnemyClass.ApplyTo(result.enemy, true);
                result.enemyTile = EnemyClass.tile;
            }
            var playerBack = playerBackOverride ? playerBackOverride : playerCardBack;
            var enemyBack = enemyBackOverride ? enemyBackOverride : enemyCardBack;
            if(playerBack) { result.playerCardBack = playerBack; playerBack.ApplyTo(result.player); }
            if(enemyBack) { result.enemyCardBack = enemyBack; enemyBack.ApplyTo(result.enemy); }
            return result;
        }

        private void OnValidate()
        {
            if(!playerClass) playerClass = FirstClass(legacyPlayerClasses);
            if(!enemyClass) enemyClass = FirstClass(legacyEnemyClasses);
            legacyPlayerClasses = null;
            legacyEnemyClasses = null;
            if(player == null) player = new SideArt { name = "Player" };
            if(enemy == null) enemy = new SideArt { name = "Enemy" };
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
