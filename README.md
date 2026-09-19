# Virtual Encore

Unity card-game senior project. This README is the technical entry point for
teammates: use it to open the right Unity version, find the playable scenes,
and run a small reproducible test.

## Quick start

1. Open this repository in **Unity 6000.5.4f1**. The required version is also
   recorded in `ProjectSettings/ProjectVersion.txt`.
2. Open one scene from `Assets/Scenes/`:
   - `Game Bri.unity` is the current team gameplay scene.
   - `Game Bri - Enemy AI Test.unity` is an isolated manual test scene for the
     Enemy AI. It uses a minimum of 3 energy so the supplied cost-3 cards can
     be played in round 1.
3. Enter Play Mode. On the player's turn, drag a card to a legal tile or press
   **Pass**. Wait for the enemy action, then continue.

If the screen appears unresponsive, read the phase text first. It explicitly
states whether it is your turn, the enemy's turn, scoring, or a paused state.

## Documentation map

- [Architecture](Docs/Architecture.md) — component responsibilities and data flow.
- [Game rules](Docs/GameRules.md) — current implemented rules, not a future design wish list.
- [Enemy AI](Docs/EnemyAI.md) — explainable scoring policy and scene setup.
- [Playtest log](Docs/Playtest.md) — reproducible tests and observed results.

Run the Enemy AI scorer checks from the repository root with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/QA/Run-EnemyActionScorerChecks.ps1
```

## Collaboration convention

The repository is the source of truth for versioned technical facts: scenes,
scripts, setup instructions, implemented rules, and test evidence. Notion is
the source of truth for task ownership, meeting notes, and design discussion.
Discord is for short updates and links, not the only location of a decision.

When changing a gameplay feature, update the relevant `Docs/` page and add a
short playtest entry with the scene, steps, result, and any remaining issue.
