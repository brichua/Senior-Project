using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VocaloidTCG
{
    [CreateAssetMenu(menuName = "Vocaloid TCG/Deck Catalog")]
    public sealed class DeckCatalog : ScriptableObject
    {
        public List<CardData> cards = new List<CardData>();
        public List<CharacterClassData> classes = new List<CharacterClassData>();
        public List<CardBackData> defaultCardBacks = new List<CardBackData>();
        public List<CardData> startingCollection = new List<CardData>();
        public DeckData starterDeck;
        public CardData Card(string id) => cards.Find(c => c && c.id == id);
        public CharacterClassData Class(CharacterClass id) => classes.Find(c => c && c.identity == id);
        public List<CardBackData> Backs(IEnumerable<CharacterClass> selected) => defaultCardBacks
            .Concat(selected.Select(Class).Where(c => c && c.specialCardBack).Select(c => c.specialCardBack))
            .Where(b => b).Distinct().ToList();
        public CardBackData BackData(DeckRecord deck) => Backs(deck.classes).Find(b => b.id == deck.cardBackId);
        public Sprite Back(DeckRecord deck) => BackData(deck)?.image;
        public CardData Cover(DeckRecord deck) => Card(deck.vipCardId) ?? deck.cardIds.Select(Card).FirstOrDefault(c => c);

        public bool ValidateCatalog(out string error){
            error = "Catalog requires a starter deck and at least one default card back.";
            if(!starterDeck || !defaultCardBacks.Any(b => b)) return false;
            var cardErrors = new List<string>();
            var cardsById = new Dictionary<string, string>();
            
            for(int i = 0; i < cards.Count; i++){
                var card = cards[i];
                string label = "Cards[" + i + "]";
                if(!card) { cardErrors.Add(label + ": missing card asset."); continue; }
                label += " \"" + (string.IsNullOrWhiteSpace(card.cardName) ? card.name : card.cardName) + "\" (asset: " + card.name + ")";
                if(string.IsNullOrWhiteSpace(card.id)) cardErrors.Add(label + ": missing ID.");
                else if(cardsById.TryGetValue(card.id, out var existing))
                    cardErrors.Add(label + ": duplicate ID \"" + card.id + "\"; also used by " + existing + ".");
                else cardsById.Add(card.id, label);
                if(!card.cardClass) cardErrors.Add(label + ": missing class.");
                else if(Class(card.cardClass.identity) != card.cardClass)
                    cardErrors.Add(label + ": class asset \"" + card.cardClass.name + "\" is not registered for " + card.cardClass.DisplayName + " in this catalog.");
                if(card.performer == card.stageEffect)
                    cardErrors.Add(label + ": enable exactly one card type (Performer or Stage Effect).");
            }

            if(cardErrors.Count > 0){
                error = "Catalog card issues:\n" + string.Join("\n", cardErrors);
                return false;
            }

            error = "Register each of the six character classes exactly once.";
            if(classes.Count != 6 || classes.Any(c => !c) || classes.Select(c => c.identity).Distinct().Count() != 6) return false;
            var ids = new HashSet<string>();
            error = "Card backs need unique nonempty IDs.";

            foreach(var back in Backs(classes.Select(c => c.identity))){
                if(string.IsNullOrWhiteSpace(back.id) || !ids.Add(back.id)) return false;
            }
            error = ""; return true;
        }
    }
}
