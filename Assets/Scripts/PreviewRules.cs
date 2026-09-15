using System;

namespace VocaloidTCG.BoardUI
{
    public static class PreviewRules
    {
        public static bool CanPlace(bool[,] friendly, int column, int row, int ownerId = 0){
            if(!InBounds(column, row) || friendly[column, row]) return false;
            int columnFromOwnLeft = ownerId == 1 ? 4 - column : column;
            if(columnFromOwnLeft <= 2) return true;
            
            for(int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++){
                if(dx == 0 && dy == 0) continue;
                int x = column + dx, y = row + dy;
                if(InBounds(x, y) && friendly[x, y]) return true;
            }
            return false;
        }

        public static bool CanMove(int fromColumn, int fromRow, int toColumn, int toRow, int placedTurn, int lastSideMoveTurn, int currentTurn){
            return InBounds(fromColumn, fromRow) && InBounds(toColumn, toRow) && fromRow == toRow && Math.Abs(fromColumn - toColumn) == 1 && placedTurn < currentTurn && lastSideMoveTurn < currentTurn;
        }

        private static bool InBounds(int x, int y){
            return x >= 0 && x < 5 && y >= 0 && y < 5;
        }
    }
}
