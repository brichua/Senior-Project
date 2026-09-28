# Enemy AI placement flags

## Scope

This initial batch contains ten implemented AI hints assigned to nineteen of the twenty-six current performer cards. It uses rough interpretations that can be adjusted as card rules develop. These hints guide choices; they do not execute card effects, change influence, override legality, or inspect the opposing hand. The scorer operates relative to the acting player.

| Flag | Current card | Preference | Initial weight |
|---|---|---|---|
| `near_allies` | Vampire, Monitoring | At least one adjacent friendly performer. More neighbors do not multiply this particular hint. | 24 |
| `near_contested_allies` | Bug | Adjacent friendly performers with an opponent on their tile. Enemy-only tiles and uncontested allies do not qualify. | 12 per eligible tile, capped at 24 |
| `contest_entry` | FPS | Play onto a tile containing an opponent. Does not apply to movement. | 8 |
| `ally_cluster` | World is Mine, Rolling Girl | Prefer two or more neighboring allies; one ally gives partial value. | 12 per ally, capped at 24 |
| `near_enemies` | Odds&Ends | Find an adjacent enemy to weaken. | 12 if present |
| `front_pressure` | Butterfly on Your Right Shoulder | Find enemies ahead in the same row. | 12 per enemy, capped at 24 |
| `solo_center` | Chilledren | Prefer the middle column when no other performer occupies that zone. | 24 |
| `support` | Monitoring, Senbonzakura, God-ish, Vampire Patho's, Law-evading Rock, Fantasy Pianist, Välkommen, lll Toluthin Antenna lll | Prefer an uncontested source tile to preserve ongoing or delayed value. A useful winning center contest can still outrank this hint. | 6 |
| `spawn_space` | Ghost Rule, Tell Your World | Keep adjacent tiles free of both players' performers. | 4 per free tile, capped at 8 |
| `middle_pressure` | Holy Lance Explosion Boy | Prefer playing when enemies occupy the middle column; source position is unrestricted. | 6 per middle enemy, capped at 12 |

### Working interpretations for this batch

- Adjacency is eight surrounding squares.
- Incomplete adjacent positive effects are treated as ally-oriented for the AI. Odds&Ends is treated as enemy-oriented.
- Front means horizontally toward the opponent, over the remaining same-row squares: actor 0 faces right; actor 1 faces left.
- Solo middle counts both players' performers across the entire third column. A moving card is excluded from its old friendly occupancy.
- Spawn-space checks are a rough safe-space hint. Tell Your World's exact spawn geometry is not settled by this tag.
- `support` is a survival preference, not a backstage zone or an instruction to avoid center.
- These interpretations are prototype choices, not finalized team effect rules. Trigger timing, exact amounts, harmonize, encore, and stage-share resolution remain separate from this AI batch.

Weights are utility points, not influence values. They are starting values for playtesting. The connection weight can outweigh the baseline reward for moving toward center, while a useful center contest can still win. A currently losing destination receives no positive placement hint: an unimplemented effect must not rescue it in the AI's assessment.

For moves, subtract the source hint value from the destination hint value. Exclude the moving card's original square when checking destination neighbors. Missing and unknown flags leave the baseline unchanged; repeated flags do not stack. Diagnostic reasons explicitly call these contributions `placement hint`.

`BasicEnemyAITest` retains its existing script GUID and scene references but inherits the shared `EnemyAIController` policy. Existing game, class, and action-delay fields are preserved. Puzzle and draw-animation guards apply to both entry points. `GameplayBoardBridge.enemyAI` accepts the base controller.

## Current performer mapping

Empty flags do not imply a card has no effect. They mean the current effect does not need one of these coarse hints. This mapping uses current asset text and the working interpretations above. The tentative tags form a practical first batch rather than finalized effect rules.

