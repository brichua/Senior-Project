using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VocaloidTCG.BoardUI
{
    [CreateAssetMenu(menuName = "Vocaloid TCG/Board Setup Catalog")]
    public sealed class BoardSetupCatalog : ScriptableObject
    {
        [System.Serializable] public sealed class Entry{
            public CharacterClass player, enemy;
            public BoardSetup setup;
        }
        
        public List<Entry> boards = new List<Entry>();

        public static BoardSetup Find(CharacterClassData player, CharacterClassData enemy){
            if(!player || !enemy) return null;
            var catalog = Resources.Load<BoardSetupCatalog>("MultiplayerBoards");
            return catalog ? catalog.boards.FirstOrDefault(entry => entry != null && entry.setup && entry.player == player.identity && entry.enemy == enemy.identity)?.setup : null;
        }
    }
}
