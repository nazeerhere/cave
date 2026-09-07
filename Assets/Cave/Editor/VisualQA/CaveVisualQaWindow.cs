using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.Editor.VisualQA
{
    /// <summary>Small explicit entry point for editor-only room evidence exports.</summary>
    public sealed class CaveVisualQaWindow : EditorWindow
    {
        [MenuItem("Cave/Visual QA")]
        public static void Open()
        {
            GetWindow<CaveVisualQaWindow>("Cave Visual QA");
        }

        private void OnGUI()
        {
            Scene scene = SceneManager.GetActiveScene();
            EditorGUILayout.LabelField("Cave Visual QA Bridge", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Exports read-only visual and structural evidence. Capture creates temporary hidden render objects only; it does not save or edit the scene.",
                MessageType.Info);
            EditorGUILayout.LabelField("Active Scene", scene.IsValid() ? scene.name : "None");
            EditorGUILayout.LabelField("Output", CaveVisualQaCapture.OutputRoot + "/<SceneName>/");
            GUILayout.Space(8f);

            using (new EditorGUI.DisabledScope(!scene.IsValid() || !scene.isLoaded))
            {
                if (GUILayout.Button("Capture Current Scene"))
                {
                    Execute(() => CaveVisualQaCapture.CaptureAll(scene));
                }

                if (GUILayout.Button("Capture Room Overview"))
                {
                    Execute(() => CaveVisualQaCapture.CaptureRoomOverview(scene));
                }

                if (GUILayout.Button("Capture Game View"))
                {
                    Execute(() => CaveVisualQaCapture.CaptureGameView(scene));
                }

                if (GUILayout.Button("Export Manifest"))
                {
                    Execute(() => CaveVisualQaCapture.ExportManifest(scene));
                }

                if (GUILayout.Button("Capture All"))
                {
                    Execute(() => CaveVisualQaCapture.CaptureAll(scene));
                }
            }
        }

        private static void Execute(Func<string> action)
        {
            try
            {
                string outputDirectory = action();
                Debug.Log("Cave Visual QA completed: " + outputDirectory);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Cave Visual QA", exception.Message, "OK");
            }
        }
    }
}
