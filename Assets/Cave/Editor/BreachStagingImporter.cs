using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.Editor
{
    /// <summary>
    /// Imports only approved Breach environment authoring from a staging-scene copy.
    /// Player lifecycle, camera runtime components, transitions, markers, and mission
    /// authoring remain owned by the destination scene.
    /// </summary>
    public static class BreachStagingImporter
    {
        private const string SourceScenePath = "Assets/Cave/Scenes/Breach_StagingSource.unity";
        private const string DestinationScenePath = "Assets/Cave/Scenes/01_UpperCave.unity";
        private const string SceneRootName = "01_UpperCave";
        private const string ImportedVisualRootName = "Breach Staging Visuals";

        [MenuItem("Tools/Cave/Scenes/Import Approved Breach Staging")]
        public static void ImportApprovedBreach()
        {
            if (!File.Exists(SourceScenePath) || !File.Exists(DestinationScenePath))
            {
                Debug.LogError("[Cave] Breach import requires both the staging source copy and 01_UpperCave scene.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene destinationScene = GetOrOpenScene(DestinationScenePath);
            Scene sourceScene = GetOrOpenScene(SourceScenePath);
            if (!destinationScene.IsValid() || !sourceScene.IsValid())
            {
                Debug.LogError("[Cave] Breach import could not open its source or destination scene.");
                return;
            }

            GameObject destinationRoot = FindRoot(destinationScene, SceneRootName);
            GameObject sourceRoot = FindRoot(sourceScene, SceneRootName);
            if (destinationRoot == null || sourceRoot == null)
            {
                Debug.LogError("[Cave] Breach import could not locate the shared scene root.");
                CloseSourceScene(sourceScene);
                return;
            }

            try
            {
                ReplaceDirectChild(sourceRoot.transform, destinationRoot.transform, "Environment (o)", "Environment", destinationScene);
                ReplaceDirectChild(sourceRoot.transform, destinationRoot.transform, "LightingVFX", "LightingVFX", destinationScene);
                ReplaceNestedChild(sourceRoot.transform, destinationRoot.transform, "Gameplay", "Hazards", destinationScene);
                ReplaceNestedChild(sourceRoot.transform, destinationRoot.transform, "Camera", "Upper Cave Camera Bounds", destinationScene);
                ReplaceImportedVisualRoots(sourceScene, sourceRoot, destinationScene);

                EditorSceneManager.MarkSceneDirty(destinationScene);
                EditorSceneManager.SaveScene(destinationScene);
                EditorSceneManager.SetActiveScene(destinationScene);

                int removedMissingScripts = RemoveMissingScripts(destinationScene);
                if (removedMissingScripts > 0)
                {
                    EditorSceneManager.MarkSceneDirty(destinationScene);
                    EditorSceneManager.SaveScene(destinationScene);
                    Debug.Log("[Cave] Breach import removed " + removedMissingScripts + " pre-existing null script component(s).");
                }

                ValidateDestination(destinationScene);
                Debug.Log("[Cave] Approved Breach environment imported. Main runtime and mission roots were preserved.");
            }
            finally
            {
                CloseSourceScene(sourceScene);
            }
        }

        private static Scene GetOrOpenScene(string scenePath)
        {
            Scene loadedScene = SceneManager.GetSceneByPath(scenePath);
            return loadedScene.IsValid() && loadedScene.isLoaded
                ? loadedScene
                : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        }

        private static void ReplaceDirectChild(
            Transform sourceParent,
            Transform destinationParent,
            string sourceName,
            string destinationName,
            Scene destinationScene)
        {
            Transform sourceChild = FindDirectChild(sourceParent, sourceName);
            if (sourceChild == null)
            {
                throw new InvalidOperationException("Missing staging child: " + sourceName);
            }

            DestroyDirectChild(destinationParent, destinationName);
            MoveToParent(sourceChild, destinationParent, destinationName, destinationScene);
        }

        private static void ReplaceNestedChild(
            Transform sourceRoot,
            Transform destinationRoot,
            string parentName,
            string childName,
            Scene destinationScene)
        {
            Transform sourceParent = FindDirectChild(sourceRoot, parentName);
            Transform destinationParent = FindDirectChild(destinationRoot, parentName);
            if (sourceParent == null || destinationParent == null)
            {
                throw new InvalidOperationException("Missing required runtime group: " + parentName);
            }

            Transform sourceChild = FindDirectChild(sourceParent, childName);
            if (sourceChild == null)
            {
                throw new InvalidOperationException("Missing staging child: " + childName);
            }

            DestroyDirectChild(destinationParent, childName);
            MoveToParent(sourceChild, destinationParent, childName, destinationScene);
        }

        private static void ReplaceImportedVisualRoots(Scene sourceScene, GameObject sourceSceneRoot, Scene destinationScene)
        {
            GameObject priorVisualRoot = FindRoot(destinationScene, ImportedVisualRootName);
            if (priorVisualRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(priorVisualRoot);
            }

            GameObject visualRoot = new GameObject(ImportedVisualRootName);
            SceneManager.MoveGameObjectToScene(visualRoot, destinationScene);

            GameObject[] sourceRoots = sourceScene.GetRootGameObjects();
            for (int index = 0; index < sourceRoots.Length; index++)
            {
                GameObject sourceRoot = sourceRoots[index];
                if (sourceRoot == null || sourceRoot == sourceSceneRoot)
                {
                    continue;
                }

                SceneManager.MoveGameObjectToScene(sourceRoot, destinationScene);
                sourceRoot.transform.SetParent(visualRoot.transform, true);
            }
        }

        private static void MoveToParent(
            Transform sourceChild,
            Transform destinationParent,
            string destinationName,
            Scene destinationScene)
        {
            sourceChild.SetParent(null, true);
            SceneManager.MoveGameObjectToScene(sourceChild.gameObject, destinationScene);
            sourceChild.SetParent(destinationParent, true);
            sourceChild.name = destinationName;
        }

        private static void DestroyDirectChild(Transform parent, string childName)
        {
            Transform existingChild = FindDirectChild(parent, childName);
            if (existingChild != null)
            {
                UnityEngine.Object.DestroyImmediate(existingChild.gameObject);
            }
        }

        private static Transform FindDirectChild(Transform parent, string childName)
        {
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        private static GameObject FindRoot(Scene scene, string rootName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                if (roots[index].name == rootName)
                {
                    return roots[index];
                }
            }

            return null;
        }

        private static void ValidateDestination(Scene destinationScene)
        {
            int missingScripts = 0;
            int activeCameras = 0;
            int audioListeners = 0;
            GameObject[] roots = destinationScene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                MonoBehaviour[] behaviours = roots[rootIndex].GetComponentsInChildren<MonoBehaviour>(true);
                for (int behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
                {
                    if (behaviours[behaviourIndex] == null)
                    {
                        missingScripts++;
                    }
                }

                Camera[] cameras = roots[rootIndex].GetComponentsInChildren<Camera>(true);
                for (int cameraIndex = 0; cameraIndex < cameras.Length; cameraIndex++)
                {
                    if (cameras[cameraIndex].isActiveAndEnabled)
                    {
                        activeCameras++;
                    }
                }

                AudioListener[] listeners = roots[rootIndex].GetComponentsInChildren<AudioListener>(true);
                for (int listenerIndex = 0; listenerIndex < listeners.Length; listenerIndex++)
                {
                    if (listeners[listenerIndex].isActiveAndEnabled)
                    {
                        audioListeners++;
                    }
                }
            }

            if (missingScripts > 0)
            {
                Debug.LogError("[Cave] Breach import validation found " + missingScripts + " missing script component(s).");
            }

            if (activeCameras != 1 || audioListeners != 1)
            {
                Debug.LogError(
                    "[Cave] Breach import validation expected one active Camera and AudioListener; found "
                    + activeCameras + " camera(s) and " + audioListeners + " listener(s).");
            }
        }

        private static int RemoveMissingScripts(Scene destinationScene)
        {
            int removedCount = 0;
            GameObject[] roots = destinationScene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Transform[] transforms = roots[rootIndex].GetComponentsInChildren<Transform>(true);
                for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
                {
                    removedCount += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(
                        transforms[transformIndex].gameObject);
                }
            }

            return removedCount;
        }

        private static void CloseSourceScene(Scene sourceScene)
        {
            if (sourceScene.IsValid() && sourceScene.isLoaded)
            {
                EditorSceneManager.CloseScene(sourceScene, true);
            }
        }
    }
}
