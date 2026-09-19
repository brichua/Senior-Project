using System;
using VocaloidTCG.BoardUI;

// Compiles the actual production scorer without Unity.
internal static class EnemyActionScorerChecks
{
    private static int checks;

    private static void Check(bool condition, string name)
    {
        checks++;
        if (!condition) throw new Exception(name);
    }

    public static int Main()
    {
        try
        {
            var centerPlay = EnemyActionScorer.Evaluate(2, -1, 3, 0, 2, false);
            var outerPlay = EnemyActionScorer.Evaluate(0, -1, 3, 0, 2, false);
            Check(centerPlay.score > outerPlay.score, "Center is preferred to an outer column");

            var winningContest = EnemyActionScorer.Evaluate(2, -1, 4, 2, 3, false);
            var losingContest = EnemyActionScorer.Evaluate(2, -1, 1, 2, 3, false);
            Check(winningContest.score > losingContest.score, "Winning contest is preferred to losing contest");
            Check(losingContest.score <= 0, "Clearly losing center contest is not useful");

            var towardCenter = EnemyActionScorer.Evaluate(2, 1, 2, 0, 0, true);
            var awayFromCenter = EnemyActionScorer.Evaluate(0, 1, 2, 0, 0, true);
            Check(towardCenter.score > awayFromCenter.score, "Move toward center is preferred");

            var sidewaysMove = EnemyActionScorer.Evaluate(2, 2, 2, 0, 0, true);
            Check(sidewaysMove.score == 0, "Move on the same scoring lane has no position value");

            var cheapPlay = EnemyActionScorer.Evaluate(1, -1, 2, 0, 1, false);
            var expensivePlay = EnemyActionScorer.Evaluate(1, -1, 2, 0, 7, false);
            Check(cheapPlay.score > expensivePlay.score, "Lower-cost equal play is preferred");

            Check(centerPlay.reason.Contains("center position"), "Evaluation includes a debug reason");
            Console.WriteLine("PASS: " + checks + " EnemyActionScorer checks.");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("FAIL after " + checks + " checks: " + e.Message);
            return 1;
        }
    }
}
