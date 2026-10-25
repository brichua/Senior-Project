#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VocaloidTCG;
using VocaloidTCG.BoardUI;

public static class EnemyDeckPreview
{
    [MenuItem("Tools/Enemy AI/Prepare Miku Test")]
    public static void Miku() => Prepare("miku");
    [MenuItem("Tools/Enemy AI/Prepare Len Test")]
    public static void Len() => Prepare("len");
    [MenuItem("Tools/Enemy AI/Prepare Luka Test")]
    public static void Luka() => Prepare("luka");
    [MenuItem("Tools/Enemy AI/Prepare Rin Test")]
    public static void Rin() => Prepare("rin");
    [MenuItem("Tools/Enemy AI/Prepare MEIKO Test")]
    public static void Meiko() => Prepare("meiko");
    [MenuItem("Tools/Enemy AI/Prepare KAITO Test")]
    public static void Kaito() => Prepare("kaito");

    private static void Prepare(string pool)
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Stop Play Mode and save your current scene before preparing an AI test.");
        EditorSceneManager.OpenScene("Assets/Scenes/Game Bri - Enemy AI Test.unity");
        var game = UnityEngine.Object.FindAnyObjectByType<GameplayBoardBridge>();
        var catalog = AssetDatabase.LoadAssetAtPath<DeckCatalog>("Assets/Decks/deck catalog.asset");
        var deck = AssetDatabase.LoadAssetAtPath<DeckData>("Assets/Decks/" + pool + " effects ai.asset");
        game.useSelectedDeck = false; game.deckCatalog = catalog; game.enemyDeck = deck;
        game.player0Cards = catalog.starterDeck.cards.ToArray(); game.player0OpeningCard = catalog.starterDeck.vipCard;
        game.playerClasses = catalog.starterDeck.classes.ToList();
        game.enemyClasses = deck.classes.ToList();
        game.enemyAI = game.GetComponent<EnemyAIController>(); game.enemyAI.game = game;
        game.enableOpeningSequence = true; game.minimumRoundEnergy = 1;
        Selection.activeGameObject = game.gameObject;
        Debug.Log("[Enemy AI] Prepared " + deck.deckName + ". Press Play. Test settings are unsaved; personal decks/saves are unused.");
    }
}
#endif
