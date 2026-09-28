using System;
using System.Collections.Generic;

namespace VocaloidTCG.BoardUI
{
    public static class EnemyPlacementFlags
    {
        public const string NearAllies = "near_allies";
        public const string NearContestedAllies = "near_contested_allies";
        public const string ContestEntry = "contest_entry";
        public const string AllyCluster = "ally_cluster";
        public const string NearEnemies = "near_enemies";
        public const string FrontPressure = "front_pressure";
        public const string SoloCenter = "solo_center";
        public const string Support = "support";
        public const string SpawnSpace = "spawn_space";
        public const string MiddlePressure = "middle_pressure";
    }

    // Only visible performer occupancy, oriented relative to the acting player.
    public sealed class EnemyPlacementBoard
    {
        public readonly bool[,] allies = new bool[5, 5];
        public readonly bool[,] enemies = new bool[5, 5];
        public int forwardDirection = 1;
    }

    /// <summary>
    /// Soft placement hints, not effect execution or a combat preview. No influence
    /// is changed. Movement earns only the difference from the source position.
    /// </summary>
    public static class EnemyPlacementScorer
    {
        private const int AllyConnectionValue = 24;
        private const int ContestedAllyValue = 12;
        private const int EntryValue = 8;

        public static EnemyActionEvaluation Evaluate(EnemyActionEvaluation baseline,
            IEnumerable<string> flags, EnemyPlacementBoard board,
            int toX, int toY, int fromX, int fromY, bool isMove, bool losingDestination)
        {
            if (flags == null) return baseline;
            // Duplicate tags must not multiply a bonus; unknown tags are inert.
            var hints = new HashSet<string>(flags, StringComparer.Ordinal);
            if (hints.Contains(EnemyPlacementFlags.NearAllies))
            {
                int target = AdjacentAllies(board, toX, toY, fromX, fromY, false) > 0 ? AllyConnectionValue : 0;
                int source = isMove && AdjacentAllies(board, fromX, fromY, fromX, fromY, false) > 0 ? AllyConnectionValue : 0;
                AddHint(ref baseline, EnemyPlacementFlags.NearAllies, target, source, losingDestination);
            }
            if (hints.Contains(EnemyPlacementFlags.NearContestedAllies))
            {
                int target = Math.Min(2, AdjacentAllies(board, toX, toY, fromX, fromY, true)) * ContestedAllyValue;
                int source = isMove ? Math.Min(2, AdjacentAllies(board, fromX, fromY, fromX, fromY, true)) * ContestedAllyValue : 0;
                AddHint(ref baseline, EnemyPlacementFlags.NearContestedAllies, target, source, losingDestination);
            }
            if (!isMove && hints.Contains(EnemyPlacementFlags.ContestEntry))
                AddHint(ref baseline, EnemyPlacementFlags.ContestEntry, board.enemies[toX, toY] ? EntryValue : 0, 0, losingDestination);
            if (hints.Contains(EnemyPlacementFlags.AllyCluster))
                AddHint(ref baseline, EnemyPlacementFlags.AllyCluster,
                    Math.Min(2, AdjacentAllies(board, toX, toY, fromX, fromY, false)) * 12,
                    isMove ? Math.Min(2, AdjacentAllies(board, fromX, fromY, fromX, fromY, false)) * 12 : 0, losingDestination);
            if (hints.Contains(EnemyPlacementFlags.NearEnemies))
                AddHint(ref baseline, EnemyPlacementFlags.NearEnemies,
                    AdjacentEnemies(board, toX, toY) > 0 ? 12 : 0,
                    isMove && AdjacentEnemies(board, fromX, fromY) > 0 ? 12 : 0, losingDestination);
            if (hints.Contains(EnemyPlacementFlags.FrontPressure))
                AddHint(ref baseline, EnemyPlacementFlags.FrontPressure,
                    FrontValue(board, toX, toY), isMove ? FrontValue(board, fromX, fromY) : 0, losingDestination);
            if (hints.Contains(EnemyPlacementFlags.SoloCenter))
                AddHint(ref baseline, EnemyPlacementFlags.SoloCenter,
                    SoloCenterValue(board, toX, fromX, fromY), isMove ? SoloCenterValue(board, fromX, fromX, fromY) : 0, losingDestination);
            if (hints.Contains(EnemyPlacementFlags.Support))
                AddHint(ref baseline, EnemyPlacementFlags.Support,
                    board.enemies[toX, toY] ? 0 : 6, isMove && !board.enemies[fromX, fromY] ? 6 : 0, losingDestination);
            if (hints.Contains(EnemyPlacementFlags.SpawnSpace))
                AddHint(ref baseline, EnemyPlacementFlags.SpawnSpace,
                    SpawnValue(board, toX, toY, fromX, fromY), isMove ? SpawnValue(board, fromX, fromY, fromX, fromY) : 0, losingDestination);
            if (hints.Contains(EnemyPlacementFlags.MiddlePressure))
            {
                int value = 0;
                for (int y = 0; y < 5; y++) if (board.enemies[2, y]) value += 6;
                value = Math.Min(12, value);
                // This effect's target zone is independent of the source tile.
                AddHint(ref baseline, EnemyPlacementFlags.MiddlePressure, value, isMove ? value : 0, losingDestination);
            }
            return baseline;
        }

