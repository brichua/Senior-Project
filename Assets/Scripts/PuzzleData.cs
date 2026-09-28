using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using VocaloidTCG.BoardUI;

namespace VocaloidTCG
{
    public enum PuzzleObjectiveKind { ReachTotalScore, ScoreInOneRound, ClearEnemyCenter, ClearAllEnemies }

    [Serializable]
    public sealed class PuzzleSideSetup
    {
        [Range(0, 8)] public int energy = 3;
        [Min(0)] public int score;
        [FormerlySerializedAs("hand")]
        public List<CardData> startingHand = new List<CardData>();
        [Tooltip("Cards received at the start of this side's later turns. No cards are drawn automatically.")]
        public List<PuzzleTurnCards> cardsOnTurns = new List<PuzzleTurnCards>();
    }

    [Serializable]
    public sealed class PuzzleTurnCards
    {
        [Tooltip("This side's own turn number: 2 means its second turn, across Preparation and Performance. Use Starting Hand for initial cards.")]
        [Min(2)] public int turn = 2;
        public List<CardData> cards = new List<CardData>();
    }

    [Serializable]
    public sealed class PuzzlePlacement
    {
        public CardData card;
        public bool enemy;
        [Range(0, 4)] public int column, row;
        [Tooltip("-1 uses the card's original influence.")]
        public int influence = -1;
    }

    [Serializable]
    public sealed class PuzzleEnemyAction
    {
        public BoardActionKind kind;
        [Tooltip("For PlayCard, play the first matching card from the enemy hand.")]
        public CardData card;
        [Range(0, 4)] public int fromColumn, fromRow, toColumn, toRow;
    }

    [Serializable]
    public sealed class PuzzleEnemyTurn
    {
        [Tooltip("Actions run in order, then the enemy passes. An omitted turn is a pass.")]
        public List<PuzzleEnemyAction> actions = new List<PuzzleEnemyAction>();
    }

    [CreateAssetMenu(menuName = "Vocaloid TCG/Puzzle")]
    public sealed class PuzzleData : ScriptableObject
    {
        public string id;
        [Min(1)] public int number = 1;
        public string puzzleName;
        [TextArea] public string shortDescription;
        [TextArea(3, 10)] public string longDescription;
        public CharacterClassData puzzleClass, againstClass;
        public CardData rewardCard;
        public BoardSetup boardSetup;
        public PuzzleSideSetup player = new PuzzleSideSetup(), enemy = new PuzzleSideSetup();
        public List<PuzzlePlacement> placements = new List<PuzzlePlacement>();
        public List<PuzzleEnemyTurn> enemyTurns = new List<PuzzleEnemyTurn>();
        public bool enemyStarts;
        [Tooltip("0 disables the timer so puzzle attempts are not affected by time pressure.")]
        [Min(0)] public float turnSeconds;
        public RoundPhase startingPhase = RoundPhase.Preparation;
        [Min(1)] public int startingRound = 1;
        public PuzzleObjectiveKind objective;
        [Min(1)] public int targetScore = 10;
        [Tooltip("0 = unlimited. Counts rounds resolved from the starting situation.")]
        [Min(0)] public int roundLimit = 1;
        [Tooltip("0 = unlimited. A player turn ends when the player passes; the enemy gets its response before the deadline is checked.")]
        [Min(0)] public int playerTurnLimit;

