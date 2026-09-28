using System;
using VocaloidTCG.BoardUI;

internal static class EnemyPlacementScorerChecks
{
    private static int checks;
    private static void Check(bool condition, string name)
    {
        checks++;
        if (!condition) throw new Exception(name);
    }

    private static EnemyActionEvaluation Hint(EnemyPlacementBoard board, string flag,
        int x, int y, int sourceX = -1, int sourceY = -1, bool losing = false)
    {
        return EnemyPlacementScorer.Evaluate(new EnemyActionEvaluation(0, ""),
            new[] { flag }, board, x, y, sourceX, sourceY, sourceX >= 0, losing);
    }

    private static EnemyActionEvaluation Play(EnemyPlacementBoard board, string flag, int x, int y, int enemyInfluence = 0)
    {
        var baseline = EnemyActionScorer.Evaluate(x, -1, 1, enemyInfluence, 2, false);
        return EnemyPlacementScorer.Evaluate(baseline, new[] { flag }, board,
            x, y, -1, -1, false, enemyInfluence > 1);
    }

    public static int Main()
    {
        try
        {
            const string ally = EnemyPlacementFlags.NearAllies;
            const string bug = EnemyPlacementFlags.NearContestedAllies;
            const string fps = EnemyPlacementFlags.ContestEntry;
            var empty = new EnemyPlacementBoard();
            Check(Hint(empty, ally, 0, 0).score == 0, "No ally means no support hint, including board corners");
            var board = new EnemyPlacementBoard();
            board.enemies[1, 1] = true;
            Check(Hint(board, ally, 0, 0).score == 0, "Enemy neighbor cannot activate ally hint");
            board.allies[1, 1] = true;
            Check(Hint(board, ally, 0, 0).score > 0, "Diagonal allies are included in eight-tile adjacency");
            Check(Hint(board, ally, 3, 3).score == 0, "Distant allies do not activate adjacency");
            Check(Play(board, ally, 0, 0).score > Play(board, ally, 2, 4).score,
                "Useful support position can outrank an empty center position");
            Check(Play(board, ally, 2, 2, 1).score > Play(board, ally, 0, 0).score,
                "Support card may still prefer a useful center contest");
            int oneAlly = Hint(board, ally, 0, 0).score;
            board.allies[0, 1] = true;
            Check(Hint(board, ally, 0, 0).score == oneAlly, "Vampire connection saturates at one ally");
            Check(Hint(board, bug, 0, 0).score > 0, "Bug finds a contested friendly neighbor");
            board.enemies[1, 1] = false;
            Check(Hint(board, bug, 0, 0).score == 0, "Uncontested allies do not activate Bug");
            board.enemies[1, 0] = true;
            Check(Hint(board, bug, 0, 0).score == 0, "Enemy-only neighbor is not a contested ally");
            board.enemies[1, 1] = true;
            board.enemies[0, 1] = true;
            Check(Hint(board, bug, 0, 0).score > Hint(board, bug, 2, 2).score,
                "Two eligible Bug targets outrank one");

            var moving = new EnemyPlacementBoard();
            moving.allies[1, 2] = true;
            Check(Hint(moving, ally, 2, 2, 1, 2).score == 0, "Moving performer is not its own neighbor");
            moving.allies[0, 2] = true;
            int lostSupport = Hint(moving, ally, 2, 2, 1, 2).score;
            Check(lostSupport < 0, "Moving away from a useful connection loses support value");
            var toward = EnemyActionScorer.Evaluate(2, 1, 1, 0, 0, true);
            var adjusted = EnemyPlacementScorer.Evaluate(toward, new[] { ally }, moving, 2, 2, 1, 2, true, false);
            Check(adjusted.score <= 0, "Do not automatically abandon support just to approach center");
            moving.allies[2, 3] = true;
            Check(Hint(moving, ally, 2, 2, 1, 2).score == 0, "Preserving support does not earn the bonus again");
            moving.enemies[2, 3] = true;
            Check(Hint(moving, bug, 2, 2, 1, 2).score == 0, "Equal contested support has zero movement gain");

            var target = new EnemyPlacementBoard();
            target.enemies[2, 2] = true;
            Check(Hint(target, fps, 2, 2).score > Hint(target, fps, 2, 1).score,
                "FPS entry prefers a tile containing an enemy");
            Check(Hint(target, fps, 2, 2, 1, 2).score == 0, "FPS cannot retrigger its entry hint by moving");
            Check(Play(target, fps, 2, 2, 3).score <= 0, "Unimplemented entry effect cannot rescue a losing contest");
            target.allies[1, 1] = true;
            Check(Play(target, ally, 2, 2, 3).score <= 0, "Support hints cannot rescue a losing contest");
            Check(Hint(target, ally, 2, 2, 1, 2, true).score <= 0,
                "Moving into a losing contest gains no destination support bonus");

            var baseline = new EnemyActionEvaluation(13, "baseline");
            Check(EnemyPlacementScorer.Evaluate(baseline, null, board, 0, 0, -1, -1, false, false).score == 13,
                "Missing flags preserve the baseline");
            Check(Hint(board, "not_a_supported_flag", 0, 0).score == 0, "Unknown flags are inert");
            var duplicates = EnemyPlacementScorer.Evaluate(new EnemyActionEvaluation(), new[] { ally, ally }, board,
                0, 0, -1, -1, false, false);
            Check(duplicates.score == Hint(board, ally, 0, 0).score, "Repeated tags do not stack");
            Check(duplicates.reason.Contains("placement hint"), "Logs distinguish hints from resolved effects");
            Check(board.allies[1, 1] && board.enemies[1, 1] && !board.allies[0, 0], "Scoring leaves occupancy unchanged");

            var cluster = new EnemyPlacementBoard();
            cluster.allies[0, 0] = true;
            int oneNeighbor = Hint(cluster, EnemyPlacementFlags.AllyCluster, 1, 1).score;
            cluster.allies[2, 2] = true;
            Check(Hint(cluster, EnemyPlacementFlags.AllyCluster, 1, 1).score > oneNeighbor, "Cluster hint prefers two allies to one");
            Check(Hint(cluster, EnemyPlacementFlags.NearEnemies, 1, 1).score == 0, "Enemy adjacency cannot be satisfied by allies");
            cluster.enemies[2, 2] = true;
            Check(Hint(cluster, EnemyPlacementFlags.NearEnemies, 1, 1).score > 0, "Debuff hint finds a diagonal enemy");

            var front = new EnemyPlacementBoard();
            front.enemies[3, 1] = true;
            Check(Hint(front, EnemyPlacementFlags.FrontPressure, 1, 1).score > 0, "Front pressure looks toward the opponent");
            Check(Hint(front, EnemyPlacementFlags.FrontPressure, 4, 1).score == 0, "Enemies behind the source do not count as front targets");
            Check(Hint(front, EnemyPlacementFlags.FrontPressure, 1, 2).score == 0, "Front pressure uses the same row");
            var mirroredFront = new EnemyPlacementBoard();
            mirroredFront.forwardDirection = -1;
            mirroredFront.enemies[1, 1] = true;
            Check(Hint(front, EnemyPlacementFlags.FrontPressure, 1, 1).score == Hint(mirroredFront, EnemyPlacementFlags.FrontPressure, 3, 1).score,
                "Front direction mirrors for the other actor");

            var solo = new EnemyPlacementBoard();
            Check(Hint(solo, EnemyPlacementFlags.SoloCenter, 2, 0).score > Hint(solo, EnemyPlacementFlags.SoloCenter, 1, 0).score,
                "Solo-center hint requires a center destination");
            solo.allies[2, 4] = true;
            Check(Hint(solo, EnemyPlacementFlags.SoloCenter, 2, 0).score == 0, "Another ally anywhere in the middle zone breaks solo");
            solo.allies[2, 4] = false;
            solo.enemies[2, 4] = true;
            Check(Hint(solo, EnemyPlacementFlags.SoloCenter, 2, 0).score == 0, "Enemy middle-zone occupancy also breaks solo in the draft convention");
            solo.enemies[2, 4] = false;
            solo.allies[2, 0] = true;
            Check(Hint(solo, EnemyPlacementFlags.SoloCenter, 2, 0, 1, 0).score == 0, "An existing center ally still blocks a proposed solo move");
            Check(Hint(solo, EnemyPlacementFlags.SoloCenter, 1, 0, 2, 0).score < 0, "Leaving the solo center loses the position benefit");

            var safety = new EnemyPlacementBoard();
            safety.enemies[2, 1] = true;
            Check(Hint(safety, EnemyPlacementFlags.Support, 2, 0).score > Hint(safety, EnemyPlacementFlags.Support, 2, 1).score,
                "Support prefers an uncontested tile without forbidding center");
            var winning = EnemyPlacementScorer.Evaluate(EnemyActionScorer.Evaluate(2, -1, 9, 2, 7, false),
                new[] { EnemyPlacementFlags.Support }, safety, 2, 1, -1, -1, false, false);
            var safeOuter = EnemyPlacementScorer.Evaluate(EnemyActionScorer.Evaluate(0, -1, 9, 0, 7, false),
                new[] { EnemyPlacementFlags.Support }, safety, 0, 1, -1, -1, false, false);
            Check(winning.score > safeOuter.score, "Strong support may still win a center contest");

            var spawn = new EnemyPlacementBoard();
            int openSpace = Hint(spawn, EnemyPlacementFlags.SpawnSpace, 0, 0).score;
            spawn.allies[0, 1] = true;
            spawn.allies[1, 0] = true;
            spawn.enemies[1, 1] = true;
            Check(openSpace > Hint(spawn, EnemyPlacementFlags.SpawnSpace, 0, 0).score, "Spawn hint prefers available neighboring space");
            Check(Hint(spawn, EnemyPlacementFlags.SpawnSpace, 0, 0).score == 0, "Friendly and enemy occupancy both block safe spawn space");

            var middle = new EnemyPlacementBoard();
            Check(Hint(middle, EnemyPlacementFlags.MiddlePressure, 0, 0).score == 0, "No enemy middle presence gives no middle-pressure hint");
            middle.enemies[2, 3] = true;
            Check(Hint(middle, EnemyPlacementFlags.MiddlePressure, 0, 0).score > 0, "Middle-pressure card is useful against an enemy in center");
            Check(Hint(middle, EnemyPlacementFlags.MiddlePressure, 0, 0).score == Hint(middle, EnemyPlacementFlags.MiddlePressure, 2, 0).score,
                "Global middle pressure does not require its source to be in center");
            Check(Hint(middle, EnemyPlacementFlags.MiddlePressure, 1, 0, 0, 0).score == 0,
                "Moving a global middle-pressure source does not invent new effect value");

            // Mirror every pair of squares: neither player orientation nor board
            // edges should change a relative adjacency preference.
            for (int x = 0; x < 5; x++) for (int y = 0; y < 5; y++)
            for (int ax = 0; ax < 5; ax++) for (int ay = 0; ay < 5; ay++)
            {
                var original = new EnemyPlacementBoard();
                original.allies[ax, ay] = true;
                var mirrored = new EnemyPlacementBoard();
                mirrored.allies[4 - ax, 4 - ay] = true;
                Check(Play(original, ally, x, y).score == Play(mirrored, ally, 4 - x, 4 - y).score,
                    "Mirrored board produces equivalent placement preference");
            }
            Console.WriteLine("PASS: " + checks + " EnemyPlacementScorer checks.");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("FAIL after " + checks + " checks: " + e.Message);
            return 1;
        }
    }
}
