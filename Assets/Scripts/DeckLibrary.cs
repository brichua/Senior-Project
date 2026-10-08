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
        }
        private static DeckLibrary instance;
        private SaveData data;
        private readonly string path;
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
            if(instance != null) return instance;
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
                if(string.IsNullOrEmpty(starter.cardBackId)) starter.cardBackId = catalog.defaultCardBacks.First(b => b).id;
                data.owned = data.owned
                    .Concat(starter.cardIds).Concat(new[] { starter.vipCardId }).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
                
                if(!DeckRules.Validate(starter, catalog, data.owned, out error))
                    throw new InvalidOperationException("Invalid starter deck: " + error);
                
                data.decks.Add(starter); data.selectedId = starter.id;
                saveNeedsUpdate = true;
            }
            if(saveNeedsUpdate) Write(data);
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
            return loaded;
        }

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
            if(!DeckRules.Validate(deck, Catalog, data.owned, out error)) return false;
            
            return Commit(next => {
                var copy = deck.Copy();
                if(string.IsNullOrEmpty(copy.id)) copy.id = Guid.NewGuid().ToString("N");
                int index = next.decks.FindIndex(d => d.id == copy.id);
                if(index >= 0) next.decks[index] = copy; else next.decks.Add(copy);
                next.selectedId = copy.id;
            }, out error);
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
            if(!audition.firstClearReward || Catalog.Card(audition.firstClearReward.id) != audition.firstClearReward) return false;
            bool saved = Commit(next => {
                next.storyClears.Add(new StoryClearRecord { storyId = story.id, opponent = opponent,
                    firstClearUtc = DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture) });
                if(!next.owned.Contains(audition.firstClearReward.id)) next.owned.Add(audition.firstClearReward.id);
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
