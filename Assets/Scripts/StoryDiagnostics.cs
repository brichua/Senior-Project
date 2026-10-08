using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VocaloidTCG
{
    public static class StoryDiagnostics
    {
        public static void Require(List<string> issues, params (string field, UnityEngine.Object value)[] fields){
            foreach(var field in fields)
                if(!field.value) issues.Add(field.field + ": missing reference. Assign it in the Inspector.");
        }

        public static void Text(List<string> issues, string field, string value){
            if(string.IsNullOrWhiteSpace(value)) issues.Add(field + ": text is empty.");
        }

        public static bool Report(UnityEngine.Object owner, IEnumerable<string> issues){
            bool valid = true;

            foreach(var issue in issues){
                valid = false;
                string location = owner is Component component ? Hierarchy(component.transform) : owner ? owner.name : "Missing object";
                Debug.LogError("[Story Setup] " + location + " > " + issue, owner);
            }

            return valid;
        }

        private static string Hierarchy(Transform transform){
            string path = transform.name;

            while(transform.parent){
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }

        public static void Scene(List<string> issues, string field, string scene){
            if(string.IsNullOrWhiteSpace(scene)){
                issues.Add(field + ": scene name is empty.");
                return;
            }
#if UNITY_EDITOR
            bool available = UnityEditor.EditorBuildSettings.scenes.Any(s => s.enabled &&
                (s.path == scene || Path.GetFileNameWithoutExtension(s.path) == scene));
#else
            bool available = Application.CanStreamedLevelBeLoaded(scene);
#endif
            if(!available) issues.Add(field + ": \"" + scene + "\" is missing or disabled in the build scene list.");
        }

        public static void CheckUI(StoryUI ui){
            var issues = new List<string>();
            Require(issues, (nameof(ui.catalog), ui.catalog),
                (nameof(ui.classPanel), ui.classPanel), (nameof(ui.mapPanel), ui.mapPanel), (nameof(ui.auditionPanel), ui.auditionPanel),
                (nameof(ui.storyInfo), ui.storyInfo), (nameof(ui.selectedDeckPanel), ui.selectedDeckPanel),
                (nameof(ui.classRoot), ui.classRoot), (nameof(ui.rivalRoot), ui.rivalRoot), (nameof(ui.mapRoot), ui.mapRoot), (nameof(ui.deckRoot), ui.deckRoot),
                (nameof(ui.classPrefab), ui.classPrefab), (nameof(ui.rivalPrefab), ui.rivalPrefab), (nameof(ui.mapPrefab), ui.mapPrefab), (nameof(ui.deckPrefab), ui.deckPrefab),
                (nameof(ui.difficultyLocked), ui.difficultyLocked), (nameof(ui.afterLocked), ui.afterLocked), (nameof(ui.afterUnlocked), ui.afterUnlocked), (nameof(ui.auditionPassed), ui.auditionPassed),
                (nameof(ui.begin), ui.begin), (nameof(ui.classBack), ui.classBack), (nameof(ui.mapBack), ui.mapBack), (nameof(ui.auditionBack), ui.auditionBack),
                (nameof(ui.prologue), ui.prologue), (nameof(ui.epilogue), ui.epilogue), (nameof(ui.before), ui.before), (nameof(ui.after), ui.after), (nameof(ui.editDeck), ui.editDeck), (nameof(ui.start), ui.start),
                (nameof(ui.selectedClassImage), ui.selectedClassImage), (nameof(ui.auditionClassImage), ui.auditionClassImage), (nameof(ui.rewardArt), ui.rewardArt),
                (nameof(ui.deckClass), ui.deckClass), (nameof(ui.deckSecondClass), ui.deckSecondClass), (nameof(ui.cardBack), ui.cardBack),
                (nameof(ui.epilogueImage), ui.epilogueImage), (nameof(ui.epilogueLockedSprite), ui.epilogueLockedSprite), (nameof(ui.epilogueUnlockedSprite), ui.epilogueUnlockedSprite),
                (nameof(ui.className), ui.className), (nameof(ui.storyHeader), ui.storyHeader), (nameof(ui.storyCount), ui.storyCount), (nameof(ui.overallCount), ui.overallCount),
                (nameof(ui.mapCount), ui.mapCount), (nameof(ui.auditionClassName), ui.auditionClassName), (nameof(ui.deckDescription), ui.deckDescription),
                (nameof(ui.location), ui.location), (nameof(ui.auditionText), ui.auditionText), (nameof(ui.rewardName), ui.rewardName),
                (nameof(ui.selectedDeckName), ui.selectedDeckName), (nameof(ui.deckCount), ui.deckCount), (nameof(ui.firstClear), ui.firstClear), (nameof(ui.status), ui.status),
                (nameof(ui.storySlider), ui.storySlider), (nameof(ui.overallSlider), ui.overallSlider), (nameof(ui.mapSlider), ui.mapSlider), (nameof(ui.dialogue), ui.dialogue));

            foreach(var panel in new[] { ui.classPanel, ui.mapPanel, ui.auditionPanel, ui.storyInfo, ui.dialogue ? ui.dialogue.panel : null })
                if(panel && ui.transform.IsChildOf(panel.transform)) issues.Add("StoryUI is inside \"" + panel.name + "\", which it hides. Move StoryUI to an always-active Canvas or manager.");

            if(!UnityEngine.Object.FindAnyObjectByType<EventSystem>()) issues.Add("EventSystem: no active EventSystem was found in the scene; UI buttons cannot receive input.");
            var canvas = ui.GetComponentInParent<Canvas>();
            if(canvas && !canvas.GetComponent<GraphicRaycaster>()) issues.Add("Canvas > GraphicRaycaster: missing. Add it to receive UI clicks.");
            Scene(issues, nameof(ui.mainScene), ui.mainScene); Scene(issues, nameof(ui.gameScene), ui.gameScene); Scene(issues, nameof(ui.deckScene), ui.deckScene);
            var pins = ui.locationPins;
            if(pins == null || pins.Length != 6) issues.Add("locationPins: assign six entries, one for each class.");
            var classes = new HashSet<CharacterClass>(); var buttons = new HashSet<Button>();

            for(int i = 0; i < (pins?.Length ?? 0); i++){
                var pin = pins[i]; string path = "locationPins[" + i + "]";
                if(pin == null){
                    issues.Add(path + ": entry is missing.");
                    continue;
                }

                if(!Enum.IsDefined(typeof(CharacterClass), pin.characterClass)) issues.Add(path + ".characterClass: invalid class.");
                if(!classes.Add(pin.characterClass)) issues.Add(path + ".characterClass: duplicate " + pin.characterClass + ".");
                Require(issues, (path + ".button", pin.button));
                if(pin.button && !buttons.Add(pin.button)) issues.Add(path + ".button: the same button is assigned to multiple pins.");
            }

            foreach(CharacterClass identity in Enum.GetValues(typeof(CharacterClass)))
                if(!classes.Contains(identity)) issues.Add("locationPins: missing pin for " + identity + ".");

            if(ui.difficultyButtons == null || ui.difficultyButtons.Length != 3) issues.Add("difficultyButtons: assign exactly three entries in Easy, Medium, Hard order.");

            for(int i = 0; i < 3; i++){
                var view = ui.difficultyButtons != null && i < ui.difficultyButtons.Length ? ui.difficultyButtons[i] : null;
                Require(issues, ("difficultyButtons[" + i + "] (" + (StoryDifficulty)i + ")", view));
                CheckChoice(view, "Difficulty");
            }

            Report(ui, issues);
            CheckChoice(ui.classPrefab, "Class");
            if(ui.tutorialPrefab && ui.tutorialPrefab != ui.classPrefab) CheckChoice(ui.tutorialPrefab, "Class");
            CheckChoice(ui.rivalPrefab, "Rival"); CheckChoice(ui.mapPrefab, "Map"); CheckChoice(ui.deckPrefab, "Deck");
            if(ui.dialogue) ui.dialogue.ValidateSetup();
            if(ui.catalog) ui.catalog.ValidateSetup(false);
        }

        private static void CheckChoice(StoryChoiceView view, string role){
            if(!view) return;
            var issues = new List<string>();
            if(role == "Class" || role == "Deck" || role == "Difficulty")
                Require(issues, (role + " view > button", view.button ? view.button : view.GetComponent<Button>()));
            if(role != "Map" && role != "Difficulty") Require(issues, (role + " view > title", view.title));
            if(role == "Rival" || role == "Map" || role == "Deck") Require(issues, (role + " view > art", view.art));
            if(role == "Class" || role == "Rival") Require(issues, (role + " view > classIcon", view.classIcon));
            if(role == "Map") Require(issues, (role + " view > border", view.border));
            if(role == "Class") Require(issues, ("Class view > count", view.count), ("Class view > progress", view.progress));
            if(role == "Class" || role == "Map"){
                if(!view.completed && !view.completionImage) issues.Add(role + " view: assign completed OR completionImage to show completion.");
                if(view.completionImage) Require(issues, (role + " view > completedSprite", view.completedSprite));
            }

            if(role == "Deck" || role == "Difficulty") Require(issues, (role + " view > highlight", view.highlight));
            Report(view, issues);
        }

        public static void Dialogue(List<string> issues, StoryDialogue dialogue, string path){
            if(dialogue?.lines == null || dialogue.lines.Count == 0){
                issues.Add(path + ".lines: no dialogue lines assigned; this scene will be skipped.");
                return;
            }

            for(int i = 0; i < dialogue.lines.Count; i++){
                var line = dialogue.lines[i]; string prefix = path + ".lines[" + i + "]";
                if(line == null){
                    issues.Add(prefix + ": line is missing.");
                    continue;
                }

                Text(issues, prefix + ".text", line.text);
                var usedSlots = new HashSet<int>();

                for(int j = 0; j < (line.avatars?.Count ?? 0); j++){
                    var avatar = line.avatars[j]; string avatarPath = prefix + ".avatars[" + j + "]";
                    if(avatar == null){
                        issues.Add(avatarPath + ": entry is missing.");
                        continue;
                    }

                    if(avatar.slot < 0 || avatar.slot > 5) issues.Add(avatarPath + ".slot: use a slot from 0 through 5.");
                    if(!usedSlots.Add(avatar.slot)) issues.Add(avatarPath + ".slot: duplicate slot " + avatar.slot + " on the same line.");
                    Require(issues, (avatarPath + ".sprite", avatar.sprite));
                }
            }
        }

        public static void Audition(List<string> issues, StoryAudition audition, DeckCatalog catalog, string path){
            Text(issues, path + ".deckDescription", audition.deckDescription); Text(issues, path + ".location", audition.location);
            Text(issues, path + ".auditionText", audition.auditionText);
            Require(issues, (path + ".firstClearReward", audition.firstClearReward));
            if(audition.firstClearReward){
                if(catalog && catalog.Card(audition.firstClearReward.id) != audition.firstClearReward)
                    issues.Add(path + ".firstClearReward: \"" + audition.firstClearReward.name + "\" is not registered in DeckCatalog.cards.");
                if(!audition.firstClearReward.iconImage && !audition.firstClearReward.cardImage)
                    issues.Add(path + ".firstClearReward > iconImage/cardImage: assign reward artwork on \"" + audition.firstClearReward.name + "\".");
            }

            foreach(StoryDifficulty difficulty in Enum.GetValues(typeof(StoryDifficulty))){
                var deck = audition.Deck(difficulty); string deckPath = path + "." + difficulty.ToString().ToLowerInvariant() + "Deck";
                Require(issues, (deckPath, deck)); if(!deck) continue;
                if(deck.classes == null || !deck.classes.Any(c => c && c.identity == audition.opponent))
                    issues.Add(deckPath + " > \"" + deck.name + "\".classes: must include " + audition.opponent + ".");
                if(deck.cards == null || deck.cards.Count == 0) issues.Add(deckPath + " > \"" + deck.name + "\".cards: enemy deck is empty.");
                else for(int j = 0; j < deck.cards.Count; j++) Require(issues, (deckPath + " > \"" + deck.name + "\".cards[" + j + "]", deck.cards[j]));
            }

            Dialogue(issues, audition.before, path + ".before"); Dialogue(issues, audition.after, path + ".after");
        }
    }
}
