# Enemy AI

Updated October 25, 2026 for the six standard character pools.

## Goal

Provide a simple opponent that is believable, deterministic enough to debug,
and easy to explain in a senior-project presentation. It is intentionally a
small utility AI, not machine learning or a search tree.

## Decision loop

On an enemy turn, `EnemyAIController` waits `actionDelay` seconds (default
0.75), asks `GameplayBoardBridge` which actions are legal, and evaluates them.
It submits the highest positive-scoring action. If no positive legal action
exists, it passes.

The controller resolves its own pending choice before checking ordinary turns
or phases. This includes departure choices during the player's turn and during
EndRound scoring with ordinary input disabled. It considers activated abilities.
Mandatory target/discard choices finish even if
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

`EnemyCardPolicy.Extended.cs` adds the Rin sacrifice/departure/token effects,
MEIKO creation/keyword effects, and KAITO relocation/column effects. Choices use
their effect and stage: sacrificing an ally has a cost, revealing a card values
new keywords, and Formation's second stage is distinct from direct spell targets.
Token and gained/shared keyword data are read through `PerformerKeywords.Of`.
Tokens retain performer behavior even when created from a stage-effect asset.

The implementation stays in the existing utility-AI classes, with ordinary
switches and focused helper methods. It estimates local value rather than
implementing a second rules engine or searching future turns.

Assign **any existing DeckData** to `GameplayBoardBridge.enemyDeck` to let the
enemy use its cards; this is not a policy restricted to three deck names.
`BasicEnemyAITest` in the team scene inherits the same controller. The bridge
finds an existing controller if its serialized `enemyAI` reference is empty.

The six **coverage presets** are for deterministic checks and manual previews:

| Preset | Cards | Purpose |
|---|---|---|
| `Assets/Decks/miku effects ai.asset` | 30 | 23 standard Miku definitions |
| `Assets/Decks/len effects ai.asset` | 30 | 16 standard Len definitions and the legacy Välkommen VIP |
| `Assets/Decks/luka effects ai.asset` | 30 | 19 standard Luka definitions |
| `Assets/Decks/rin effects ai.asset` | 30 | 16 standard Rin definitions |
| `Assets/Decks/meiko effects ai.asset` | 30 | 17 standard MEIKO definitions |
| `Assets/Decks/kaito effects ai.asset` | 30 | 15 standard KAITO definitions |

Each pool includes its covered definitions and fills to 30 with at most three
copies. These are reproducible test lists rather than balanced competitive decks.
The team also has 18 difficulty decks in `Assets/Decks/enemy ai`, three per
character. Their original compositions and Story references are preserved.

The twelve newer VIP assets and sixty duet assets have missing or mismatched
custom mechanics compared with their current descriptions. The coverage presets
exclude those new cards. Full-match checks also exercise the team's difficulty
decks with their current VIP configurations; a completed match does not verify
the unfinished VIP's printed behavior. The AI executes available rules and
cannot supply missing card mechanics through a placement flag.

## Reproduce the checks

Use the normal Unity compiler and the checks below. The existing action and
placement scorer runners in `Tools/QA` provide another 10 and 671 checks.

In Unity, run **Tools > Enemy AI > Run Deck Effect Checks**. It creates temporary
objects without using player saves, forces all 107 standard/legacy card
definitions through both actor positions, exercises chained choices/abilities,
and runs 108 seeded full matches: 72 across all six coverage pools and both
local sides, plus 36 using all eighteen real difficulty presets on both sides.
Semantic regressions cover sacrifice/removal, protection, token identity,
keyword inheritance, movement, Formation stages and a real departure trigger.
Results go to `Logs/enemy-deck-checks.txt`. A missing result or a
FAIL is not a passing integration run.

For manual play, open `Game Bri - Enemy AI Test.unity` (Miku versus Len by
default), or use **Tools > Enemy AI > Prepare [character] Test** for any of
the six characters and press Play. The preparation menu refuses unsaved scenes
or an already-running game; its test settings are not saved automatically.
The test scene now uses the current team UI and opening sequence. It bypasses
the saved player deck and uses 1 initial round energy, matching normal gameplay.

**Tools > Enemy AI > Run Six Deck Turn Smoke** separately checks the real
MonoBehaviour decision loop and animation gates in Play Mode. It supplies a
guaranteed legal performer and 8 test energy, pauses/resumes, and checks that
each preset's enemy acts, resolves choices and passes to the local player. It
also creates AI-owned pending choices during the player's turn and during
EndRound to verify Update routing. It uses no player saves and restores the
temporary background-running setting. Results: `Logs/enemy-deck-turn-smoke.txt`.

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
Normal moves and effect-driven relocations both update that memory. The column
ability is valued only when an improving unvisited destination remains; its
choices prefer those destinations. This prevents repeated column moves from
keeping a match alive indefinitely. Memory resets on a new round or match and
does not change human movement rules or target legality.

## Current limits

- The AI makes one local best-choice evaluation; it does not simulate future
  turns.
- It uses only snapshot-visible information and has no hidden-hand knowledge.
- Scoring weights are deliberately simple and should be tuned with observed
  playtests, not guessed once and treated as final balance.
- Effect values are estimates, not a complete forward simulation of chained
  auras, retaliation or future turns. The legal target lists remain authoritative.
- October 25: 93 runtime and five Editor sources compile. Existing scorer checks
  pass: 10 action and 671 placement assertions. The native suite passes 23,503
  assertions, 4,964 actions, 1,353 effect choices, 249 ability activations and
  108 complete matches. No scores or winners are injected into those matches.
- Six real Play Mode turns pass, including pause, effect completion and handoff;
  off-turn and EndRound pending-choice timing also pass. Human balance review,
  unfinished VIP/duet mechanics and a standalone player build remain separate.
