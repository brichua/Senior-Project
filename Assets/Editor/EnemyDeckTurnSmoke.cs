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
    private static readonly string[] Pools = { "miku", "len", "luka" };
    private static readonly List<string> results = new List<string>();
    static EnemyDeckTurnSmoke()
    {
        if (SessionState.GetBool(Running, false)) EditorApplication.update += Tick;
    }

    [MenuItem("Tools/Enemy AI/Run Three Deck Turn Smoke")]
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
                Finish(true, "All three native AI turns completed.");
            }
        }
        catch (Exception ex) { Finish(false, ex.ToString()); }
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
