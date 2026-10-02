using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VocaloidTCG
{
    public sealed class StoryLogView : MonoBehaviour
    {
        public Image portrait;
        public TMP_Text speaker, line;

        [ContextMenu("Validate Log Row Setup")]
        public void ValidateSetup(){
            var issues = new System.Collections.Generic.List<string>();
            StoryDiagnostics.Require(issues, (nameof(portrait), portrait), (nameof(speaker), speaker), (nameof(line), line));
            StoryDiagnostics.Report(this, issues);
        }

        public void Bind(StoryLine entry){
            DeckUI.Image(portrait, entry.portrait);
            if(portrait) portrait.gameObject.SetActive(entry.portrait);
            DeckUI.Text(speaker, entry.speaker); DeckUI.Text(line, entry.text);
            if(line) line.maxVisibleCharacters = int.MaxValue;
        }
    }
}
