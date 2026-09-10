using System;
using System.Collections.Generic;
using UnityEngine;

namespace VocaloidTCG
{
    [CreateAssetMenu(menuName = "Vocaloid TCG/Board Skin", fileName = "New Board Skin")]
    public sealed class BoardSkin : ScriptableObject
    {
        [Serializable]
        public sealed class SideSkin
        {
            public string displayName;
            public Sprite avatar;
            public Sprite infoBackground;
            public Sprite avatarBackground;
            public Sprite skill;
            public Sprite barBackground;
            public Sprite barFill;
            public Sprite deck;
            public Sprite deckIcon;
            public Sprite cd;
            public Sprite tile;
            public Sprite hoverTile;
            public Sprite popup;
            public Sprite cardBack;
            public Sprite performerInfoIcon;
            public Sprite stageEffectInfoIcon;
            [Tooltip("Sprites used by energy slots 1 through 8. Missing entries use the last sprite.")]
            public List<Sprite> energyIcons = new List<Sprite>(8);
        }

        [Header("Shared")]
        public Sprite background;
        public Sprite topBar;
        public Sprite neutralTile;

        [Header("Player")]
        public SideSkin player = new SideSkin();

        [Header("Enemy")]
        public SideSkin enemy = new SideSkin();

        [Header("Turn Button")]
        public Sprite playerEndTurn;
    }
}
