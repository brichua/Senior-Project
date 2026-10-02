using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VocaloidTCG
{
    public enum StoryDifficulty { Easy, Medium, Hard }

    [Serializable]
    public sealed class StoryAvatar
    {
        [Range(0, 5)] public int slot;
        public Sprite sprite;
    }

    [Serializable]
    public sealed class StoryLine
    {
        public string speaker;
        public Sprite portrait;
        [TextArea(2, 8)] public string text;
        [Tooltip("The complete set of visible avatars for this line. Omitted slots are hidden.")]
        public List<StoryAvatar> avatars = new List<StoryAvatar>();
    }

    [Serializable]
    public sealed class StoryDialogue
    {
        public List<StoryLine> lines = new List<StoryLine>();
    }

    [Serializable]
    public sealed class StoryAudition
    {
        public CharacterClass opponent;
        [TextArea] public string deckDescription;
        public string location;
        [TextArea] public string auditionText;
        public CardData firstClearReward;
        public DeckData easyDeck, mediumDeck, hardDeck;
        public StoryDialogue before = new StoryDialogue(), after = new StoryDialogue();

        public DeckData Deck(StoryDifficulty difficulty) => difficulty == StoryDifficulty.Hard ? hardDeck :
            difficulty == StoryDifficulty.Medium ? mediumDeck : easyDeck;
    }

    [CreateAssetMenu(menuName = "Vocaloid TCG/Story")]
    public sealed class StoryData : ScriptableObject
    {
        [Tooltip("Permanent save key. Do not change after releasing a story.")]
        public string id;
        public bool tutorial;
        public CharacterClass storyClass;
        public string header;
        public StoryDialogue prologue = new StoryDialogue(), epilogue = new StoryDialogue();
        public List<StoryAudition> auditions = new List<StoryAudition>();
        public CharacterClass Class => tutorial ? CharacterClass.HatsuneMiku : storyClass;

        public StoryAudition Audition(CharacterClass opponent) => auditions?.Find(a => a != null && a.opponent == opponent);

        public bool Validate(out string error){
            var issues = new List<string>();
            if(string.IsNullOrWhiteSpace(id)) issues.Add("id: missing permanent save key.");
            if(!Enum.IsDefined(typeof(CharacterClass), Class)) issues.Add("storyClass: invalid class.");
            if(auditions == null || auditions.Count != 5) issues.Add("auditions: assign exactly five opponents.");
            var seen = new HashSet<CharacterClass>();

            for(int i = 0; i < (auditions?.Count ?? 0); i++){
                var audition = auditions[i]; string path = "auditions[" + i + "]";
                if(audition == null){
                    issues.Add(path + ": entry is missing.");
                    continue;
                }

                if(!Enum.IsDefined(typeof(CharacterClass), audition.opponent)) issues.Add(path + ".opponent: invalid class.");
                if(audition.opponent == Class) issues.Add(path + ".opponent: cannot face the story's own class (" + Class + ").");
                if(!seen.Add(audition.opponent)) issues.Add(path + ".opponent: duplicate " + audition.opponent + ".");
            }

            foreach(CharacterClass identity in Enum.GetValues(typeof(CharacterClass)))
                if(identity != Class && !seen.Contains(identity)) issues.Add("auditions: missing opponent " + identity + ".");

            error = string.Join("\n", issues);
            return issues.Count == 0;
        }

        [ContextMenu("Validate Story Content")]
        private void ValidateContentMenu(){
            ValidateContent(null);
        }

        public void ValidateContent(DeckCatalog catalog){
            var issues = new List<string>();
            if(!Validate(out var error)) issues.Add(error);
            StoryDiagnostics.Text(issues, nameof(header), header);
            StoryDiagnostics.Dialogue(issues, prologue, nameof(prologue));
            StoryDiagnostics.Dialogue(issues, epilogue, nameof(epilogue));

            for(int i = 0; i < (auditions?.Count ?? 0); i++)
                if(auditions[i] != null)
                    StoryDiagnostics.Audition(issues, auditions[i], catalog, "auditions[" + i + "] (" + auditions[i].opponent + ")");

            StoryDiagnostics.Report(this, issues);
        }

        private void OnValidate(){
            if(string.IsNullOrWhiteSpace(id)) id = Guid.NewGuid().ToString("N");
        }
    }
}
