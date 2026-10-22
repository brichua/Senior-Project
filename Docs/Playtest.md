# Playtest log

Record reproducible technical evidence here. A passing test means the listed
steps produced the listed result; it does not prove the full game is finished.

## Entry format

| Date | Scene / build | Steps | Result | Follow-up |
|---|---|---|---|---|

## 2026-10-18 — Card effect AI and coverage presets

| Check | Actual result | Remaining work |
|---|---|---|
| Compile all runtime and Editor sources using Unity 6000.5.4f1 compiler/project references | PASS, no errors or warnings in final compile | This is compiler validation outside the running Editor, not a native playtest. |
| Temporary external compilation/policy check, archived under `Logs/EnemyAI-LocalQA` | PASS: 150 assertions against actual compiled policy, including both actors, harmful shield breaks, own/enemy area effects, strong-target buffs, discard/search/revival choices and read-only evaluation of every enum effect | This fallback was used during the initial Editor reload issue. Normal project verification now uses the native Unity checks below; no additional compiler tooling is needed by teammates. |
| Existing action/placement scorers | PASS: 10 + 671 checks | Does not establish new effect execution or balance. |
| Three preset assets | PASS: 30 cards each, registered card references, one class each, max three copies and correct VIP slots | Teammate commit/data audit confirms Miku/Len/Luka are the three implemented effect pools; no numbered deck assets exist. |
| `Tools > Enemy AI > Run Deck Effect Checks` | PASS: 3,406 assertions; 59 real definitions as both actors; 892 submitted actions, 257 effect choices, 104 ability activations; 18 seeded full matches completed in 6–18 rounds. | No scores/winners injected. Native Play Mode timing and human review are separate. Initial movement oscillation/root-target loops were fixed before the passing run. |
| Updated AI scene / real Play Mode | PASS: three native AI turns, one per preset, each placing a guaranteed legal performer, completing its choices and passing to player. Pause prevented AI action. Runtime used 8 test energy; no scores or winners injected. `Logs/enemy-deck-turn-smoke.txt`. | Smoke covers the real Update loop/animation gates, not full human UI input or balance. Original opening/redraw interaction remains for manual review. |

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

## October 7, 2026: narrator nameplate

Scene: `Assets/Scenes/Story.unity`, in the original `VirtualEncore` project. Opened the Miku prologue in Play Mode and advanced through narrator, Miku and narrator lines. Confirmed that narration hides both the speaker label and the complete nameplate, character speech restores both, and returning to narration hides them again. All four narration background sprite references resolve. Visual captures in `Logs/NarratorUI/narrator.png` and `character.png` show the body frame and text remain in place. No C# compile errors were found and Console reported no errors after the preview. Startup had an existing Unity MCP HTTP 403 transport error. Preview did not complete a story dialogue or write viewed-story progress.

## October 11, 2026: six reviewed routes

| Scene / check | Steps | Result | Follow-up |
|---|---|---|---|
| Story assets, Unity 6000.5.4f1 | Run `SixRouteStoryChecks.CheckAssets`; compare imported speaker/text against the reviewed source snapshot; validate all rewards, starters, enemy decks and visible cast. | PASS: six routes, 72 sequences, 439 exact display boxes. Master SHA-256 unchanged. Incomplete content stays unavailable. | Replace the three support-class prototype card sets when authored assets exist. |
| Story/Game transitions | Run `SixRouteStoryChecks.Run` with separate saves for each route and two opposite opponent orders. | PASS: 6,484 assertions, 72 battle launches, 518 displayed-box checks. Covers both stage identities, losses/draws, six save failures/retries, endings, replay, starter idempotence and save reload. Literal version-1 Miku progress preserved. | Results are injected for repeatability; this does not establish battle difficulty. |
| Miku controls | Run the updated `MikuStoryChecks.Run` against the 74-box route in an isolated save. | PASS: automatic opening, log pause, skip/replay, loss/draw, save retry, five clears, ending and reload. | Human input and pacing feedback remains useful. |
| Game View presentation | Inspect native screenshots for Miku narrator/character ending lines, longest Luka narration and Rin's support deck. | Text fits the tested viewport; nameplate hides/restores; six ending avatars resolve; battle title names Rin despite Len support cards. Screenshots under `Logs/story-review`. | Rin, KAITO and MEIKO still use class icons. No new venue background was authored. |
| Natural-score Rin versus Miku | Keep the original 50-point target. Use the existing action scorer to choose legal local actions and ordinary Pass, while the original enemy AI runs. Continue the same match after its first bounded observation. | PASS: round 11, Rin 62–26; 16 local actions and 33 local passes. No outcome or score injection. Saved win returned to Rin/Miku after dialogue. Pause blocked both actors; out-of-bounds placement spent no energy and removed no card. | One automated match is not a six-route balance result or manual usability test. |
| Final startup and cleanup | Start the isolated preview; verify six playable class choices; exit through normal Stop; inspect flags and Console. | PASS: preview routing cleared, editor left out of Play Mode in unmodified Story scene; final Console has no errors or warnings. | Tutorial still relies on the existing prototype bypass. Standalone player build untested. |

The MCP camera capture initially rendered only the camera background and logged an internal PlayerLoop recursion error in its package `ScreenshotUtility.cs`. Native `ScreenCapture` at the end of the frame produced the actual overlay UI images. This tooling issue did not recur in the final clean startup; no package change was made. Flow checks intentionally log save errors before successful retries.
