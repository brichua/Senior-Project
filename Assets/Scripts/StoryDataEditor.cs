#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace VocaloidTCG.Editor
{
    [CustomEditor(typeof(StoryData))]
    public sealed class StoryDataEditor : UnityEditor.Editor
    {
        private DeckCatalog deckCatalog;
        private string saveError;

        private void OnEnable(){
            foreach(string guid in AssetDatabase.FindAssets("t:StoryCatalog")){
                var catalog = AssetDatabase.LoadAssetAtPath<StoryCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                if(catalog && catalog.stories != null && catalog.stories.Contains((StoryData)target)){
                    deckCatalog = catalog.deckCatalog;
                    if(deckCatalog) break;
                }
            }
        }

        public override void OnInspectorGUI(){
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Saved Audition Progress (Debug)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Changes the local save. Checking an audition grants its first-clear reward. Unchecking resets its first clear but keeps owned cards and viewed dialogue.", MessageType.Info);

            try{
                var story = (StoryData)target;
                var library = DeckLibrary.Get(deckCatalog);
                if(story.auditions != null) foreach(var audition in story.auditions){
                    if(audition == null) continue;
                    var character = library.Catalog.Class(audition.opponent);
                    string name = character ? character.DisplayName : audition.opponent.ToString();
                    bool cleared = library.IsAuditionCompleted(story, audition.opponent);
                    EditorGUI.BeginChangeCheck();
                    bool next = EditorGUILayout.Toggle(new GUIContent(name + " — First Clear",
                        "Whether this audition has been cleared in the local save."), cleared);
                    if(EditorGUI.EndChangeCheck()){
                        if(library.SetAuditionClearedForDebugging(story, audition.opponent, next, out string error)) saveError = null;
                        else saveError = error;
                    }
                }
            }catch(Exception ex){
                EditorGUILayout.HelpBox(ex.Message, MessageType.Error);
            }
            if(!string.IsNullOrEmpty(saveError)) EditorGUILayout.HelpBox(saveError, MessageType.Error);
        }

        public override bool RequiresConstantRepaint() => EditorApplication.isPlaying;
    }
}
#endif
