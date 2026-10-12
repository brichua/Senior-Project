# Reviewed story integration

The October 11 reviewed master is imported into all six existing `StoryData` assets in the same Unity project. It replaces the previously imported Miku dialogue. There are 72 sequences and 439 dialogue boxes, including narration. Opponents may be played in any order; each route is an alternate playthrough with its own headliner.

| Route | Boxes | Ending boxes |
|---|---:|---:|
| Miku | 74 | 8 |
| Rin | 74 | 8 |
| Len | 73 | 8 |
| Luka | 73 | 7 |
| KAITO | 73 | 7 |
| MEIKO | 72 | 6 |

`Tools/Story/ReviewedRoutes.json` records the exact display text and the source master SHA-256. `Tools/Story/ImportReviewedRoutes.py <master.md>` regenerates the six assets by opponent identity, removes display-only Markdown emphasis, and preserves their IDs and metadata. Locations and editorial notes are not dialogue boxes. No new music, background, streaming integration or NPC portrait is required by the narrated actions.

## Player flow

- Select a singer and Begin. If the saved collection has no usable deck containing that singer, the route's configured starter is added without replacing existing decks or changing the global deck selection. The opening plays on first entry; its viewed state is saved when the player finishes or skips it.
- Select an opponent and a deck containing the selected singer. Start plays the before scene, then loads Game. Skip advances to the battle.
- A saved win unlocks the after scene. Back to audition plays it automatically. Losses and draws return without playing it or adding a clear.
- The fifth saved clear leads from the after scene to the ending. Opening, before, after and ending buttons still support replay.
- Existing save-failure handling keeps the result open until Retry Save succeeds. Repeated wins keep the original first-clear date and do not duplicate owned cards.

The save schema stays at version 1, with the existing optional `viewedStoryDialogues` list. All six permanent story keys remain unchanged, including `story-hatsunemiku`. Replacing dialogue preserves old cards, decks, clear timestamps and viewed flags. Previously viewed Miku opening/ending scenes are available through replay buttons rather than being forced again.

Class selection requires the full route's opening, ending, encounter descriptions, before/after scenes, rewards and all difficulty-deck references to be usable. Filling an opening alone cannot expose an unfinished route. The Story scene still enables `debugTutorialCompleted` for prototype access; the tutorial remains unfilled and disabled.

## Prototype configurations

All six routes reuse an opponent's existing deck and reward. Miku as a new opponent uses the existing starter deck and World Is Mine reward; the other five retain the configuration below. Every opponent has all three difficulty slots assigned. Player starters use the same legal card lists in separate named assets.

| Opponent | Location | Enemy deck | First-clear card |
|---|---|---|---|
| Len | The Rooftop Theatre | Existing Len AI | Odds&Ends |
| Rin | The Tram Depot | Rin/Len support deck | Teo |
| KAITO | The Harbor Stage | KAITO/Len support deck | Tell Your World |
| MEIKO | The Converted Cinema | MEIKO/Len support deck | Glow |
| Luka | The Planetarium | Existing registered Luka performers | From Y to Y |

Rin, KAITO and MEIKO have no authored card assets in this main snapshot. Their temporary decks contain registered Len cards and both character classes; story battles keep the named opponent as the primary stage class. Other game modes retain the existing ordering. Replace these decks when their own cards are ready.

All three difficulty slots currently reference the same opponent deck. This makes every route playable through the existing automatic difficulty progression; it does not provide three balanced difficulties. Rewards are existing registered cards and may already be owned. The current collection stores ownership, not quantities.

Both the player's selected protagonist and the named rival take priority in battle presentation when a deck contains a support class. Cards keep their existing class membership and rules. Normal game modes retain their original card-count ordering.

Miku, Len and Luka use existing character sprites. Rin, KAITO and MEIKO use their class icons as temporary portraits/art. Narrator log entries have no portrait. The venues are conveyed in text; no new backgrounds, animation, music or environmental battle rules were added.

Finishing the tutorial and normal access rules remain a separate task. Full content validation is still available from the Story Catalog context menu; that includes the intentionally unfinished tutorial. Runtime startup checks structure and UI without logging that unfinished content as a startup error.

## Development checks

Use Unity 6000.5.4f1 and the Story scene in this checkout.

- `Tools > Story > Check Six Reviewed Routes` compares all 439 boxes against the source snapshot, validates identity mapping, visible cast, player starters, rewards and difficulty decks, and checks that incomplete content remains unavailable.
- `Tools > Story > Run Six Route Flow Checks` uses independent saves under `Logs/` for all six routes in forward and reverse opponent orders. It checks all displayed dialogue and nameplate transitions, mixed-deck player/rival identity, losses/draws, deliberate save failure/retry, five-clear endings, replay, reload, starter idempotence and literal version-1 legacy Miku progress.
- `Tools > Story > Preview Reviewed Dialogue (Isolated Save)` opens the Story scene for a visual preview without the real player save. Use normal Stop or `Tools > Story > End Isolated Preview` to restore normal save routing.

