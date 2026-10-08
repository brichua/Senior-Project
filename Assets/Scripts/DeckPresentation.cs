using System.Collections.Generic;
using System.Linq;

namespace VocaloidTCG
{
    public static class DeckPresentation
    {
        public static List<CharacterClassData> Classes(DeckRecord deck, DeckCatalog catalog){
            if(deck == null || !catalog) return new List<CharacterClassData>();
            var cards = deck.cardIds.Concat(new[] { deck.vipCardId }).Select(catalog.Card).Where(c => c).ToList();
            return deck.classes.Select(catalog.Class).Where(c => c).OrderByDescending(c => cards.Count(card => card.cardClass == c)).ToList();
        }
    }
}
