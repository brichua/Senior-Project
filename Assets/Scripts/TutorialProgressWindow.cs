#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace VocaloidTCG
{
    public sealed class TutorialProgressWindow : EditorWindow
    {
        [MenuItem("Tools/Progress/Tutorial Progress")]
        public static void Open()
        {
            GetWindow<TutorialProgressWindow>("Tutorial Progress");
        }

        private void OnInspectorUpdate() { Repaint(); }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Saved Tutorial Progress", EditorStyles.boldLabel);
            bool completed = TutorialProgress.Completed;
            EditorGUI.BeginChangeCheck();
            completed = EditorGUILayout.Toggle("Tutorial Completed", completed);
            if (EditorGUI.EndChangeCheck()) TutorialProgress.SetCompleted(completed);
            EditorGUILayout.HelpBox("Uncheck to restore the Main Menu and Story class locks. Check to unlock them. Changes are saved immediately and work across scenes.\n\nCompleted lessons, cards, and rewards are kept. Separate debug-unlock options still bypass the locks.", MessageType.Info);
        }
    }
}
#endif
