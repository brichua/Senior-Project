using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VocaloidTCG
{
    [CreateAssetMenu(menuName = "Vocaloid TCG/Story Catalog")]
    public sealed class StoryCatalog : ScriptableObject
    {
        public DeckCatalog deckCatalog;
        public List<StoryData> stories = new List<StoryData>();
        public StoryData Tutorial => stories?.FirstOrDefault(s => s && s.tutorial);

        public bool Validate(out string error){
            var issues = new List<string>();
            StoryDiagnostics.Require(issues, (nameof(deckCatalog), deckCatalog));
            if(stories == null || stories.Count != 7) issues.Add("stories: assign seven assets (Tutorial plus six class stories).");
            var ids = new HashSet<string>();

            for(int i = 0; i < (stories?.Count ?? 0); i++){
                var story = stories[i]; string path = "stories[" + i + "]";
                if(!story){
                    issues.Add(path + ": StoryData asset is missing.");
                    continue;
                }

                if(!ids.Add(story.id ?? "")) issues.Add(path + " (" + story.name + ").id: duplicate save key \"" + story.id + "\".");
                if(!story.Validate(out var storyError)) issues.Add(path + " (" + story.name + "):\n" + storyError);
            }

            var assigned = (stories ?? new List<StoryData>()).Where(s => s).ToList();
            int tutorials = assigned.Count(s => s.tutorial);
            if(tutorials != 1) issues.Add("stories > tutorial: expected one Tutorial, found " + tutorials + ".");

            foreach(CharacterClass identity in System.Enum.GetValues(typeof(CharacterClass))){
                int count = assigned.Count(s => !s.tutorial && s.Class == identity);
                if(count != 1) issues.Add("stories > " + identity + ": expected one non-tutorial story, found " + count + ".");
            }

            error = string.Join("\n", issues);
            return issues.Count == 0;
        }

        [ContextMenu("Validate Story Catalog")]
        public void ValidateSetup(){
            ValidateSetup(true);
        }

        public void ValidateSetup(bool checkContent){
            if(!Validate(out var error)) StoryDiagnostics.Report(this, new[] { error });
            if(deckCatalog){
                if(deckCatalog.cards == null || deckCatalog.classes == null)
                    StoryDiagnostics.Report(deckCatalog, new[] { "cards/classes: catalog lists are missing." });
                else{
                    foreach(CharacterClass identity in System.Enum.GetValues(typeof(CharacterClass))){
                        var data = deckCatalog.Class(identity);
                        if(!data){
                            StoryDiagnostics.Report(deckCatalog, new[] { "classes: missing class asset for " + identity + "." });
                            continue;
                        }

                        var issues = new List<string>();
                        StoryDiagnostics.Require(issues, (nameof(data.classIcon), data.classIcon), (nameof(data.selectedButtonImage), data.selectedButtonImage));
                        if(!data.storyArt && !data.puzzleArt) issues.Add("storyArt/puzzleArt: assign at least one artwork sprite for rivals and map icons.");
                        StoryDiagnostics.Report(data, issues);
                    }
                }
            }

            if(checkContent)
                foreach(var story in (stories ?? new List<StoryData>()).Where(s => s).Distinct())
                    story.ValidateContent(deckCatalog && deckCatalog.cards != null ? deckCatalog : null);
        }
    }
}