        public bool Validate(out string error){
            var issues = new List<string>();
            if(string.IsNullOrWhiteSpace(id)) issues.Add("ID: missing. Re-save this asset to generate its ID.");
            if(!puzzleClass) issues.Add("Puzzle Class: assign the class this puzzle belongs to.");
            if(!againstClass) issues.Add("Against Class: assign the opponent's class.");
            
            CheckCard(rewardCard, "Reward Card", issues);
            if(rewardCard && !rewardCard.cardClass) issues.Add("Reward Card > Card Class: assign a class on " + rewardCard.name + ".");
            if(startingPhase != RoundPhase.Preparation && startingPhase != RoundPhase.Performance)
                issues.Add("Starting Phase: choose Preparation or Performance.");
            
            if(startingRound < 1) issues.Add("Starting Round: must be at least 1.");
            if(targetScore < 1) issues.Add("Target Score: must be at least 1.");
            if(roundLimit < 0) issues.Add("Round Limit: must be 0 (unlimited) or higher.");
            if(playerTurnLimit < 0) issues.Add("Player Turn Limit: must be 0 (unlimited) or higher.");
            if(turnSeconds < 0) issues.Add("Turn Seconds: must be 0 (no timer) or higher.");
            if(!Enum.IsDefined(typeof(PuzzleObjectiveKind), objective)) issues.Add("Objective: choose a supported objective.");
            
            if(boardSetup) {
                if(puzzleClass && boardSetup.PlayerClass != puzzleClass) issues.Add("Board Setup > Player Class: must match Puzzle Class.");
                if(againstClass && boardSetup.EnemyClass != againstClass) issues.Add("Board Setup > Enemy Class: must match Against Class.");
            }
            
            CheckSide(player, "Player", issues);
            CheckSide(enemy, "Enemy", issues);
            var occupied = new Dictionary<string, int>();
            
            if(placements == null) issues.Add("Placements: list is missing.");
            else for(int i = 0; i < placements.Count; i++){
                var entry = placements[i];
                string path = "Placements[" + i + "]";
                
                if(entry == null){
                    issues.Add(path + ": entry is missing.");
                    continue;
                }
                CheckCard(entry.card, path + " > Card", issues);
                
                if(entry.card && !entry.card.performer) issues.Add(path + " > Card: must be a performer.");
                CheckCoordinate(entry.column, path + " > Column", issues);
                CheckCoordinate(entry.row, path + " > Row", issues);
                
                if(entry.influence < -1) issues.Add(path + " > Influence: use -1 for the card value or 0 and above.");
                string key = entry.enemy + ":" + entry.column + ":" + entry.row;
                if(occupied.TryGetValue(key, out int previous))
                    issues.Add(path + ": duplicates the " + (entry.enemy ? "enemy" : "player") + " tile in Placements[" + previous + "].");
                else occupied.Add(key, i);
            }

            if(enemyTurns == null) issues.Add("Enemy Turns: list is missing.");
            else for(int i = 0; i < enemyTurns.Count; i++){
                var turn = enemyTurns[i];
                string path = "Enemy Turns[" + i + "]";
                
                if(turn == null){
                    issues.Add(path + ": entry is missing.");
                    continue;
                }
                if(turn.actions == null){
                    issues.Add(path + " > Actions: list is missing.");
                    continue;
                }

                for(int j = 0; j < turn.actions.Count; j++){
                    var action = turn.actions[j];
                    string actionPath = path + " > Actions[" + j + "]";
                    
                    if(action == null){
                        issues.Add(actionPath + ": entry is missing.");
                        continue;
                    }

                    CheckCoordinate(action.toColumn, actionPath + " > To Column", issues);
                    CheckCoordinate(action.toRow, actionPath + " > To Row", issues);
                    
                    if(action.kind == BoardActionKind.PlayCard) CheckCard(action.card, actionPath + " > Card", issues);
                    else if(action.kind == BoardActionKind.MovePerformer){
                        CheckCoordinate(action.fromColumn, actionPath + " > From Column", issues);
                        CheckCoordinate(action.fromRow, actionPath + " > From Row", issues);
                    }else issues.Add(actionPath + " > Kind: choose Play Card or Move Performer.");
                }
            }
            error = issues.Count == 0 ? "" : "Puzzle \"" + name + "\" (#" + number + ") has incomplete or invalid fields:\n- " + string.Join("\n- ", issues);
            return issues.Count == 0;
        }

        private static void CheckSide(PuzzleSideSetup side, string path, List<string> issues){
            if(side == null){
                issues.Add(path + ": setup is missing.");
                return;
            }

            if(side.energy < 0 || side.energy > 8) issues.Add(path + " > Energy: must be between 0 and 8.");
            if(side.score < 0) issues.Add(path + " > Score: cannot be negative.");
            if(side.startingHand == null) issues.Add(path + " > Starting Hand: list is missing.");
            else{
                if(side.startingHand.Count > GameplayBoardBridge.MaxHandSize) issues.Add(path + " > Starting Hand: contains " + side.startingHand.Count + " cards; maximum is 8.");
                for(int i = 0; i < side.startingHand.Count; i++) CheckCard(side.startingHand[i], path + " > Starting Hand[" + i + "]", issues);
            }

            if(side.cardsOnTurns == null){
                issues.Add(path + " > Cards On Turns: list is missing.");
                return;
            }

            for(int i = 0; i < side.cardsOnTurns.Count; i++){
                var grant = side.cardsOnTurns[i];
                string grantPath = path + " > Cards On Turns[" + i + "]";
                
                if(grant == null) { issues.Add(grantPath + ": entry is missing."); continue; }
                if(grant.turn < 2) issues.Add(grantPath + " > Turn: must be at least 2; put initial cards in Starting Hand.");
                if(grant.cards == null) { issues.Add(grantPath + " > Cards: list is missing."); continue; }
                
                for(int j = 0; j < grant.cards.Count; j++) CheckCard(grant.cards[j], grantPath + " > Cards[" + j + "]", issues);
            }
        }

        private static void CheckCard(CardData card, string path, List<string> issues){
            if(!card) issues.Add(path + ": assign a card.");
            else if(card.performer == card.stageEffect) issues.Add(path + ": card \"" + card.name + "\" must have exactly one type enabled (Performer or Stage Effect).");
        }

        private static void CheckCoordinate(int value, string path, List<string> issues){
            if(value < 0 || value > 4) issues.Add(path + ": " + value + " is outside the board; use 0 through 4.");
        }

        [ContextMenu("Validate Puzzle")]
        private void LogValidation(){
            if(Validate(out string error)) Debug.Log("Puzzle \"" + name + "\": puzzle field validation passed.", this);
            else Debug.LogError(error, this);
        }

        private void OnValidate(){
#if UNITY_EDITOR
            var path = UnityEditor.AssetDatabase.GetAssetPath(this);
            if(!string.IsNullOrEmpty(path)) id = UnityEditor.AssetDatabase.AssetPathToGUID(path);
#endif
            if(string.IsNullOrWhiteSpace(id)) id = Guid.NewGuid().ToString("N");
        }
    }
}
