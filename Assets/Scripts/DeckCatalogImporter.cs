using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VocaloidTCG
{
    public static class DeckCatalogImporter
    {
        public static string Key(DeckData asset) => "catalog/" + asset.name;

        public static bool Import(DeckCatalog catalog, List<DeckRecord> saved, List<string> owned,
            List<string> imported, List<DeckRecord> baselines){
            var templates = new List<DeckRecord>();
            var keys = new HashSet<string>();
            foreach(var asset in catalog.decks ?? new List<DeckData>()){
                if(!asset) continue;
                var candidate = asset.CreateRecord();
                candidate.id = candidate.catalogKey = Key(asset);
                if(!keys.Add(candidate.id)){
                    Debug.LogWarning("Catalog deck asset names must be unique: " + asset.name);
                    continue;
                }
                if(string.IsNullOrEmpty(candidate.cardBackId))
                    candidate.cardBackId = catalog.Backs(candidate.classes).FirstOrDefault()?.id ?? "";
                var available = owned.Concat(candidate.cardIds).Concat(new[] { candidate.vipCardId }).ToList();
                if(!DeckRules.Validate(candidate, catalog, available, out var error)){
                    Debug.LogWarning("Could not synchronize catalog deck \"" + asset.name + "\": " + error);
                    continue;
                }
                templates.Add(candidate);
            }
            return Synchronize(templates, saved, owned, imported, baselines);
        }

        internal static bool Synchronize(List<DeckRecord> templates, List<DeckRecord> saved, List<string> owned,
            List<string> imported, List<DeckRecord> baselines){
            bool changed = false;
            foreach(var template in templates){
                string key = template.catalogKey;
                var baseline = baselines.Find(d => d.id == key);
                var linked = saved.Find(d => d.catalogKey == key || d.id == key);
                if(linked == null){
                    var matches = saved.Where(d => string.IsNullOrEmpty(d.catalogKey) && SameIdentity(d, template)).ToList();
                    linked = matches.FirstOrDefault(d => SameDeck(d, template)) ?? (matches.Count == 1 ? matches[0] : null);
                }
                if(linked == null && imported.Contains(key)) continue;
                if(linked == null){
                    linked = Clone(template);
                    saved.Add(linked);
                    changed = true;
                }
                if(linked.catalogKey != key){ linked.catalogKey = key; changed = true; }
                bool templateChanged = baseline == null || !SameDeck(baseline, template);
                if(templateChanged && !SameDeck(linked, template)){
                    bool customized = baseline != null ? !SameDeck(linked, baseline) : !PreviousStarterVersion(linked, template);
                    if(customized){
                        var personal = Clone(linked);
                        personal.id = Guid.NewGuid().ToString("N");
                        personal.catalogKey = "";
                        personal.deckName = personal.deckName ?? "Deck";
                        personal.deckName = personal.deckName.Substring(0, Math.Min(7, personal.deckName.Length)) + " (saved)";
                        saved.Add(personal);
                    }
                    string id = linked.id;
                    bool favorite = linked.favorite;
                    int index = saved.IndexOf(linked);
                    linked = Clone(template);
                    linked.id = id;
                    linked.favorite = favorite;
                    saved[index] = linked;
                    changed = true;
                }
                if(templateChanged){
                    baselines.RemoveAll(d => d.id == key);
                    baselines.Add(Clone(template));
                    changed = true;
                }
                if(!imported.Contains(key)){ imported.Add(key); changed = true; }
                foreach(string id in template.cardIds.Concat(new[] { template.vipCardId }).Where(id => !string.IsNullOrEmpty(id)))
                    if(!owned.Contains(id)){ owned.Add(id); changed = true; }
            }
            return changed;
        }

        internal static DeckRecord Clone(DeckRecord source) => new DeckRecord {
            id = source.id, catalogKey = source.catalogKey, deckName = source.deckName,
            cardBackId = source.cardBackId, vipCardId = source.vipCardId, favorite = source.favorite,
            classes = new List<CharacterClass>(source.classes), cardIds = new List<string>(source.cardIds)
        };

        private static bool PreviousStarterVersion(DeckRecord saved, DeckRecord template){
            if(!SameIdentity(saved, template) || !string.IsNullOrEmpty(saved.vipCardId) ||
                string.IsNullOrEmpty(template.vipCardId) || saved.cardIds.Count != template.cardIds.Count + 1) return false;
            var remaining = new List<string>(saved.cardIds);
            return template.cardIds.All(id => remaining.Remove(id));
        }

        private static bool SameIdentity(DeckRecord a, DeckRecord b) => a.deckName == b.deckName &&
            a.cardBackId == b.cardBackId && a.classes.OrderBy(c => c).SequenceEqual(b.classes.OrderBy(c => c));

        internal static bool SameDeck(DeckRecord a, DeckRecord b) => SameIdentity(a, b) && a.vipCardId == b.vipCardId &&
            a.cardIds.OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(b.cardIds.OrderBy(id => id, StringComparer.Ordinal));
    }
}