| Class | Card | Current flags / decision | Reason |
|---|---|---|---|
| Miku | Deep Sea Girl | Empty | No special effect text. Baseline scoring. |
| Miku | Monitoring | `near_allies`, `support` | Treat the ongoing adjacent enhancement as ally support. |
| Miku | Romeo and Cinderella | Empty | Stage-share behavior needs a separate rule; do not infer a placement hint. |
| Miku | World is Mine | `ally_cluster` | Treat two neighboring performers as an ally-cluster preference. |
| Miku | Rolling Girl | `ally_cluster` | Favor multiple nearby allies for the adjacent enhancement and harmonize role. |
| Miku | Vampire | `near_allies` | Explicit ally adjacency. Encore is not implemented by this hint. |
| Miku | Odds&Ends | `near_enemies` | Treat the adjacent reduction as an enemy-targeting role. |
| Miku | Senbonzakura | `support` | Preserve its ongoing field presence; global range needs no adjacency hint. |
| Miku | Snow White Princess | Empty | Selected ally buff gives no source-position restriction. |
| Miku | Tell Your World | `spawn_space` | Roughly favor room around a summoning performer. |
| Miku | Bug | `near_contested_allies` | Explicit surrounding allies on contested tiles. |
| Miku | Ghost Rule | `spawn_space` | Preserve room for its adjacent minion. |
| Miku | Darling Dance | Empty | No special effect text. Baseline scoring. |
| Miku | God-ish | `support` | Preserve ongoing minion enhancement. |
| Len | Butterfly on Your Right Shoulder | `front_pressure` | Aim its row toward opposing performers. |
| Len | Holy Lance Explosion Boy | `middle_pressure` | Value enemy middle presence without requiring the source to stand there. |
| Len | Vampire Patho's | `support` | Preserve recurring performance-phase value. |
| Len | Law-evading Rock | `support` | Preserve delayed energy value. |
| Len | Fire Flower | Empty | Energy threshold is not a board-position condition. |
| Len | lll Toluthin Antenna lll | `support` | Preserve the repeated control ability, without inventing a target range. |
| Len | Gigantic O.T.N. | Empty | Next-spell discount is not a board-position condition. |
| Len | Chilledren | `solo_center` | Use the provisional both-sides, whole-middle-zone solo condition. |
| Len | FPS | `contest_entry` | Explicit on-play same-tile opposing target. |
| Len | Fantasy Pianist | `support` | Preserve delayed draw value. |
| Len | Välkommen | `support` | Preserve recurring draw value while still considering its high influence in contests. |
| Len | Buraikku Jikorizer | Empty | Inherited keywords require runtime effect implementation; static flags cannot predict them. |

## Verification

Run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/QA/Run-EnemyPlacementScorerChecks.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/QA/Run-EnemyActionScorerChecks.ps1
```

The first runner compiles production scoring code without Unity. It covers useful support versus center, diagonal and enemy ownership distinctions, contested targets, movement loss and self-neighbor exclusion, entry timing, current losing contests, duplicate/unknown flags, cluster thresholds, mirrored front direction, solo-zone occupancy, support safety, spawn availability, global middle pressure, and mirrored boards.

`Unity-EnemyPlacementIntegrationChecks.csx` is a method-body snippet for Unity MCP `execute_code`, not an Assets script. Run it in Edit mode. It creates and cleans up one temporary hidden object, loads the real card assets, exercises legal choices through the production controller for both actors, and verifies an inherited BasicEnemyAITest update executes a legal action. It does not save scenes or assets.

Verified September 27, 2026, after this batch: 46 focused placement assertions, 10 baseline scorer assertions, and the existing 625 mirror combinations passed. Unity passed 20 controller decision scenarios plus one inherited Update execution. All 19 tagged assets imported with supported flag names; compilation and the error query were clean. Game and Game Bri scene AI references, class lists, and action delays were checked during the initial integration; no scene files were edited for this batch.

The baseline scorer also values the current influence added by an unopposed play. Normal performers contribute their influence; the implemented generic stage effect contributes only its added influence, not the target performer's existing strength. This avoids preferring low-influence cards solely for their lower cost. Currently losing contests do not receive this unopposed bonus.

Not yet verified: a complete interactive match, difficulty balance, and the unimplemented special effects. The Notion database has not been edited.
