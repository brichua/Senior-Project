#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VocaloidTCG;
using VocaloidTCG.BoardUI;

// Real engine tests in temporary objects, no player saves or scene changes.
public static class EnemyDeckChecks
{
    private static int assertions, actions, choices, abilities, matches;
    private static readonly List<string> evidence = new List<string>();
    private static readonly List<UnityEngine.Object> temporary = new List<UnityEngine.Object>();
    private static readonly string[] Pools = { "Miku", "Len", "Luka" };
    private static void Check(bool ok, string message)
    {
        assertions++;
        if (!ok) throw new InvalidOperationException(message);
    }

    [MenuItem("Tools/Enemy AI/Run Deck Effect Checks")]
    public static void Run()
    {
        assertions = actions = choices = abilities = matches = 0; evidence.Clear();
        var randomState = UnityEngine.Random.state;
        string result;
        try
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DeckCatalog>("Assets/Decks/deck catalog.asset");
            Check(catalog, "Catalog is available.");
            Check(catalog.ValidateCatalog(out var error), error);
            foreach (string pool in Pools)
            {
                var cards = AssetDatabase.FindAssets("t:CardData", new[] { "Assets/Card Obj/" + pool })
                    .Select(g => AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(g))).OrderBy(c => c.id).ToArray();
                Check(cards.Length > 0, pool + " card pool is present.");
                foreach (var card in cards)
                    for (int actor = 0; actor <= 1; actor++) CardScenario(catalog, card, actor);
                evidence.Add(pool + ": " + cards.Length + " actual card definitions exercised as both actors.");
                var deck = AssetDatabase.LoadAssetAtPath<DeckData>("Assets/Decks/" + pool.ToLowerInvariant() + " effects ai.asset");
                Check(deck, pool + " AI deck exists.");
                Check(DeckRules.Validate(deck.CreateRecord(), catalog, catalog.cards.Select(c => c.id).ToList(), out error), error);
            }
            ChoiceRegressions(catalog);
            foreach (string enemy in Pools) foreach (string player in Pools) for (int side = 0; side <= 1; side++)
                Match(catalog, player, enemy, side);
            result = "PASS\n";
        }
        catch (Exception ex) { result = "FAIL\n" + ex + "\n"; Debug.LogException(ex); }
        finally
        {
            UnityEngine.Random.state = randomState;
            foreach (var obj in temporary) if (obj) UnityEngine.Object.DestroyImmediate(obj);
            temporary.Clear();
        }
        Directory.CreateDirectory("Logs");
        result += string.Join("\n", evidence) + "\nassertions=" + assertions + "; submitted actions=" + actions +
            "; effect choices=" + choices + "; abilities=" + abilities + "; completed matches=" + matches + "\n";
        File.WriteAllText("Logs/enemy-deck-checks.txt", result);
        Debug.Log("[Enemy deck checks] " + result);
        if (Application.isBatchMode) EditorApplication.Exit(result.StartsWith("PASS") ? 0 : 1);
    }

    private static GameplayBoardBridge NewGame(DeckCatalog catalog, int local = 0)
    {
        var obj = new GameObject("Enemy AI isolated check") { hideFlags = HideFlags.HideAndDontSave };
        temporary.Add(obj);
        var game = obj.AddComponent<GameplayBoardBridge>();
        game.startAutomatically = false; game.useSelectedDeck = false; game.deckCatalog = catalog;
        game.enableOpeningSequence = false; game.enableDebugLogs = false; game.localPlayerId = local;
        game.minimumRoundEnergy = 8; game.endRoundDisplaySeconds = 0;
        var ai = obj.AddComponent<EnemyAIController>(); ai.game = game; ai.enabled = false; ai.enableDecisionLogs = false;
        game.enemyAI = ai;
        // A long nonempty deck prevents tests from ending at setup through exhaustion.
        var basic = catalog.cards.First(c => c.performer && c.effect == CardEffect.None && c.cost > 0);
        game.player0Cards = game.player1Cards = Enumerable.Repeat(basic, 30).ToArray();
        game.StartMatch(0);
        return game;
    }

    private static CardState Card(CardData data, int actor, string id = null) => new CardState {
        data = data, definitionId = data.id, ownerId = actor, instanceId = id ?? Guid.NewGuid().ToString("N"),
        currentCost = data.cost, currentInfluence = data.protectedInfluence > 0 ? data.protectedInfluence : data.influence,
        shieldActive = data.protectedInfluence > 0
    };

    private static CardData Basic(DeckCatalog catalog) => catalog.cards.First(c => c.performer && c.effect == CardEffect.None && c.influence >= 2);
    private static void Put(BoardSnapshot s, CardState c, int tile)
    {
        if (c.ownerId == 0) s.tiles[tile].side0 = c; else s.tiles[tile].side1 = c;
        s.tiles[tile].total0 = s.tiles[tile].side0?.currentInfluence ?? 0;
        s.tiles[tile].total1 = s.tiles[tile].side1?.currentInfluence ?? 0;
    }

    private static void CardScenario(DeckCatalog catalog, CardData data, int actor)
    {
        var game = NewGame(catalog); var s = game.Snapshot; var basic = Basic(catalog);
        s.activePlayerId = actor; s.Side(actor).energy = 8; s.Side(actor).score = 10;
        s.side0.hand.Clear(); s.side1.hand.Clear();
        foreach (int tile in new[] { 6, 7, 11 }) Put(s, Card(basic, actor), tile);
        // ReturnToCenter requires an ally beyond the center in enemy territory.
        Put(s, Card(basic, actor), actor == 0 ? 13 : 11);
        foreach (int tile in new[] { 7, 12, 17 }) Put(s, Card(basic, 1 - actor), tile);
        var smallEnemy = actor == 0 ? s.tiles[12].side1 : s.tiles[12].side0;
        smallEnemy.currentInfluence = 2;
        var cheap = catalog.cards.First(c => c.performer && c.cost <= 2 && c.effect != CardEffect.DiscardToPlay);
        s.Side(actor).hand.Add(Card(cheap, actor));
        s.Side(actor).hand.Add(Card(basic, actor));
        s.Side(actor).discard.Add(Card(cheap, actor));
        var card = Card(data, actor); s.Side(actor).hand.Add(card);
        var ai = game.enemyAI;
        bool submitted = false;
        for (int i = 0; i < 25; i++)
        {
            var action = new BoardAction(BoardActionKind.PlayCard, card.instanceId, -1, -1, i % 5, i / 5);
            // Evaluate every legal target, then force the actual card through engine
            // execution to exercise effects that a normal shuffled match may not draw.
            if (!game.CanSubmitFor(actor, action, out _)) continue;
            if (data.stageEffect) EnemyCardPolicy.SpellBenefit(s, actor, card, i);
            else { EnemyCardPolicy.EntryInfluence(s, actor, card, i); EnemyCardPolicy.PerformerBenefit(s, actor, card, i); }
            if (!submitted)
            {
                Check(game.TrySubmitFor(actor, action), data.cardName + " legal play submits."); actions++; submitted = true;
                Resolve(game, actor);
                if (game.CanActivateEffect(card))
                {
                    var ability = new BoardAction(BoardActionKind.ActivateEffect, card.instanceId, -1, -1, -1, -1);
                    Check(game.TrySubmitFor(actor, ability), data.cardName + " ability submits."); abilities++; Resolve(game, actor);
                }
                break;
            }
        }
        Check(submitted, data.cardName + " has an executable legal scenario.");
        UnityEngine.Object.DestroyImmediate(game.gameObject);
    }

    private static void Resolve(GameplayBoardBridge game, int actor)
    {
        for (int i = 0; game.Snapshot.pendingEffect != null && i < 80; i++)
        {
            Check(game.enemyAI.TryChooseAction(actor, out var action, out _), "AI finds mandatory/optional effect action.");
            Check(game.CanSubmitFor(actor, action, out var error), error);
            Check(game.TrySubmitFor(actor, action), "Selected effect action executes."); choices++;
        }
        Check(game.Snapshot.pendingEffect == null, "Effect chain terminates within 80 choices.");
    }

    private static void ChoiceRegressions(DeckCatalog catalog)
    {
        var game = NewGame(catalog); var s = game.Snapshot; var ai = game.enemyAI;
        var buff = catalog.cards.First(c => c.stageEffect && c.effect == CardEffect.DoubleInfluence);
        var source = Card(buff, 1); var first = Card(Basic(catalog), 1); var stronger = Card(Basic(catalog), 1);
        first.currentInfluence = 1; stronger.currentInfluence = 5;
        Put(s, first, 2); s.tiles[2].assist1 = stronger;
        s.pendingEffect = new PendingCardEffect { actor = 1, source = source, sourceTile = 2, stage = 2,
            effect = buff.effect, choice = EffectChoice.Ally, cards = new List<CardState> { first, stronger } };
        Check(ai.TryChooseAction(1, out var action, out _) && action.cardInstanceId == stronger.instanceId, "DoubleInfluence prefers stronger assistant over first target.");
        Check(game.TrySubmitFor(1, action) && stronger.currentInfluence >= 10, "Selected assistant receives real double-influence effect.");
        // Mandatory discard must resolve even when all options have negative utility.
        var weak = Card(Basic(catalog), 1); weak.currentInfluence = 1;
        var valuable = Card(Basic(catalog), 1); valuable.currentInfluence = 8;
        s.side1.hand.Clear(); s.side1.hand.Add(valuable); s.side1.hand.Add(weak);
        s.pendingEffect = new PendingCardEffect { actor = 1, source = source, effect = CardEffect.DiscardToPlay,
            choice = EffectChoice.HandDiscard, cards = new List<CardState> { valuable, weak } };
        Check(ai.TryChooseAction(1, out action, out _) && action.cardInstanceId == weak.instanceId, "Discard keeps the stronger card.");
        Check(!ai.TryChooseAction(0, out _, out _), "AI cannot choose another actor's pending effect.");
        Resolve(game, 1);
        // Root targets only primary performers; a strong untargetable assistant
        // must not keep triggering an ability against a depleted primary.
        var rootData = catalog.cards.First(c => c.effect == CardEffect.WeakenAndRoot);
        var root = Card(rootData, 1);
        var depleted = Card(Basic(catalog), 0); depleted.currentInfluence = 0; depleted.rootedRound = s.roundNumber;
        for (int i = 0; i < 25; i++) s.tiles[i] = new TileState();
        Put(s, depleted, 2); s.tiles[2].assist0 = Card(Basic(catalog), 0);
        Check(EnemyCardPolicy.AbilityBenefit(s, 1, root) == 0, "Root does not value an untargetable assistant.");
        var reviveData = catalog.cards.First(c => c.effect == CardEffect.ReviveAbility);
        var revive = Card(reviveData, 1); var dead = Card(Basic(catalog), 1); dead.currentInfluence = 0;
        s.side1.discard.Clear(); s.side1.discard.Add(dead); s.side1.energy = 8;
        Check(EnemyCardPolicy.AbilityBenefit(s, 1, revive) > 0, "Revival values printed influence of a depleted discarded card.");
        s.side1.hand.Clear(); s.activePlayerId = 1; s.phase = RoundPhase.Performance;
        Check(!ai.TryChooseAction(1, out _, out _), "Performance with no legal action passes.");
        s.pendingEffect = new PendingCardEffect { actor = 1, source = source, effect = CardEffect.MoveAllToCenter,
            choice = EffectChoice.MoveAlly, optional = true };
        Check(ai.TryChooseAction(1, out action, out _) && action.kind == BoardActionKind.FinishEffect, "Optional movement can finish without a target.");
        Resolve(game, 1);
        UnityEngine.Object.DestroyImmediate(game.gameObject);
    }

    private static void Match(DeckCatalog catalog, string player, string enemy, int local)
    {
        UnityEngine.Random.InitState(1800 + matches);
        var game = NewGame(catalog, local);
        var p = AssetDatabase.LoadAssetAtPath<DeckData>("Assets/Decks/" + player.ToLowerInvariant() + " effects ai.asset");
        game.enemyDeck = AssetDatabase.LoadAssetAtPath<DeckData>("Assets/Decks/" + enemy.ToLowerInvariant() + " effects ai.asset");
        if (local == 0) { game.player0Cards = p.cards.ToArray(); game.player0OpeningCard = p.vipCard; }
        else { game.player1Cards = p.cards.ToArray(); game.player1OpeningCard = p.vipCard; }
        game.minimumRoundEnergy = 1; game.StartMatch(matches % 2);
        int step;
        for (step = 0; step < 3000 && game.Snapshot.phase != RoundPhase.Finished; step++)
        {
            var s = game.Snapshot;
            if (s.phase == RoundPhase.EndRound)
            {
                while (game.RoundResolutionPending && game.ScoringRow < 5) game.ResolveNextScoringTile();
                if (game.RoundResolutionPending) game.FinishRoundResolution();
                Check(game.CompleteRound(), "Round completes."); continue;
            }
            int actor = s.pendingEffect?.actor ?? s.activePlayerId;
            if (game.enemyAI.TryChooseAction(actor, out var action, out _))
            {
                Check(game.CanSubmitFor(actor, action, out var error), error);
                Check(game.TrySubmitFor(actor, action), "AI match action executes."); actions++;
                if (action.kind == BoardActionKind.ChooseEffect || action.kind == BoardActionKind.FinishEffect) choices++;
                if (action.kind == BoardActionKind.ActivateEffect) abilities++;
            }
            else Check(game.TryPassFor(actor), "AI can legally pass without a pending choice.");
        }
        var final = game.Snapshot;
        Check(final.phase == RoundPhase.Finished, player + " vs " + enemy + " terminates within 3000 actions. " +
            "phase=" + final.phase + "; round=" + final.roundNumber + "; active=" + final.activePlayerId +
            "; energy=" + final.side0.energy + "/" + final.side1.energy + "; hand=" + final.side0.hand.Count + "/" + final.side1.hand.Count +
            "; decks=" + final.side0.deckCount + "/" + final.side1.deckCount + "; scores=" + final.side0.score + "/" + final.side1.score);
        matches++;
        evidence.Add(player + " vs " + enemy + " local=" + local + ": " + step + " steps, round " + game.Snapshot.roundNumber +
            ", score " + game.Snapshot.side0.score + "-" + game.Snapshot.side1.score + ".");
        UnityEngine.Object.DestroyImmediate(game.gameObject);
    }
}
#endif
