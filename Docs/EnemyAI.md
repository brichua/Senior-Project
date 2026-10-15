# Enemy AI

## Goal

Provide a simple opponent that is believable, deterministic enough to debug,
and easy to explain in a senior-project presentation. It is intentionally a
small utility AI, not machine learning or a search tree.

## Decision loop

On an enemy turn, `EnemyAIController` waits `actionDelay` seconds (default
0.75), asks `GameplayBoardBridge` which actions are legal, and evaluates them.
It submits the highest positive-scoring action. If no positive legal action
exists, it passes.

## Scoring policy

`EnemyActionScorer` gives a score and human-readable reasons:

| Signal | Current purpose |
|---|---|
| Center-column position | Prefer the lane that scores at round end. |
| Contesting the opponent | Prefer challenging opponent influence. |
| Direction / progress | Prefer advancing a performer toward the scoring lane. |
| Energy efficiency | Break close ties in favor of a lower-cost card. |

The Console prints the selected action, total score, and reasons. Example:

```text
[Enemy AI] Player 1 chose play Deep Sea Girl to (2, 0) — score 25.
Reason: center position +20; energy efficiency +5
```

## Scene setup

Use `Assets/Scenes/Game Bri - Enemy AI Test.unity` for testing. On `GameManager`:

- `GameplayBoardBridge.minimumRoundEnergy` is 3.
- `EnemyAIController.game` references the same `GameplayBoardBridge`.
- `EnemyAIPlaytestHarness` is present but disabled; enable it only for an
  automated smoke test, not a manual-player test.

`Game Bri.unity` remains the team gameplay scene. Do not add a test harness to
it without team agreement.

## Current limits

- The AI makes one local best-choice evaluation; it does not simulate future
  turns.
- It uses only snapshot-visible information and has no hidden-hand knowledge.
- Scoring weights are deliberately simple and should be tuned with observed
  playtests, not guessed once and treated as final balance.
