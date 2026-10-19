using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VocaloidTCG
{
    public static class DeckCatalogImporter
    {
        public static bool Import(DeckCatalog catalog, List<DeckRecord> saved, List<string> owned, List<string> imported){
            bool changed = false;
            foreach(var asset in catalog.decks ?? new List<DeckData>()){
                if(!asset) continue;
                string key = "catalog/" + asset.name;
                if(imported.Contains(key)) continue;
                var candidate = asset.CreateRecord();
                
                if(string.IsNullOrEmpty(candidate.cardBackId))
                    candidate.cardBackId = catalog.Backs(candidate.classes).FirstOrDefault()?.id ?? "";
                if(!DeckRules.Validate(candidate, catalog, owned, out var error)){
                    Debug.LogWarning("Could not add catalog deck \"" + asset.name + "\" to the collection: " + error);
                    continue;
                }
                
                bool exists = saved.Any(d => d.id == key || SameDeck(d, candidate));
                if(!exists){
                    candidate.id = key;
                    saved.Add(candidate);
                }
                imported.Add(key);
                changed = true;
            }
            return changed;
        }

        private static bool SameDeck(DeckRecord a, DeckRecord b){
            return a.deckName == b.deckName && a.cardBackId == b.cardBackId && a.vipCardId == b.vipCardId &&
                a.classes.OrderBy(c => c).SequenceEqual(b.classes.OrderBy(c => c)) &&
                a.cardIds.OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(b.cardIds.OrderBy(id => id, StringComparer.Ordinal));
        }
    }
}