        private static int AdjacentEnemies(EnemyPlacementBoard board, int x, int y)
        {
            int count = 0;
            for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
            {
                int nx = x + dx, ny = y + dy;
                if ((dx != 0 || dy != 0) && nx >= 0 && nx < 5 && ny >= 0 && ny < 5 && board.enemies[nx, ny]) count++;
            }
            return count;
        }

        private static int FrontValue(EnemyPlacementBoard board, int x, int y)
        {
            int count = 0;
            int direction = board.forwardDirection < 0 ? -1 : 1;
            for (int nx = x + direction; nx >= 0 && nx < 5; nx += direction)
                if (board.enemies[nx, y]) count++;
            return Math.Min(2, count) * 12;
        }

        private static int SoloCenterValue(EnemyPlacementBoard board, int x, int sourceX, int sourceY)
        {
            if (x != 2) return 0;
            for (int y = 0; y < 5; y++)
                if (board.enemies[2, y] || (board.allies[2, y] && !(sourceX == 2 && sourceY == y))) return 0;
            return 24;
        }

        private static int SpawnValue(EnemyPlacementBoard board, int x, int y, int sourceX, int sourceY)
        {
            int available = 0;
            for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
            {
                int nx = x + dx, ny = y + dy;
                if ((dx == 0 && dy == 0) || nx < 0 || nx >= 5 || ny < 0 || ny >= 5) continue;
                bool occupiedByAlly = board.allies[nx, ny] && !(nx == sourceX && ny == sourceY);
                if (!occupiedByAlly && !board.enemies[nx, ny]) available++;
            }
            return Math.Min(2, available) * 4;
        }

        private static int AdjacentAllies(EnemyPlacementBoard board, int x, int y,
            int excludedX, int excludedY, bool contestedOnly)
        {
            int count = 0;
            for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
            {
                int neighborX = x + dx, neighborY = y + dy;
                if ((dx == 0 && dy == 0) || neighborX < 0 || neighborX >= 5 || neighborY < 0 || neighborY >= 5 ||
                    (neighborX == excludedX && neighborY == excludedY)) continue;
                if (board.allies[neighborX, neighborY] && (!contestedOnly || board.enemies[neighborX, neighborY])) count++;
            }
            return count;
        }

        private static void AddHint(ref EnemyActionEvaluation evaluation, string name,
            int targetValue, int sourceValue, bool losingDestination)
        {
            // Until special effects execute in gameplay, do not use an intended
            // effect to turn a currently losing contest into a positive choice.
            int delta = (losingDestination ? 0 : targetValue) - sourceValue;
            evaluation.score += delta;
            string reason = name + " placement hint " + (delta >= 0 ? "+" : "") + delta;
            if (losingDestination && targetValue > 0) reason += " (target bonus suppressed: losing contest)";
            evaluation.reason = string.IsNullOrEmpty(evaluation.reason) ? reason : evaluation.reason + "; " + reason;
        }
    }
}
