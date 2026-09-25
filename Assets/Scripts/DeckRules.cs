using System.Collections.Generic;
using System.Linq;

namespace VocaloidTCG
{
    public static class DeckRules
    {
        public static bool Validate(DeckRecord deck, DeckCatalog catalog, ICollection<string> owned, out string error)
        {
            error = "Deck name must contain 1–15 characters.";
            if(deck == null || string.IsNullOrWhiteSpace(deck.deckName) || deck.deckName.Length > 15) return false;
            error = "Choose one or two different classes.";

            if(deck.classes == null || deck.classes.Count < 1 || deck.classes.Count > 2 ||
                deck.classes.Distinct().Count() != deck.classes.Count || deck.classes.Any(c => !catalog.Class(c))) return false;
            error = "Deck cannot contain more than 30 cards.";

            if(deck.cardIds == null || deck.Count > 30) return false;
            error = "Choose an available card back.";

            if(!catalog.Backs(deck.classes).Any(b => b.id == deck.cardBackId)) return false;
            var cardErrors = new List<string>();

            foreach(var group in deck.cardIds.GroupBy(id => id)){
                var card = CheckCard(group.Key, "Regular card list", deck, catalog, owned, cardErrors);
                if(!card) continue;
                string label = CardLabel(card);
                if(card.vip)
                    cardErrors.Add(label + ": found " + group.Count() + " VIP copy/copies in the regular card list. Remove them from that list and use the single VIP slot instead.");
                else if(group.Count() > 3)
                    cardErrors.Add(label + ": " + group.Count() + " copies in the deck; maximum is 3. Remove " + (group.Count() - 3) + ".");
            }

            if(!string.IsNullOrEmpty(deck.vipCardId)){
                var vip = CheckCard(deck.vipCardId, "VIP slot", deck, catalog, owned, cardErrors);
                if(vip && !vip.vip)
                    cardErrors.Add(CardLabel(vip) + ": placed in the VIP slot, but this card is not marked as VIP. Move it to the regular card list or choose a VIP card.");
            }

            if(cardErrors.Count > 0){
                error = "Deck \"" + deck.deckName + "\" has card issues:\n" + string.Join("\n", cardErrors);
                return false;
            }
            error = ""; return true;
        }

        private static string CardLabel(CardData card) => "\"" +
            (string.IsNullOrWhiteSpace(card.cardName) ? card.name : card.cardName) + "\" (ID: " + card.id + ")";

        private static CardData CheckCard(string id, string location, DeckRecord deck, DeckCatalog catalog, ICollection<string> owned, List<string> errors){
            if(string.IsNullOrWhiteSpace(id)){
                errors.Add(location + ": contains a missing/blank card ID. Remove the invalid entry and add the intended card again.");
                return null;
            }

            var card = catalog.Card(id);
            if(!card){
                errors.Add(location + ": card ID \"" + id + "\" was not found in the catalog. Register the card or remove it from the deck.");
                return null;
            }

            string label = location + " — " + CardLabel(card);
            if(owned == null || !owned.Contains(id))
                errors.Add(label + ": not owned in the saved collection. Unlock this card or remove it from the deck.");
            if(!card.cardClass)
                errors.Add(label + ": no class assigned on the card asset. Assign its character class.");
            else if(!deck.classes.Contains(card.cardClass.identity))
                errors.Add(label + ": belongs to " + card.cardClass.DisplayName + ", but this deck selects " +
                    string.Join(" / ", deck.classes.Select(c => catalog.Class(c).DisplayName)) + ". Select its class or remove the card.");
            return card;
        }

        public static bool Allowed(CardData card, DeckRecord deck, ICollection<string> owned) =>
            card && card.cardClass && owned.Contains(card.id) && deck.classes.Contains(card.cardClass.identity);

        public static bool SetCopies(DeckRecord deck, CardData card, int count, ICollection<string> owned){
            if(!Allowed(card, deck, owned) || count < 0 || count > (card.vip ? 1 : 3)) return false;
            if(card.vip){
                if(count == 1 && !string.IsNullOrEmpty(deck.vipCardId) && deck.vipCardId != card.id) return false;
                if(count == 1) deck.vipCardId = card.id;
                else if(deck.vipCardId == card.id) deck.vipCardId = "";
            }else{
                deck.cardIds.RemoveAll(id => id == card.id);
                for(int i = 0; i < count; i++) deck.cardIds.Add(card.id);
            }
            return true;
        }

        public static string Stats(DeckRecord deck, DeckCatalog catalog, bool average){
            var cards = deck.cardIds.Select(catalog.Card).Where(c => c).ToList();
            var vip = catalog.Card(deck.vipCardId);
            
            if(vip) cards.Add(vip);
            return (vip ? 1 : 0) + (average ? " VIP" : "/1 VIP card") + "\n" +
                cards.Count(c => c.performer) + " performers\n" + cards.Count(c => c.stageEffect) + " stage effects" +
                (average ? "\n" + (cards.Count == 0 ? 0 : cards.Average(c => c.cost)).ToString("0.##") + " avg cost" : "");
        }
    }
}
