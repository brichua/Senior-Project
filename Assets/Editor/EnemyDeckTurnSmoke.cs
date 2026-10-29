#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VocaloidTCG;
using VocaloidTCG.BoardUI;

// Exercises the real MonoBehaviour.Update, animation gates and pause, rather
// than directly invoking the AI selector as the synchronous engine suite does.
[InitializeOnLoad]
public static class EnemyDeckTurnSmoke
{
    private const string Running = "EnemyDeckTurnSmoke.Running";
    private static GameplayBoardBridge game;
    private static int pool, stage, initialHand;
    private static bool previousBackground;
    private static double phaseStart;
    private static readonly string[] Pools = { "miku", "len", "luka", "rin", "meiko", "kaito" };
    private static readonly List<string> results = new List<string>();
    static EnemyDeckTurnSmoke()
    {
        if (SessionState.GetBool(Running, false)) EditorApplication.update += Tick;
    }

    [MenuItem("Tools/Enemy AI/Run Six Deck Turn Smoke")]
    public static void Run()
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Stop Play Mode and save the current scene first.");
        EditorSceneManager.OpenScene("Assets/Scenes/Game Bri - Enemy AI Test.unity");
        pool = stage = 0; game = null; results.Clear();
        SessionState.SetBool(Running, true);
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (!game)
            {
                game = UnityEngine.Object.FindAnyObjectByType<GameplayBoardBridge>();
                if (!game) return;
                previousBackground = Application.runInBackground; Application.runInBackground = true;
                game.startAutomatically = false; game.enableOpeningSequence = false; game.turnSeconds = 0;
            }
            if (EditorApplication.isPaused) EditorApplication.isPaused = false;
            EditorApplication.QueuePlayerLoopUpdate();
            if (stage == 0)
            {
                game.enemyDeck = AssetDatabase.LoadAssetAtPath<DeckData>("Assets/Decks/" + Pools[pool] + " effects ai.asset");
                game.enemyAI = game.GetComponent<EnemyAIController>();
                game.enemyAI.actionDelay = 0.1f;
                game.StartMatch(1);
                var card = game.enemyDeck.cards.First(c => c.performer && c.cost <= 3 && c.effect != CardEffect.DiscardToPlay);
                game.Snapshot.side1.hand[0] = new CardState { data = card, ownerId = 1, instanceId = "smoke-guaranteed-performer",
                    currentCost = card.cost, currentInfluence = card.influence };
                game.Snapshot.side1.energy = 8;
                initialHand = game.Snapshot.side1.hand.Count;
                game.SetLocalPause(true);
                phaseStart = EditorApplication.timeSinceStartup; stage = 1; return;
            }
            if (stage == 1)
            {
                if (EditorApplication.timeSinceStartup - phaseStart < 0.6) return;
                if (game.Snapshot.side1.hand.Count != initialHand || game.Snapshot.activePlayerId != 1)
                    throw new InvalidOperationException("AI acted during pause.");
                game.SetLocalPause(false); phaseStart = EditorApplication.timeSinceStartup; stage = 2; return;
            }
            if (stage == 2)
            {
                if (EditorApplication.timeSinceStartup - phaseStart > 35)
                    throw new InvalidOperationException(Pools[pool] + " timed out: phase=" + game.Snapshot.phase + ", active=" + game.Snapshot.activePlayerId +
                        ", draw=" + game.DrawAnimationPlaying + ", phaseAnimation=" + game.PhaseAnimationPlaying + ", pending=" + game.Snapshot.pendingEffect?.choice);
                if (game.Snapshot.activePlayerId != 0 || game.Snapshot.pendingEffect != null || game.DrawAnimationPlaying || game.PhaseAnimationPlaying) return;
                if (!game.Snapshot.tiles.Any(t => t.side1 != null)) throw new InvalidOperationException("Enemy did not place its guaranteed legal performer.");
                results.Add(Pools[pool] + ": native AI placed a performer, finished choices and passed to player; pause blocked AI.");
                if (++pool < Pools.Length) { stage = 0; return; }
                StartPendingChoice(false); stage = 3;
            }
            if (stage == 3 || stage == 4)
            {
                if (EditorApplication.timeSinceStartup - phaseStart > 5)
                    throw new InvalidOperationException("AI-owned off-turn/scoring choice timed out.");
                if (game.Snapshot.pendingEffect != null) return;
                if (game.Snapshot.side1.hand.Any(c => c.instanceId == "smoke-pending-discard"))
                    throw new InvalidOperationException("Pending choice disappeared without discarding its selected card.");
                results.Add(stage == 3 ? "AI Update resolved its choice during the player's turn." : "AI Update resolved its choice during EndRound with ordinary input disabled.");
                if (stage == 3) { StartPendingChoice(true); stage = 4; return; }
                Finish(true, "All six native AI turns and both pending-choice timing checks completed.");
            }
        }
        catch (Exception ex) { Finish(false, ex.ToString()); }
    }

    private static void StartPendingChoice(bool scoring)
    {
        var state = game.Snapshot;
        state.phase = scoring ? RoundPhase.EndRound : RoundPhase.Preparation;
        state.activePlayerId = 0;
        state.inputAllowed = !scoring;
        game.endRoundDisplaySeconds = 1000;
        var data = game.enemyDeck.cards.First(c => c.performer);
        var card = new CardState { data = data, ownerId = 1, instanceId = "smoke-pending-discard",
            currentCost = data.cost, currentInfluence = data.influence };
        state.side1.hand.Add(card);
        state.pendingEffect = new PendingCardEffect { actor = 1, source = card, effect = CardEffect.DiscardToPlay,
            choice = EffectChoice.HandDiscard, cards = new List<CardState> { card } };
        phaseStart = EditorApplication.timeSinceStartup;
    }

    private static void Finish(bool success, string message)
    {
        SessionState.SetBool(Running, false); EditorApplication.update -= Tick;
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/enemy-deck-turn-smoke.txt", (success ? "PASS\n" : "FAIL\n") + message + "\n" + string.Join("\n", results));
        Debug.Log("[Enemy turn smoke] " + (success ? "PASS " : "FAIL ") + message);
        Application.runInBackground = previousBackground;
        EditorApplication.ExitPlaymode();
    }
}
#endif
