using System.Collections.Generic;
using UnityEngine;

namespace VocaloidTCG
{
    [CreateAssetMenu(menuName = "Vocaloid TCG/Puzzle Catalog")]
    public sealed class PuzzleCatalog : ScriptableObject
    {
        public DeckCatalog deckCatalog;
        public List<PuzzleData> puzzles = new List<PuzzleData>();

        public bool Validate(out string error){
            var issues = new List<string>();
            if(!deckCatalog) issues.Add("Deck Catalog: assign a Deck Catalog asset.");
            if(puzzles == null) issues.Add("Puzzles: list is missing.");
            var ids = new Dictionary<string, int>();
            if(puzzles != null) for(int i = 0; i < puzzles.Count; i++){
                var puzzle = puzzles[i];
                string path = "Puzzles[" + i + "]";
                
                if(!puzzle){
                    issues.Add(path + ": assign a Puzzle asset or remove the empty entry.");
                    continue;
                }
                path += " (" + puzzle.name + ")";
                
                if(!puzzle.Validate(out string puzzleError)) issues.Add(path + ": " + puzzleError);
                if(!string.IsNullOrWhiteSpace(puzzle.id)){
                    if(ids.TryGetValue(puzzle.id, out int previous)) issues.Add(path + " > ID: duplicates Puzzles[" + previous + "].");
                    else ids.Add(puzzle.id, i);
                }

                if(!deckCatalog) continue;
                if(puzzle.rewardCard && deckCatalog.Card(puzzle.rewardCard.id) != puzzle.rewardCard)
                    issues.Add(path + " > Reward Card: register " + puzzle.rewardCard.name + " in Deck Catalog > Cards.");
                if(puzzle.puzzleClass && deckCatalog.Class(puzzle.puzzleClass.identity) != puzzle.puzzleClass)
                    issues.Add(path + " > Puzzle Class: register this exact class asset in Deck Catalog > Classes.");
                if(puzzle.againstClass && deckCatalog.Class(puzzle.againstClass.identity) != puzzle.againstClass)
                    issues.Add(path + " > Against Class: register this exact class asset in Deck Catalog > Classes.");
            }
            error = issues.Count == 0 ? "" : "Puzzle Catalog \"" + name + "\" has setup errors:\n- " + string.Join("\n- ", issues);
            return issues.Count == 0;
        }

        [ContextMenu("Validate Puzzle Catalog")]
        private void LogValidation(){
            if(Validate(out string error)) Debug.Log("Puzzle Catalog \"" + name + "\": validation passed.", this);
            else Debug.LogError(error, this);
        }
    }

    public static class PuzzleLaunch
    {
        public static PuzzleData Pending { get; private set; }
        public static DeckCatalog Catalog { get; private set; }
        public static void Prepare(PuzzleData puzzle, DeckCatalog catalog) { Pending = puzzle; Catalog = catalog; }
        public static void Clear() { Pending = null; Catalog = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { Clear(); }
    }
}
