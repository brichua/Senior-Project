# Architecture

## Runtime flow

```text
Player input (BoardUIController)
        |
        v
GameplayBoardBridge ----> BoardSnapshot ----> BoardUIController refreshes HUD, hand, and tiles
        ^
        |
EnemyAIController (only acts when the enemy owns the active turn)
```

`GameplayBoardBridge` owns the authoritative local game state: legal-action
checks, turn progression, pass handling, phase changes, scoring, and the
published `BoardSnapshot`. UI and AI request actions through the bridge; they
do not directly alter tiles, energy, or hands.

## Main components

| Component | Responsibility |
|---|---|
| `GameplayBoardBridge` | Rules, state, energy, phases, scoring, and action validation. |
| `BoardUIController` | Renders state; handles card drag/drop and Pass; explains whose turn it is. |
| `EnemyAIController` | Enumerates legal enemy actions, scores them, takes the best positive action or passes. |
| `EnemyActionScorer` | Pure, dependency-free utility scoring function used by the AI. |
| `EnemyAIPlaytestHarness` | Optional development helper that automatically plays Player 0 actions. Disabled in the manual test scene by default. |

## Ownership boundaries

- The UI only sends requests such as play, move, and pass.
- The bridge decides whether a request is legal and publishes the result.
- The AI only reads the public snapshot and uses the same bridge validation as
  the player. It does not inspect a hidden player hand.
- The test harness is not gameplay logic. Keep it disabled when manually
  testing a player experience.

## Why this is presentation-friendly

This separates *decision making* from *game rules*. We can say: “The bridge
keeps the game fair; the AI proposes a legal move; the scorer explains why that
move is preferred.”
