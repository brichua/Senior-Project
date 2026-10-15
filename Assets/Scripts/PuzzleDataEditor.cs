using System;
using UnityEditor;
using UnityEngine;

namespace VocaloidTCG.Editor
{
    [CustomEditor(typeof(PuzzleData))]
    public sealed class PuzzleDataEditor : UnityEditor.Editor
    {
        private DeckCatalog deckCatalog;
        private string saveError;

        private void OnEnable(){
            foreach(string guid in AssetDatabase.FindAssets("t:PuzzleCatalog")){
                var catalog = AssetDatabase.LoadAssetAtPath<PuzzleCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                if(catalog && catalog.puzzles != null && catalog.puzzles.Contains((PuzzleData)target)){
                    deckCatalog = catalog.deckCatalog;
                    if(deckCatalog) break;
                }
            }
        }

        public override void OnInspectorGUI(){
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Saved Progress (Debug)", EditorStyles.boldLabel);

            try{
                var puzzle = (PuzzleData)target;
                var library = DeckLibrary.Get(deckCatalog);
                bool cleared = library.IsPuzzleCompleted(puzzle);
                EditorGUI.BeginChangeCheck();
                bool next = EditorGUILayout.Toggle(new GUIContent("Cleared", "Whether this puzzle is cleared in the local save."), cleared);
                if(EditorGUI.EndChangeCheck()){
                    if(library.SetPuzzleClearedForDebugging(puzzle, next, out string error)) saveError = null;
                    else saveError = error;
                }
            }catch(Exception ex){
                EditorGUILayout.HelpBox(ex.Message, MessageType.Error);
            }
            if(!string.IsNullOrEmpty(saveError)) EditorGUILayout.HelpBox(saveError, MessageType.Error);
        }

        public override bool RequiresConstantRepaint() => EditorApplication.isPlaying;
    }
}
