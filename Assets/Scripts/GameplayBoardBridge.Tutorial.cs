using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace VocaloidTCG.BoardUI
{
    public sealed partial class GameplayBoardBridge
    {
        private TutorialDirector tutorialDirector;
        public bool IsTutorial => IsStory && story.tutorial;
        public int TutorialLesson => IsTutorial ? story.auditions.FindIndex(a => a.opponent == storyOpponent) : -1;
        public StoryData TutorialStory => IsTutorial ? story : null;
        public bool TutorialCanPass => !tutorialDirector || tutorialDirector.AllowsPass(localPlayerId);
        public bool TutorialFreePlay => !tutorialDirector || tutorialDirector.FreePlay;
        public bool TutorialEnemyHasActed => IsTutorial && state != null && state.activePlayerId != localPlayerId && turnHadAction;
        public bool TutorialOpeningClassesPending => tutorialDirector && tutorialDirector.ExplainingOpeningClasses;
        public void ReleaseTutorialIntro(){ if(IsPlaying()) StartClock(); Publish(); }
        public void NotifyTutorialSelection(CardState card){ if(tutorialDirector) tutorialDirector.CardSelected(card); }

        public CardData TutorialCard(string name){
            var card = deckCatalog.cards.FirstOrDefault(c => c && string.Equals(c.cardName, name, StringComparison.OrdinalIgnoreCase));
            if(!card) throw new InvalidOperationException("Tutorial requires registered card: " + name);
            return card;
        }

        private bool ConfigureTutorialDecks(){
            MatchSetupError = "";
            try{
                if(!deckCatalog || !story.playerStarterDeck) throw new InvalidOperationException("Tutorial needs its catalog and Miku starter deck.");
                var starter = story.playerStarterDeck;
                localPlayerId = 0;
                if(starter.cards.Any(c => !c || !c.cardClass || c.cardClass.identity != CharacterClass.HatsuneMiku))
                    throw new InvalidOperationException("The tutorial starter must contain only Miku cards.");
                var opponent = deckCatalog.Class(storyOpponent);
                playerClasses = new List<CharacterClassData> { deckCatalog.Class(CharacterClass.HatsuneMiku) };
                enemyClasses = new List<CharacterClassData> { opponent };
                if(!opponent || !playerClasses[0]) throw new InvalidOperationException("Tutorial character classes are missing from the catalog.");
                var pool = deckCatalog.cards.Where(c => c && c.cardClass == opponent && c.performer && !c.vip && c.effect == CardEffect.None)
                    .OrderBy(c => c.influence).ThenBy(c => c.cost).ToArray();
                if(pool.Length == 0) pool = deckCatalog.cards.Where(c => c && c.cardClass == opponent && c.performer && !c.vip).OrderBy(c => c.cost).ToArray();
                if(pool.Length == 0) throw new InvalidOperationException("Tutorial opponent has no performer cards.");
                var enemyCards = Enumerable.Repeat(pool[0], 30).ToArray();
                if(TutorialLesson == 4){
                    if(!enemyDeck || enemyDeck.cards == null || enemyDeck.cards.Count == 0 ||
                        enemyDeck.cards.Any(c => !c || c.cardClass != opponent) ||
                        enemyDeck.cards.GroupBy(c => c).Any(g => g.Count() > 3))
                        throw new InvalidOperationException("The final tutorial needs a single-class easy enemy deck with at most three copies per card.");
                    enemyCards = enemyDeck.cards.ToArray();
                }
                player0Cards = localPlayerId == 0 ? starter.cards.ToArray() : enemyCards;
                player1Cards = localPlayerId == 1 ? starter.cards.ToArray() : enemyCards;
                player0OpeningCard = localPlayerId == 0 ? starter.vipCard : null;
                player1OpeningCard = localPlayerId == 1 ? starter.vipCard : null;
                selectedPlayerBack = starter.cardBack; selectedEnemyBack = null;
                playerCardBack = starter.cardBack ? starter.cardBack.image : null;
                chosenBoard = FindBoardSetup(playerClasses[0], opponent);
                enableOpeningSequence = TutorialLesson >= 3;
                winningScore = 50;
                if(enemyAI){ enemyAI.classes = enemyClasses; enemyAI.enabled = TutorialLesson == 4; }
                foreach(var name in TutorialDirector.RequiredCards(TutorialLesson)) TutorialCard(name);
                ConfigurationVersion++;
                return true;
            }catch(Exception ex){ MatchSetupError = ex.Message; return false; }
        }

        private void TutorialHand(int actor, params string[] names){
            state.Side(actor).hand.Clear();
            foreach(var name in names) state.Side(actor).hand.Add(MakeCard(TutorialCard(name), actor));
            state.Side(actor).hiddenHandCount = names.Length;
        }

        private void TutorialPerformer(int actor, string name, int x, int y){
            var card = MakeCard(TutorialCard(name), actor);
            SetPerformer(state.tiles[y * 5 + x], actor, card);
            placedTurns[card.instanceId] = -1;
        }

        private void PrepareTutorialBoard(){
            if(TutorialLesson == 4){ if(tutorialDirector) tutorialDirector.ResetLesson(); return; }
            int player = localPlayerId, enemy = 1 - player;
            drawPresentations.Clear();
            if(player != 0) throw new InvalidOperationException("Scripted tutorials require local player 0.");
            state.Side(enemy).hand.Clear(); state.Side(enemy).hiddenHandCount = 0;
            state.side0.energy = state.side1.energy = TutorialLesson == 0 ? 1 : 4;
            switch(TutorialLesson){
                case 0: TutorialHand(player, "Romeo and Cinderella"); break;
                case 1:
                    TutorialHand(player, "Romeo and Cinderella", "Glow", "From Y to Y");
                    TutorialPerformer(enemy, "Cool Me Down", 2, 2);
                    break;
                case 2:
                    TutorialHand(player, "Darling Dance", "Romeo and Cinderella");
                    TutorialPerformer(player, "Deep Sea Girl", 2, 2);
                    TutorialPerformer(enemy, "Fire Flower", 2, 2);
                    TutorialPerformer(enemy, "Butterfly on Your Right Shoulder", 3, 2);
                    break;
                case 3:
                    TutorialHand(player, "Rabbit Hole", "Deep Sea Girl", "Glow", "From Y to Y");
                    foreach(var card in state.Side(player).hand.Where(c => !c.data.vip))
                        drawPresentations.Enqueue(new DrawPresentation { card = card });
                    var rest = decks[player].ToArray(); decks[player].Clear();
                    decks[player].Enqueue(MakeCard(TutorialCard("Romeo and Cinderella"), player));
                    foreach(var card in rest) decks[player].Enqueue(card);
                    state.Side(player).deckCount = decks[player].Count;
                    TutorialHand(enemy, "Sadistic Love", "Meltdown", "Tengaku");
                    foreach(var card in state.Side(enemy).hand)
                        drawPresentations.Enqueue(new DrawPresentation { card = card });
                    break;
            }
            Recompute();
            if(tutorialDirector) tutorialDirector.ResetLesson();
        }

        public bool TutorialRedrawAllowed(IEnumerable<string> ids){
            if(!IsTutorial || TutorialLesson != 3) return true;
            var selected = (ids ?? Enumerable.Empty<string>()).ToArray();
            return tutorialDirector && tutorialDirector.ReadyForRedraw && selected.Length == 1 &&
                openingCards[localPlayerId].Any(c => c.instanceId == selected[0] && c.data == TutorialCard("From Y to Y"));
        }

        public bool FinishTutorialLesson(out string error){
            error = "";
            if(!IsTutorial || TutorialLesson >= 4 || !tutorialDirector || !tutorialDirector.ReadyToFinish) return false;
            try{
                if(!DeckLibrary.Get(deckCatalog).CompleteAudition(story, storyOpponent, out error)) return false;
                StorySaveError = "";
                StorySession.PlayAfterBattle = false;
                bool returned = ReturnToStory();
                if(!returned) error = "The story return scene is unavailable. Add it to the build scene list.";
                return returned;
            }catch(Exception ex){ error = ex.Message; return false; }
        }
    }
}

