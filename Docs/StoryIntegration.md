# Miku story integration

The current Miku draft uses the existing `StoryData` asset and story scene. It contains an opening, five auditions with before/after dialogue, and an ending: twelve sequences and 62 dialogue boxes. Opponents may be played in any order.

## Player flow

- Select Miku and Begin. The opening plays on first entry; its viewed state is saved when the player finishes or skips it.
- Select an opponent and a Miku deck. Start plays the before scene, then loads Game. Skip advances to the battle.
- A saved win unlocks the after scene. Back to audition plays it automatically. Losses and draws return without playing it or adding a clear.
- The fifth saved clear leads from the after scene to the ending. Opening, before, after and ending buttons still support replay.
- Existing save-failure handling keeps the result open until Retry Save succeeds. Repeated wins keep the original first-clear date and do not duplicate owned cards.

The save schema stays at version 1, with an optional `viewedStoryDialogues` list. Older saves initialize the missing list without losing decks, cards or prior clears. The story save key remains `story-hatsunemiku`.

## Prototype configurations

| Opponent | Location | Enemy deck | First-clear card |
|---|---|---|---|
| Len | The Rooftop Theatre | Existing Len AI | Odds&Ends |
| Rin | The Tram Depot | Rin/Len support deck | Teo |
| KAITO | The Harbor Stage | KAITO/Len support deck | Tell Your World |
| MEIKO | The Converted Cinema | MEIKO/Len support deck | Glow |
| Luka | The Planetarium | Existing registered Luka performers | From Y to Y |

Rin, KAITO and MEIKO have no authored card assets in this main snapshot. Their temporary decks contain registered Len cards and both character classes; story battles keep the named opponent as the primary stage class. Other game modes retain the existing ordering. Replace these decks when their own cards are ready.

All three difficulty slots currently reference the same opponent deck. This makes the route playable through the existing automatic difficulty progression; it does not provide three balanced difficulties. Rewards are existing registered cards and may already be owned. The current collection stores ownership, not quantities.

Miku, Len and Luka use existing character sprites. Rin, KAITO and MEIKO use their class icons as temporary portraits/art. Narrator log entries have no portrait. The venues are conveyed in text; no new backgrounds, animation, music or environmental battle rules were added.

The checked Story scene already enables `debugTutorialCompleted`, which remains enabled for this prototype. The tutorial and other character stories still have no authored dialogue and are disabled in class selection until they have opening content. Finishing the tutorial and normal access rules remain a separate task. Full content validation is still available from the Story Catalog context menu; runtime startup checks structure and UI without logging every unfinished route as an error.

## Development checks

Use Unity 6000.5.4f1 and the Story scene in this checkout.

- `Tools > Story > Check Miku Content` validates dialogue counts, sprite/card references, opponent deck classes and the enabled build scene.
- `Tools > Story > Run Miku Flow Checks` uses a fresh save under `Logs/`, exercises real scene transitions, checks dialogue/log/auto/skip/replay, loss/draw behavior, save failure/retry, five clears and reload persistence. Outcomes are injected for repeatability; this checks integration, not AI balance or the difficulty of winning a full match.

The flow check writes `Logs/miku-story-checks.txt`. Batch command:

```text
Unity.exe -batchmode -nographics -projectPath "<this checkout>" -executeMethod MikuStoryChecks.Run -logFile "<this checkout>/Logs/unity-miku-checks.log"
```

The development helper is in an Editor folder and is excluded from player builds. Keep its output separate from a manual visual review of dialogue layout and normal player interaction.

### Verified October 7, 2026

Unity 6000.5.4f1 compiled this checkout and the batch flow check returned PASS. All five opponents loaded Game with valid story inputs, saved wins and returned to their after scenes. The fifth clear opened the ending; replay and reloaded progress remained correct. Losses, draws and a deliberate temporary-file save failure also passed their checks.

The log includes the deliberately injected save error and a Unity Editor Search indexing exception during startup. The latter paused the editor through Console Error Pause; the helper resumes the player loop during its run. No C# compilation errors or Story Setup errors were found. This run does not validate a standalone player build, visual layout, natural battle outcomes or difficulty balance.

The initial story implementation was authored on `weien/miku-story`, based on fetched `origin/main` at `e8cf816`, then consolidated into the existing AI branch `weien/preview-rules-qa` at the user's request. Use only `D:/2026 Fall/开发/VirtualEncore` as the Unity project. It contains both the story route and the existing flag-aware enemy AI. `BasicEnemyAITest` keeps its serialized scene identity as a subclass of `EnemyAIController`; the controller also waits while phase animations are playing. The temporary story worktree has been removed. Its original verification logs are preserved under `Logs/initial-story-check/`.

### Verification after consolidation

On October 7, Unity 6000.5.4f1 compiled the combined original project and the full Miku flow check again returned PASS (`Logs/miku-story-checks.txt`; `Logs/one-project-unity-checks.log`). It exercises the same twelve sequences and five encounters with the flag-aware enemy AI now present. Standalone checks also passed: 766 PreviewRules, 10 EnemyActionScorer and 671 EnemyPlacementScorer checks. Original local Unity MCP package edits and the untracked preview QA files were checked against their pre-merge hashes and preserved exactly. The editor was restarted after refresh stopped responding.
