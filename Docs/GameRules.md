# Implemented game rules

This page describes the current `GameplayBoardBridge` implementation. It is a
record of what can be tested today, not a replacement for team design decisions.

## Board and turns

- The board is 5 by 5.
- A round begins in **Preparation**, then moves to **Performance**, then
  **EndRound**.
- Only the active player may act. The active player may play a legal card, move
  a legal performer, or pass.
- Two consecutive passes advance the phase. At EndRound, input is intentionally
  locked while the game displays scoring for 2 seconds, then a new round starts
  unless the match is finished.

## Energy and placement

- Each side receives round energy equal to the round number, clamped to 1–8.
- `minimumRoundEnergy` is a bridge setting. The normal scene uses 1; the Enemy
  AI test scene uses 3 so cost-3 cards are playable immediately.
- A card cannot be played when its cost exceeds remaining energy.
- Performers may be played during Preparation only, on an empty friendly slot.
- Initial placement is restricted to the owner's leftmost three columns; later
  placement can also use a tile adjacent to a friendly performer.

## Movement and scoring

- A performer may move one horizontal tile during Preparation.
- A performer cannot move on the same turn it was placed, and each side may
  make at most one movement per turn.
- End-of-round scoring totals influence in the center **column** (column index
  2) for each player and adds those totals to their scores.
- The default winning score is 50.

## Player-facing feedback

The phase label states whether it is the player's turn, the enemy's turn,
scoring, or pause. It also shows player energy and tells a player with zero
energy to press Pass. This avoids mistaking an intentional turn lock for a
frozen game.
