using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VocaloidTCG
{
    [Serializable]
    public sealed class DeckRecord
    {
        public string id = Guid.NewGuid().ToString("N");
        public string deckName = "";
        public string cardBackId = "";
        public List<CharacterClass> classes = new List<CharacterClass>();
        public List<string> cardIds = new List<string>();
        public string vipCardId = "";
        public bool favorite;
        public int Count => cardIds.Count + (string.IsNullOrEmpty(vipCardId) ? 0 : 1);
        public DeckRecord Copy() => JsonUtility.FromJson<DeckRecord>(JsonUtility.ToJson(this));
        public int Copies(CardData card) => !card ? 0 : card.vip ? (vipCardId == card.id ? 1 : 0) : cardIds.Count(id => id == card.id);
    }

    [CreateAssetMenu(menuName = "Vocaloid TCG/Deck")]
    public sealed class DeckData : ScriptableObject
    {
        public string deckName;
        public CardBackData cardBack;
        public List<CardData> cards = new List<CardData>();
        public CardData vipCard;
        public List<CharacterClassData> classes = new List<CharacterClassData>();
        public DeckRecord CreateRecord() => new DeckRecord {
            deckName = deckName ?? "", cardBackId = cardBack ? cardBack.id : "",
            cardIds = cards.Where(c => c).Select(c => c.id).ToList(),
            vipCardId = vipCard ? vipCard.id : "",
            classes = classes.Where(c => c).Select(c => c.identity).Distinct().ToList()
        };
    }
}
