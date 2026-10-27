using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace VocaloidTCG
{
    public sealed class DeckLibrary
    {
        [Serializable] private sealed class SaveData
        {
            public int version = 1;
            public List<DeckRecord> decks = new List<DeckRecord>();
            public List<string> owned = new List<string>();
            public string selectedId;
            public List<string> completedPuzzles = new List<string>();
            public List<StoryClearRecord> storyClears = new List<StoryClearRecord>();
            public List<string> viewedStoryDialogues = new List<string>();
            public List<string> importedCatalogDecks = new List<string>();
            public List<DeckRecord> catalogDeckBaselines = new List<DeckRecord>();
        }
        private static DeckLibrary instance;
        private SaveData data;
        private readonly string path;
        private string catalogSignature;
        private bool refreshingCatalog;
        public DeckCatalog Catalog { get; private set; }
        public event Action Changed;
        public IReadOnlyList<DeckRecord> Decks => data.decks.Select(d => d.Copy()).ToList();
        public List<string> Owned => new List<string>(data.owned);
        public DeckRecord Selected => data.decks.Find(d => d.id == data.selectedId)?.Copy();
        public bool CanDeleteDeck => data.decks.Count > 1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { instance = null; }

        public static DeckLibrary Get(DeckCatalog catalog = null)
        {
            if(instance != null){ instance.RefreshCatalog(); return instance; }
            if(!catalog) catalog = Resources.Load<DeckCatalog>("DeckCatalog");
            if(!catalog) throw new InvalidOperationException("Assign a DeckCatalog or create Resources/DeckCatalog.asset.");
            instance = new DeckLibrary(catalog, Path.Combine(Application.persistentDataPath, "decks-v1.json")); return instance;
        }

        private DeckLibrary(DeckCatalog catalog, string savePath){
            Catalog = catalog;
            string error;
            if(!catalog.ValidateCatalog(out error)) throw new InvalidOperationException(error);
            
            path = savePath;
            if(File.Exists(path)) {
                try{
                    data = Read(path);
                    }
                catch(Exception primary){
                    if(!File.Exists(path + ".bak")) throw new IOException("Deck save could not be read; it has been preserved.", primary);
                    data = Read(path + ".bak");
                    File.Copy(path, path + ".corrupt-" + DateTime.UtcNow.Ticks);
                    File.Copy(path + ".bak", path, true);
                    Debug.LogWarning("Recovered deck library from backup.");
                }
            }else{
                data = new SaveData {
                    owned = catalog.startingCollection.Where(c => c).Select(c => c.id).Distinct().ToList()
                };
            }

            var startingCardIds = catalog.startingCollection
                .Where(card => card && !string.IsNullOrWhiteSpace(card.id))
                .Select(card => card.id)
                .Distinct()
                .ToList();
            bool saveNeedsUpdate = startingCardIds.Any(id => !data.owned.Contains(id));
            if(saveNeedsUpdate){
                data.owned = data.owned.Concat(startingCardIds).Distinct().ToList();
            }

            if(data.decks.Count == 0) {
                var starter = catalog.starterDeck.CreateRecord();
                if(catalog.decks.Contains(catalog.starterDeck))
                    starter.id = starter.catalogKey = DeckCatalogImporter.Key(catalog.starterDeck);
                if(string.IsNullOrEmpty(starter.cardBackId)) starter.cardBackId = catalog.defaultCardBacks.First(b => b).id;
                data.owned = data.owned
                    .Concat(starter.cardIds).Concat(new[] { starter.vipCardId }).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
                
                if(!DeckRules.Validate(starter, catalog, data.owned, out error))
                    throw new InvalidOperationException("Invalid starter deck: " + error);
                
                data.decks.Add(starter); data.selectedId = starter.id;
                saveNeedsUpdate = true;
            }
            saveNeedsUpdate |= DeckCatalogImporter.Import(catalog, data.decks, data.owned, data.importedCatalogDecks, data.catalogDeckBaselines);
            if(saveNeedsUpdate) Write(data);
            catalogSignature = CatalogSignature();
        }

        private static SaveData Read(string file){
            var loaded = JsonUtility.FromJson<SaveData>(File.ReadAllText(file));
            
            if(loaded == null || loaded.version != 1 || loaded.decks == null || loaded.owned == null ||
                loaded.decks.Any(d => d == null || string.IsNullOrEmpty(d.id) || d.classes == null || d.cardIds == null) ||
                loaded.decks.Select(d => d.id).Distinct().Count() != loaded.decks.Count)
                throw new InvalidDataException("Unsupported or damaged deck save.");
            
            if(!loaded.decks.Any(d => d.id == loaded.selectedId)) loaded.selectedId = loaded.decks.FirstOrDefault()?.id;
            if(loaded.completedPuzzles == null) loaded.completedPuzzles = new List<string>();
            if(loaded.storyClears == null) loaded.storyClears = new List<StoryClearRecord>();
            if(loaded.viewedStoryDialogues == null) loaded.viewedStoryDialogues = new List<string>();
            if(loaded.importedCatalogDecks == null) loaded.importedCatalogDecks = new List<string>();
            if(loaded.catalogDeckBaselines == null) loaded.catalogDeckBaselines = new List<DeckRecord>();
            return loaded;
        }

        private string CatalogSignature(){
            var parts = new List<string>();
            foreach(var asset in Catalog.decks ?? new List<DeckData>()){
                if(!asset) continue;
                var deck = asset.CreateRecord();
                deck.id = deck.catalogKey = DeckCatalogImporter.Key(asset);
                parts.Add(JsonUtility.ToJson(deck));
            }
            parts.Add(string.Join("|", Catalog.startingCollection.Where(c => c).Select(c => c.id)));
            return string.Join("\n", parts);
        }

        public void RefreshCatalog(){
            if(refreshingCatalog) return;
            string signature = CatalogSignature();
            if(signature == catalogSignature) return;
            refreshingCatalog = true;
            try{
                var next = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
                bool changed = false;
                foreach(var card in Catalog.startingCollection.Where(c => c))
                    if(!next.owned.Contains(card.id)){ next.owned.Add(card.id); changed = true; }
                changed |= DeckCatalogImporter.Import(Catalog, next.decks, next.owned,
                    next.importedCatalogDecks, next.catalogDeckBaselines);
                if(changed){ Write(next); data = next; }
                catalogSignature = signature;
                if(changed) Changed?.Invoke();
            }
            finally{ refreshingCatalog = false; }
        }

#if UNITY_EDITOR
        private static double nextCatalogCheck;
        [UnityEditor.InitializeOnLoadMethod]
        private static void WatchCatalog(){
            UnityEditor.EditorApplication.update -= PollCatalog;
            UnityEditor.EditorApplication.update += PollCatalog;
        }
        private static void PollCatalog(){
            if(!Application.isPlaying || instance == null || UnityEditor.EditorApplication.timeSinceStartup < nextCatalogCheck) return;
            nextCatalogCheck = UnityEditor.EditorApplication.timeSinceStartup + 0.5;
            try{ instance.RefreshCatalog(); }
            catch(Exception ex){ Debug.LogWarning("Could not synchronize deck catalog: " + ex.Message); }
        }

        private bool CanWriteCatalog => !Application.dataPath.Replace('\\', '/').Contains("/Library/VP/") &&
            !string.IsNullOrEmpty(UnityEditor.AssetDatabase.GetAssetPath(Catalog));

        private bool SaveCatalogAsset(DeckRecord deck, out string error){
            error = "";
            if(!CanWriteCatalog) return true;
            var asset = Catalog.decks.FirstOrDefault(d => d && DeckCatalogImporter.Key(d) == deck.catalogKey);
            bool created = !asset;
            DeckRecord previous = asset ? asset.CreateRecord() : null;
            try{
                if(created){
                    string parent = Path.GetDirectoryName(UnityEditor.AssetDatabase.GetAssetPath(Catalog)).Replace('\\', '/');
                    string folder = parent + "/Player Decks";
                    if(!UnityEditor.AssetDatabase.IsValidFolder(folder)) UnityEditor.AssetDatabase.CreateFolder(parent, "Player Decks");
                    asset = ScriptableObject.CreateInstance<DeckData>();
                    UnityEditor.AssetDatabase.CreateAsset(asset, UnityEditor.AssetDatabase.GenerateUniqueAssetPath(folder + "/Deck-" + Guid.NewGuid().ToString("N") + ".asset"));
                    Catalog.decks.Add(asset);
                }
                ApplyToAsset(asset, deck);
                UnityEditor.EditorUtility.SetDirty(asset);
                UnityEditor.AssetDatabase.SaveAssetIfDirty(asset);
                if(created){ UnityEditor.EditorUtility.SetDirty(Catalog); UnityEditor.AssetDatabase.SaveAssetIfDirty(Catalog); }
                deck.catalogKey = DeckCatalogImporter.Key(asset);
                return true;
            }
            catch(Exception ex){
                if(created && asset){
                    Catalog.decks.Remove(asset);
                    UnityEditor.AssetDatabase.DeleteAsset(UnityEditor.AssetDatabase.GetAssetPath(asset));
                }
                else if(asset && previous != null) ApplyToAsset(asset, previous);
                error = "Could not update the catalog deck asset: " + ex.Message;
                return false;
            }
        }

        private void ApplyToAsset(DeckData asset, DeckRecord deck){
            asset.deckName = deck.deckName;
            asset.cards = deck.cardIds.Select(Catalog.Card).ToList();
            asset.vipCard = Catalog.Card(deck.vipCardId);
            asset.classes = deck.classes.Select(Catalog.Class).ToList();
            asset.cardBack = Catalog.BackData(deck);
        }
#endif

        private void Write(SaveData next){
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(next, true));
            if(File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }

        private bool Commit(Action<SaveData> change, out string error){
            var next = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
            change(next);
            try{
                Write(next);
            }
            catch(Exception ex){
                error = "Could not save decks: " + ex.Message; return false;
            }
            data = next; error = ""; Changed?.Invoke(); return true;
        }

        public bool SaveDeck(DeckRecord deck, out string error){
            RefreshCatalog();
            if(!DeckRules.Validate(deck, Catalog, data.owned, out error)) return false;
            var savedDeck = deck.Copy();
            if(string.IsNullOrEmpty(savedDeck.id)) savedDeck.id = Guid.NewGuid().ToString("N");
            var existing = data.decks.Find(d => d.id == savedDeck.id);
            savedDeck.catalogKey = existing?.catalogKey ?? "";
#if UNITY_EDITOR
            if(!SaveCatalogAsset(savedDeck, out error)) return false;
#endif
            return Commit(next => {
                var copy = savedDeck.Copy();
                int index = next.decks.FindIndex(d => d.id == copy.id);
                if(index >= 0) next.decks[index] = copy; else next.decks.Add(copy);
                if(!next.decks.Any(d => d.id == next.selectedId)) next.selectedId = copy.id;
                if(!string.IsNullOrEmpty(copy.catalogKey)){
                    if(!next.importedCatalogDecks.Contains(copy.catalogKey)) next.importedCatalogDecks.Add(copy.catalogKey);
#if UNITY_EDITOR
                    if(CanWriteCatalog){
                        var baseline = DeckCatalogImporter.Clone(copy);
                        baseline.id = copy.catalogKey;
                        next.catalogDeckBaselines.RemoveAll(d => d.id == baseline.id);
                        next.catalogDeckBaselines.Add(baseline);
                    }
#endif
                }
            }, out error);
        }

        public bool EnsureStoryDeck(StoryData story, out string deckId, out string error){
            deckId = null; error = "Select a configured story.";
            if(!story) return false;
            var existing = data.decks.Find(d => d.classes.Contains(story.Class) && DeckRules.Validate(d, Catalog, data.owned, out _));
            if(existing != null){ deckId = existing.id; error = ""; return true; }
            error = "Create a deck containing " + Catalog.Class(story.Class).DisplayName + " to begin.";
            if(!story.playerStarterDeck) return false;
            var starter = story.playerStarterDeck.CreateRecord();
            if(!starter.classes.Contains(story.Class)) return false;
            if(!DeckRules.Validate(starter, Catalog, data.owned, out error)) return false;
            starter.id = story.id + "/starter";
            if(data.decks.Any(d => d.id == starter.id)) starter.id = Guid.NewGuid().ToString("N");
            if(!Commit(next => next.decks.Add(starter.Copy()), out error)) return false;
            deckId = starter.id;
            return true;
        }
        
        public bool Select(string id, out string error){
            error = "Deck no longer exists.";
            if(!data.decks.Any(d => d.id == id)) return false;
            return Commit(next => next.selectedId = id, out error);
        }

        public bool Favorite(string id, out string error) => Commit(next => {
            var deck = next.decks.Find(d => d.id == id); if(deck != null) deck.favorite = !deck.favorite;
        }, out error);
        
        public bool Delete(string id, out string error){
            error = "Deck no longer exists.";
            
            if(!data.decks.Any(d => d.id == id)) return false;
            error = "You can't delete a deck when you have only 1 deck. Create another deck first.";
            
            if(!CanDeleteDeck) return false;
            return Commit(next => {
                next.decks.RemoveAll(d => d.id == id);
                if(next.selectedId == id) next.selectedId = next.decks[0].id;
            }, out error);
        }
        
        public bool Unlock(CardData card, out string error){
            error = "Card must be registered in the catalog.";
            if(!card || Catalog.Card(card.id) != card) return false;
            return Commit(next => { if(!next.owned.Contains(card.id)) next.owned.Add(card.id); }, out error);
        }

        public bool IsPuzzleCompleted(PuzzleData puzzle) => puzzle && data.completedPuzzles.Contains(puzzle.id);

#if UNITY_EDITOR
        public bool SetPuzzleClearedForDebugging(PuzzleData puzzle, bool cleared, out string error){
            error = "Puzzle needs a stable ID.";
            if(!puzzle || string.IsNullOrWhiteSpace(puzzle.id)) return false;
            if(cleared) return CompletePuzzle(puzzle, out error);
            if(!IsPuzzleCompleted(puzzle)) { error = ""; return true; }
            return Commit(next => next.completedPuzzles.RemoveAll(id => id == puzzle.id), out error);
        }

        public bool SetAuditionClearedForDebugging(StoryData story, CharacterClass opponent, bool cleared, out string error){
            error = "Story needs a permanent save key and a matching audition.";
            if(!story || string.IsNullOrWhiteSpace(story.id) || story.Audition(opponent) == null) return false;
            if(cleared) return CompleteAudition(story, opponent, out error);
            if(!IsAuditionCompleted(story, opponent)) { error = ""; return true; }
            return Commit(next => next.storyClears.RemoveAll(record =>
                record != null && record.storyId == story.id && record.opponent == opponent), out error);
        }
#endif

        [Serializable]
        public sealed class StoryClearRecord
        {
            public string storyId;
            public CharacterClass opponent;
            public string firstClearUtc;
        }

        public string StoryFirstClear(StoryData story, CharacterClass opponent) => !story ? null :
            data.storyClears.Find(c => c != null && c.storyId == story.id && c.opponent == opponent)?.firstClearUtc;

        public bool IsAuditionCompleted(StoryData story, CharacterClass opponent) => StoryFirstClear(story, opponent) != null;
        public bool IsAuditionUnlocked(StoryData story, CharacterClass opponent){
            if(!story || story.auditions == null) return false;
            int index = story.auditions.FindIndex(a => a != null && a.opponent == opponent);
            return index >= 0 && (!story.tutorial || story.auditions.Take(index).All(a => a != null && IsAuditionCompleted(story, a.opponent)));
        }
        public int StoryClearCount(StoryData story) => !story || story.auditions == null ? 0 : story.auditions
            .Where(a => a != null).Select(a => a.opponent).Distinct().Count(c => IsAuditionCompleted(story, c));
        public bool IsStoryCompleted(StoryData story) => story && story.Validate(out _) && StoryClearCount(story) == 5;

        public bool HasViewedStoryDialogue(StoryData story, bool ending) => story &&
            data.viewedStoryDialogues.Contains(story.id + (ending ? "/epilogue" : "/prologue"));

        public bool MarkStoryDialogueViewed(StoryData story, bool ending, out string error){
            error = "Story needs a permanent save key.";
            if(!story || string.IsNullOrWhiteSpace(story.id)) return false;
            if(HasViewedStoryDialogue(story, ending)) { error = ""; return true; }
            return Commit(next => next.viewedStoryDialogues.Add(story.id + (ending ? "/epilogue" : "/prologue")), out error);
        }

        public bool CompleteAudition(StoryData story, CharacterClass opponent, out string error)
        {
            error = "Story or audition is not configured.";
            if(!story || !story.Validate(out error)) return false;
            var audition = story.Audition(opponent);
            if(audition == null) return false;
            if(IsAuditionCompleted(story, opponent)) { error = ""; return true; }
            error = "Register the audition's first clear reward in the deck catalog.";
            if((!story.tutorial || audition.firstClearReward) && (!audition.firstClearReward || Catalog.Card(audition.firstClearReward.id) != audition.firstClearReward)) return false;
            bool saved = Commit(next => {
                next.storyClears.Add(new StoryClearRecord { storyId = story.id, opponent = opponent,
                    firstClearUtc = DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture) });
                if(audition.firstClearReward && !next.owned.Contains(audition.firstClearReward.id)) next.owned.Add(audition.firstClearReward.id);
            }, out error);
            if(saved && story.tutorial && IsStoryCompleted(story)) TutorialProgress.SetCompleted();
            return saved;
        }

        public bool CompletePuzzle(PuzzleData puzzle, out string error){
            error = "Puzzle needs a stable ID and a reward registered in the deck catalog.";
            if(!puzzle || string.IsNullOrWhiteSpace(puzzle.id) || !puzzle.rewardCard ||
                Catalog.Card(puzzle.rewardCard.id) != puzzle.rewardCard) return false;
            if(IsPuzzleCompleted(puzzle)) { error = ""; return true; }
            return Commit(next => {
                next.completedPuzzles.Add(puzzle.id);
                if(!next.owned.Contains(puzzle.rewardCard.id)) next.owned.Add(puzzle.rewardCard.id);
            }, out error);
        }
    }
}
