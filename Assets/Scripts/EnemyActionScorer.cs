using System;

namespace VocaloidTCG.BoardUI
{
    /// <summary>
    /// The result of scoring one legal enemy action. Keeping the reason next to the
    /// score makes it easy to print a useful explanation during playtesting.
    /// </summary>
    public struct EnemyActionEvaluation
    {
        public int score;
        public string reason;

        public EnemyActionEvaluation(int score, string reason)
        {
            this.score = score;
            this.reason = reason;
        }
    }

    /// <summary>
    /// A small, deterministic utility scorer for the first enemy-AI version.
    /// It evaluates one already-legal action; it never changes game state or
    /// reads hidden player information.
    /// </summary>
    public static class EnemyActionScorer
    {
        private const int CenterLane = 2;
        private const int CenterBonus = 20;
        private const int StepTowardCenterBonus = 5;
        private const int WinningContestBonus = 25;
        private const int TyingContestBonus = 5;
        private const int LosingContestPenalty = -30;
        private const int MovingTowardCenterBonus = 15;
        private const int MovingAwayFromCenterPenalty = -10;

        /// <summary>
        /// Scores one action using only visible, current-board information.
        /// sourceLane is ignored for plays and used only for moves. A caller can
        /// pass either a row or a column, depending on the rule used to score the board.
        /// </summary>
        public static EnemyActionEvaluation Evaluate(
            int targetLane,
            int sourceLane,
            int influence,
            int opponentInfluence,
            int energyCost,
            bool isMove)
        {
            int score = 0;
            string reason = "";

            int targetPositionValue = PositionValue(targetLane);
            int positionValue = targetPositionValue;
            if (isMove)
            {
                positionValue -= PositionValue(sourceLane);
                AddReason(ref reason, "position change " + Signed(positionValue));
            }
            else AddReason(ref reason, "center position " + Signed(positionValue));
            score += positionValue;

            if (opponentInfluence > 0)
            {
                if (influence > opponentInfluence)
                {
                    score += WinningContestBonus;
                    AddReason(ref reason, "winning contest +" + WinningContestBonus);
                }
                else if (influence == opponentInfluence)
                {
                    score += TyingContestBonus;
                    AddReason(ref reason, "tying contest +" + TyingContestBonus);
                }
                else
                {
                    score += LosingContestPenalty;
                    AddReason(ref reason, "losing contest " + LosingContestPenalty);
                }
            }

            if (isMove)
            {
                int sourceDistance = Math.Abs(sourceLane - CenterLane);
                int targetDistance = Math.Abs(targetLane - CenterLane);
                if (targetDistance < sourceDistance)
                {
                    score += MovingTowardCenterBonus;
                    AddReason(ref reason, "move toward center +" + MovingTowardCenterBonus);
                }
                else if (targetDistance > sourceDistance)
                {
                    score += MovingAwayFromCenterPenalty;
                    AddReason(ref reason, "move away from center " + MovingAwayFromCenterPenalty);
                }
            }
            else
            {
                // A lower-cost performer is slightly preferred when two plays
                // offer the same board value. This bonus is deliberately small.
                int efficiencyBonus = Math.Max(0, 8 - energyCost);
                score += efficiencyBonus;
                AddReason(ref reason, "energy efficiency +" + efficiencyBonus);
            }

            return new EnemyActionEvaluation(score, reason);
        }

        private static string Signed(int value)
        {
            return value >= 0 ? "+" + value : value.ToString();
        }

        private static int PositionValue(int lane)
        {
            return CenterBonus - StepTowardCenterBonus * Math.Abs(lane - CenterLane);
        }

        private static void AddReason(ref string reason, string item)
        {
            reason = string.IsNullOrEmpty(reason) ? item : reason + "; " + item;
        }
    }
}
