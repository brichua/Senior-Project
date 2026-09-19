# Playtest log

Record reproducible technical evidence here. A passing test means the listed
steps produced the listed result; it does not prove the full game is finished.

## Entry format

| Date | Scene / build | Steps | Result | Follow-up |
|---|---|---|---|---|

## 2026-09-18 — Enemy AI first integration

| Date | Scene / build | Steps | Result | Follow-up |
|---|---|---|---|---|
| 2026-09-18 | `Game Bri - Enemy AI Test` in Unity 6000.5.4f1 | Start a local game at 3 energy; manually play `Deep Sea Girl` to the center column; press Pass; observe enemy turn and phase transition. | Player drag/drop and Pass reached `GameplayBoardBridge`; enemy selected a legal center-column play, then passed when no positive action remained. Two passes advanced Preparation to Performance; two more passes resolved the round and began Round 2. No project script error was reported in the Unity Console during this run. | Continue with illegal-placement, zero-energy, movement, pause, and win-condition cases. |
| 2026-09-18 | `Game Bri - Enemy AI Test` in Unity 6000.5.4f1 | Run a direct bridge regression: try an illegal right-side placement, pause and attempt Pass, place a cost-3 card, pass at 0 energy, resolve both phases, then complete EndRound. Start a second match with the enemy first. | The illegal action was rejected without changing hand or energy; pause rejected Pass; legal play spent energy; 0-energy Pass worked; Preparation and Performance both advanced after two passes; Round 2 began correctly. In the enemy-first match, the AI placed `Deep Sea Girl` at center `(2,0)`, spent its 3 energy, and passed; the player then became active. | Movement and win-condition behavior still need dedicated scenarios. |
| 2026-09-18 | `Game Bri - Enemy AI Test` in Unity 6000.5.4f1 | Verify movement restrictions and a short win path. | A performer could not move on the placement turn; it could move one horizontal tile during a later Preparation turn; a second move in that turn was rejected. With a temporary win score of 1 in the test run, center-column scoring finished the match for Player 0. | No blocking regression found in this checklist. Balance and broader card coverage remain future work. |

## Minimum regression checklist

Before presenting or merging gameplay changes, run these in the named scene and
record the outcome above:

1. Start: correct player/AI turn indication and nonzero energy shown.
2. Legal player placement: card appears, energy decreases, then Pass works.
3. Illegal placement: card does not alter the board or spend energy.
4. Enemy turn: enemy acts or passes after its delay; player input stays locked.
5. Zero energy: UI says to press Pass; Pass advances the game.
6. Phase transition: two consecutive passes move Preparation → Performance →
   EndRound → next round.
7. Pause and resume: no side can act while paused; state resumes afterward.
