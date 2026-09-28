using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VocaloidTCG
{
    public sealed class PuzzleUI : MonoBehaviour
    {
        public PuzzleCatalog catalog;
        public string gameScene = "Game";
        public Transform classesRoot, collectionRoot;
        public PuzzleClassView classPrefab;
        public PuzzleCardView cardPrefab;
        public GameObject puzzleInformation, completedImage;
        public TMP_Text number, puzzleName, againstName, description, status;
        public Image againstIcon;
        public TMP_Text rewardName, rewardType, rewardCost, rewardInfluence, rewardDescription;
        public Image rewardArt, costImage, influenceImage;
        public Button startPuzzle;
        public Button backButton;
        public string mainScene = "Main";
        [Header("Total puzzle progress")]
        public TMP_Text totalPuzzleCount;
        public Slider totalPuzzleSlider;
        private DeckLibrary library;
        private CharacterClassData filter;
        private PuzzleData selected;
        private readonly List<GameObject> classViews = new List<GameObject>(), cardViews = new List<GameObject>();

        private void Awake(){
            if(startPuzzle) startPuzzle.onClick.AddListener(StartPuzzle);
            if(backButton) backButton.onClick.AddListener(BackToMain);
        }
        
        private void OnEnable(){
            if(puzzleInformation) puzzleInformation.SetActive(false);
            if(startPuzzle) startPuzzle.interactable = false;
            try{
                string error;
                if(!catalog) throw new InvalidOperationException("Assign a Puzzle Catalog.");
                if(!catalog.Validate(out error)) throw new InvalidOperationException(error);
                
                library = DeckLibrary.Get(catalog.deckCatalog);
                library.Changed += Refresh;
                Refresh();
            }catch(Exception ex){
                ReportError(ex.Message, catalog ? (UnityEngine.Object)catalog : this);
            }
        }

        private void OnDisable(){
            if(library != null) library.Changed -= Refresh;
        }
        
        private void OnDestroy(){
            if(startPuzzle) startPuzzle.onClick.RemoveListener(StartPuzzle);
            if(backButton) backButton.onClick.RemoveListener(BackToMain);
        }

        public void BackToMain(){
            if(!Application.CanStreamedLevelBeLoaded(mainScene)){
                ReportError("Main Scene: add \"" + mainScene + "\" to the build's scene list.", this);
                return;
            }

            try{
                SceneManager.LoadScene(mainScene);
            }
            catch(Exception ex){
                ReportError(ex.Message, this);
            }
        }

        private static void Clear(List<GameObject> views){
            foreach(var view in views) if(view){
                view.SetActive(false); Destroy(view);
            }
            views.Clear();
        }

        public void Refresh(){
            if(library == null) return;
            RenderTotalProgress();
            Clear(classViews);
            
            if(classesRoot && classPrefab) foreach(var cls in catalog.deckCatalog.classes.Where(c => c)){
                var puzzles = catalog.puzzles.Where(p => p && p.puzzleClass == cls).ToList();
                var view = Instantiate(classPrefab, classesRoot);
                view.gameObject.SetActive(true);
                view.Bind(cls, puzzles.Count(p => library.IsPuzzleCompleted(p)), puzzles.Count, () => Filter(cls));
                classViews.Add(view.gameObject);
            }
            RenderCollection(); RenderInformation();
        }

        private void RenderTotalProgress(){
            int total = catalog.puzzles.Count(p => p);
            int completed = catalog.puzzles.Count(p => p && library.IsPuzzleCompleted(p));
            DeckUI.Text(totalPuzzleCount, completed + "/" + total + " cleared");
            
            if(totalPuzzleSlider) {
                totalPuzzleSlider.minValue = 0;
                totalPuzzleSlider.maxValue = Mathf.Max(1, total);
                totalPuzzleSlider.wholeNumbers = true;
                totalPuzzleSlider.SetValueWithoutNotify(completed);
                totalPuzzleSlider.interactable = false;
            }
        }

        public void ShowAll(){
            Filter(null);
        }
        
        public void Filter(CharacterClassData cls){
            filter = cls; selected = null;
            RenderCollection();
            RenderInformation();
        }
        
        private void RenderCollection(){
            Clear(cardViews);
            if(collectionRoot && cardPrefab) foreach(var puzzle in catalog.puzzles.Where(p => p && (!filter || p.puzzleClass == filter)).OrderBy(p => p.number)){
                var view = Instantiate(cardPrefab, collectionRoot);
                view.gameObject.SetActive(true);
                view.Bind(puzzle, library.IsPuzzleCompleted(puzzle), () => Select(puzzle));
                cardViews.Add(view.gameObject);
            }
        }

        public void Select(PuzzleData puzzle){
            selected = puzzle;
            RenderInformation();
        }
        
        private void RenderInformation(){
            if(puzzleInformation) puzzleInformation.SetActive(selected);
            if(startPuzzle) startPuzzle.interactable = selected;
            if(!selected) return;
            
            DeckUI.Text(number, "puzzle " + selected.number); DeckUI.Text(puzzleName, selected.puzzleName);
            if(completedImage) completedImage.SetActive(library.IsPuzzleCompleted(selected));
            
            var against = selected.againstClass;
            DeckUI.Image(againstIcon, against ? against.classIcon : null);
            DeckUI.Text(againstName, against ? against.DisplayName : "");
            
            if(againstName) againstName.color = against ? against.color : Color.white;
            DeckUI.Text(description, selected.longDescription);
            
            var reward = selected.rewardCard;
            var cls = reward ? reward.cardClass : null;
            
            DeckUI.Image(rewardArt, reward ? reward.cardImage : null);
            DeckUI.Text(rewardName, reward ? reward.cardName : "");
            DeckUI.Text(rewardType, reward ? (reward.stageEffect ? "Stage Effect" : "Performer") : "");
            DeckUI.Text(rewardCost, reward ? "cost: " + reward.cost : "");
            DeckUI.Text(rewardInfluence, reward ? "inf: " + reward.influence : "");
            DeckUI.Text(rewardDescription, reward ? reward.info : "");
            DeckUI.Image(costImage, cls ? cls.costImage : null);
            DeckUI.Image(influenceImage, cls ? cls.popupImage : null);
        }

        public void StartPuzzle(){
            if(!selected) return;
            string error;
            
            if(!selected.Validate(out error)) { ReportError(error, selected); return; }
            if(!Application.CanStreamedLevelBeLoaded(gameScene)) { ReportError("Game Scene: add \"" + gameScene + "\" to the build's scene list.", this); return; }
            PuzzleLaunch.Prepare(selected, catalog.deckCatalog);
            
            try{
                SceneManager.LoadScene(gameScene);
            }catch(Exception ex){
                PuzzleLaunch.Clear(); ReportError(ex.Message, selected);
            }
        }

        private void ReportError(string message, UnityEngine.Object context){
            DeckUI.Text(status, message);
            Debug.LogError("[Puzzle UI] " + message, context);
        }
    }
}