Six-route results are written to `Logs/six-route-content-checks.txt` and `Logs/six-route-flow-checks.txt`. Outcomes in the automated flow runner are injected; natural battle playtesting and a standalone player build remain separate validation.

### Verified October 11, 2026

Unity 6000.5.4f1 compiled the updated project. Exact source/asset checks passed for all six routes. The full flow runner passed 6,484 assertions across 72 Game-scene launches, forward and reverse opponent orders, and 518 displayed-box checks, covering all 439 distinct boxes. All six deliberate save failures recovered on retry. Legacy version-1 Miku deck selection, five clears, timestamps and viewed flags were preserved. The updated Miku runner separately passed log pause, automatic playback, skip/replay, scene return, save retry and reload.

Native Game View captures were inspected for the Miku ending's six visible singers, narrator/character nameplate changes, Luka's longest narration, and the Rin support-deck battle's player identity. No overflow was found at the checked viewport. The final isolated startup exposed all six routes; normal Stop cleared preview save routing. Final Console inspection reported no errors or warnings.

A separate Rin-versus-Miku smoke match used ordinary legal actions for the local side and the original enemy AI, without injecting scores or a result. Rin won 62–26 in round 11 at the unchanged target of 50. The win saved and returned to the correct after scene. Pause blocked both actors without changing energy; an out-of-bounds placement was rejected without removing a card or spending energy. This is one automated match, not human balance approval. The first 120-second observation ended before the match; continuation completed the same match.

Evidence: `Logs/six-route-content-checks.txt`, `six-route-flow-checks.txt`, `miku-story-checks.txt`, `six-route-natural-match.txt`, `six-route-pause-check.txt`, `six-route-illegal-check.txt`, and `Logs/story-review/`. The MCP camera screenshot helper produced a background-only image and a PlayerLoop recursion error traced to its package screenshot utility. Native end-of-frame screenshots were used instead; the final clean startup did not reproduce the error. No MCP package files were changed.

Own card/art assets for Rin, KAITO and MEIKO, distinct difficulty balancing, the tutorial and a standalone player build remain production follow-ups.

- `Tools > Story > Check Miku Content` validates dialogue counts, sprite/card references, opponent deck classes and the enabled build scene.
- `Tools > Story > Run Miku Flow Checks` uses a fresh save under `Logs/`, exercises real scene transitions, checks dialogue/log/auto/skip/replay, loss/draw behavior, save failure/retry, five clears and reload persistence. Outcomes are injected for repeatability; this checks integration, not AI balance or the difficulty of winning a full match.

The flow check writes `Logs/miku-story-checks.txt`. Batch command:

```text
Unity.exe -batchmode -nographics -projectPath "<this checkout>" -executeMethod MikuStoryChecks.Run -logFile "<this checkout>/Logs/unity-miku-checks.log"
```

The development helper is in an Editor folder and is excluded from player builds. Keep its output separate from a manual visual review of dialogue layout and normal player interaction.

### Historical verification October 7, 2026

Unity 6000.5.4f1 compiled this checkout and the batch flow check returned PASS. All five opponents loaded Game with valid story inputs, saved wins and returned to their after scenes. The fifth clear opened the ending; replay and reloaded progress remained correct. Losses, draws and a deliberate temporary-file save failure also passed their checks.

This historical run tested the earlier 62-box Miku draft, not the current six-route master.

The log includes the deliberately injected save error and a Unity Editor Search indexing exception during startup. The latter paused the editor through Console Error Pause; the helper resumes the player loop during its run. No C# compilation errors or Story Setup errors were found. This run does not validate a standalone player build, visual layout, natural battle outcomes or difficulty balance.

The initial story implementation was authored on `weien/miku-story`, based on fetched `origin/main` at `e8cf816`, then consolidated into the existing AI branch `weien/preview-rules-qa` at the user's request. Use only `D:/2026 Fall/开发/VirtualEncore` as the Unity project. It contains both the story route and the existing flag-aware enemy AI. `BasicEnemyAITest` keeps its serialized scene identity as a subclass of `EnemyAIController`; the controller also waits while phase animations are playing. The temporary story worktree has been removed. Its original verification logs are preserved under `Logs/initial-story-check/`.

### Verification after consolidation

On October 7, Unity 6000.5.4f1 compiled the combined original project and the full Miku flow check again returned PASS (`Logs/miku-story-checks.txt`; `Logs/one-project-unity-checks.log`). It exercises the same twelve sequences and five encounters with the flag-aware enemy AI now present. Standalone checks also passed: 766 PreviewRules, 10 EnemyActionScorer and 671 EnemyPlacementScorer checks. Original local Unity MCP package edits and the untracked preview QA files were checked against their pre-merge hashes and preserved exactly. The editor was restarted after refresh stopped responding.

### Narrator presentation

Narrator (case-insensitive) or blank speakers hide the speaker label and use the background without the nameplate; character dialogue restores the original background and name. The original nameplate is baked into the dialogue atlas, so the narration background assembles four clean sprite regions from the same unchanged PNG. Dialogue text, portraits and the log keep their existing behavior. The Story scene assigns both background variants.
