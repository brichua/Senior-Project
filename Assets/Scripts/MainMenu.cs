using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VocaloidTCG
{
    public sealed class MainMenu : MonoBehaviour
    {
        public DeckCatalog catalog;
        public Button playButton, decksButton, puzzlesButton;
        public string gameScene = "Game", deckScene = "Edit Deck";
        public string puzzleScene = "Puzzles";
        public TMP_Text status;
        
        private void Awake()
        {
            if(playButton) playButton.onClick.AddListener(Play);
            if(decksButton) decksButton.onClick.AddListener(OpenDeckEditor);
            if(puzzlesButton) puzzlesButton.onClick.AddListener(OpenPuzzles);
            try{
                DeckLibrary.Get(catalog);
            }
            catch(Exception ex){
                DeckUI.Text(status, ex.Message);
            }
        }

        public void Play(){
            try{
                var library = DeckLibrary.Get(catalog); string error;
                if(!DeckRules.Validate(library.Selected, library.Catalog, library.Owned, out error)) { DeckUI.Text(status, error); return; }
                PuzzleLaunch.Clear();
                SceneManager.LoadScene(gameScene);
            }catch(Exception ex){
                DeckUI.Text(status, ex.Message); Debug.LogError(ex.Message, this);
            }
        }
        public void OpenDeckEditor(){ OpenScene(deckScene); }
        public void OpenPuzzles(){ OpenScene(puzzleScene); }
        private void OpenScene(string scene){
            try {
                if(!Application.CanStreamedLevelBeLoaded(scene)) throw new InvalidOperationException("Add \"" + scene + "\" to the build's scene list.");
                SceneManager.LoadScene(scene);
            } catch(Exception ex) { DeckUI.Text(status, ex.Message); Debug.LogError(ex.Message, this); }
        }
        private void OnDestroy(){
            if(playButton) playButton.onClick.RemoveListener(Play);
            if(decksButton) decksButton.onClick.RemoveListener(OpenDeckEditor);
            if(puzzlesButton) puzzlesButton.onClick.RemoveListener(OpenPuzzles);
        }
    }
}
