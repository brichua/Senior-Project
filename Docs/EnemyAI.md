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

The controller now also resolves the rules engine's pending effect choices and
considers activated abilities. Mandatory target/discard choices finish even if
all candidate values are negative. Optional movement can stop. Choices are
validated by `GameplayBoardBridge.CanSubmitFor`; the AI does not invent targets.
The bridge keeps its original first-option fallback only when no active AI owns
the board (and for the scripted puzzle path). Story launches reuse an existing
AI component instead of adding a second controller.

## Card effects and decks

`EnemyCardPolicy` estimates the value of actual `CardEffect` and keyword data,
including buffs, enemy removal/weakening, auras, sharing, tokens, draw/search,
discard, revival, movement effects, Harmonize, Soloist and shielding. Stage
effects are no longer all scored as a generic friendly +1 influence buff.
Activated abilities are considered on both performers and their assistants;
movement accounts for an accompanying assistant. Costs/discounts, phase limits,
protected targets and other legal restrictions remain the rules engine's job.

Assign **any existing DeckData** to `GameplayBoardBridge.enemyDeck` to let the
enemy use its cards; this is not a policy restricted to three deck names.
`BasicEnemyAITest` in the team scene inherits the same controller. The bridge
finds an existing controller if its serialized `enemyAI` reference is empty.

The following new **coverage presets** use the current Miku/Len/Luka card pools:

| Preset | Cards | Purpose |
|---|---|---|
| `Assets/Decks/miku effects ai.asset` | 30: 19 performers / 11 stage effects | Every current Miku definition, including its VIP |
| `Assets/Decks/len effects ai.asset` | 30: 21 performers / 9 stage effects | Every current Len definition, including its VIP |
| `Assets/Decks/luka effects ai.asset` | 30: 18 performers / 12 stage effects | Every current Luka definition; this pool has no VIP |

Each pool includes every current definition once, then fills remaining slots
with the cheapest cards in stable cost/name order, at most three copies.
These are reproducible test lists, not team-approved competitive decks. The
repository has no numbered deck assets. Teammate commits and effect data identify
Miku/Len/Luka as the three implemented card pools (23/17/19 definitions); the
other three characters still have None in all effect slots. Original story decks and
difficulty references are unchanged.
The existing Luka `Just Be Friends` asset was missing from the shared card
catalog; it is now registered so the Luka preset has no unresolvable card IDs.
This does not grant ownership or rewrite the player's saved collection.

## Reproduce the checks

Use the normal Unity compiler and the checks below. The existing action and
placement scorer runners in `Tools/QA` provide another 10 and 671 checks.

In Unity, run **Tools > Enemy AI > Run Deck Effect Checks**. It creates temporary
objects without using player saves, forces all 59 current Miku/Len/Luka card
definitions through both actor positions, exercises chained choices/abilities,
and attempts 18 seeded full matches across the three presets and both local
player sides. Results go to `Logs/enemy-deck-checks.txt`. A missing result or a
FAIL is not a passing integration run.

For manual play, open `Game Bri - Enemy AI Test.unity` (Miku versus Len by
default), or use **Tools > Enemy AI > Prepare Miku Test / Prepare Len Test /
Prepare Luka Test** and press Play. The preparation menu refuses unsaved scenes
or an already-running game; its test settings are not saved automatically.
The test scene now uses the current team UI and opening sequence. It bypasses
the saved player deck and uses 1 initial round energy, matching normal gameplay.

**Tools > Enemy AI > Run Three Deck Turn Smoke** separately checks the real
MonoBehaviour decision loop and animation gates in Play Mode. It supplies a
guaranteed legal performer and 8 test energy, pauses/resumes, and checks that
each preset's enemy acts, resolves choices and passes to the local player. It
uses no player saves and restores the temporary background-running setting.
Results: `Logs/enemy-deck-turn-smoke.txt`. October 18: all three passed.

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

- `GameplayBoardBridge.enemyDeck` is the Len coverage preset by default.
- The local player uses the Miku coverage preset, without reading/writing personal decks.
- `EnemyAIController.game` references the same `GameplayBoardBridge`.
- `BasicEnemyAITest` supplies the same AI; no automated local-player harness is enabled.

`Game Bri.unity` remains the team gameplay scene. Do not add a test harness to
it without team agreement.

The controller remembers visited movement tiles per card for the current round.
It will not revisit one through normal AI movement, preventing cross-turn
oscillation. This resets on a new round or match and does not restrict human moves.

## Current limits

- The AI makes one local best-choice evaluation; it does not simulate future
  turns.
- It uses only snapshot-visible information and has no hidden-hand knowledge.
- Scoring weights are deliberately simple and should be tuned with observed
  playtests, not guessed once and treated as final balance.
- Effect values are estimates, not a complete forward simulation of chained
  auras, retaliation or future turns. The legal target lists remain authoritative.
- October 18: both assemblies compiled successfully; 150 policy, 10 action and
  671 placement checks passed; all three preset asset references/counts checked.
  Native deck-effect/match checks also PASS: 3,406 assertions, 59 definitions
  exercised as both actors, 257 effect choices, 104 ability activations and
  18 completed seeded matches. Initial Editor reload/import delays were resolved
  after user approval. First-option/root and movement repetition issues found
  by full matches were fixed before this passing run.
- Real Play Mode turns also passed for all three presets, including pause,
  effect completion and handoff to the player. Human balance/play-feel review
  remains separate from automated verification.
