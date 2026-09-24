using System.Collections.Generic;
using Cave.Domain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.Editor
{
    /// <summary>Explicit opt-in importer and scene builder for the development-only False God playtest.</summary>
    public static class FalseGodPlaytestAssetBuilder
    {
        private const string ArtRoot = "Assets/Cave/Resources/FalseGod/Prophet";
        private const string BodyRoot = ArtRoot + "/false_god_all_cropped_animations_final";
        private const string VfxRoot = ArtRoot + "/false_god_form1_vfx_cropped";
        private const string AnimationRoot = "Assets/Cave/Animations/Development/FalseGod/Prophet";
        private const string ScenePath = "Assets/Cave/Scenes/Development/FalseGod_Playtest.unity";
        private const string WraithArtRoot = "Assets/Cave/Resources/Domain/ClaimWraith/wraith_cropped_frames_fixed";
        private const string WraithAnimationRoot = "Assets/Cave/Animations/Development/ClaimWraith";
        private const string WraithPrefabPath = "Assets/Cave/Resources/Domain/ClaimWraith/ClaimWraith.prefab";

        [MenuItem("Tools/Cave/False God/Build Prophet Playtest Assets")]
        public static void BuildProphetPlaytestAssets()
        {
            ConfigureFolder(BodyRoot);
            ConfigureFolder(VfxRoot);
            AssetDatabase.Refresh();
            BuildClips(BodyRoot, "Body");
            BuildClips(VfxRoot, "Vfx");
            AssetDatabase.SaveAssets();
            Debug.Log("[Cave] False God Prophet playtest art imported and animation clips generated.");
        }

        [MenuItem("Tools/Cave/False God/Build Claim Wraith Assets")]
        public static void BuildClaimWraithAssets()
        {
            ConfigureFolder(WraithArtRoot);
            AssetDatabase.Refresh();
            BuildClips(WraithArtRoot, WraithAnimationRoot, "Wraith");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(WraithPrefabPath) == null)
            {
                GameObject skeleton = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cave/Prefabs/Mobs/Swarm/Skeleton.prefab");
                if (skeleton == null)
                {
                    Debug.LogError("[Cave] Claim Wraith build stopped: canonical Skeleton prefab was not found.");
                    return;
                }
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(skeleton);
                instance.name = "ClaimWraith";
                instance.AddComponent<ClaimWraith>();
                instance.AddComponent<ClaimWraithPresentation>();
                PrefabUtility.SaveAsPrefabAsset(instance, WraithPrefabPath);
                Object.DestroyImmediate(instance);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Cave] Claim Wraith assets and independent Skeleton-based prefab were built.");
        }

        [MenuItem("Tools/Cave/False God/Create Development Playtest Scene")]
        public static void CreateDevelopmentPlaytestScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Debug.LogWarning("[Cave] False God development scene already exists; it was not overwritten: " + ScenePath);
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject camera = new GameObject("Playtest Camera");
            Camera cameraComponent = camera.AddComponent<Camera>();
            cameraComponent.orthographic = true;
            cameraComponent.orthographicSize = 6f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.tag = "MainCamera";

            GameObject floor = new GameObject("Development Floor");
            floor.transform.position = new Vector2(0f, -2.1f);
            BoxCollider2D floorCollider = floor.AddComponent<BoxCollider2D>();
            floorCollider.size = new Vector2(24f, 1f);

            GameObject altar = new GameObject("Background Altar (Visual Only)");
            altar.transform.position = new Vector3(0f, 0.8f, 0f);
            altar.transform.localScale = Vector3.one * 2.2f;
            SpriteRenderer altarRenderer = altar.AddComponent<SpriteRenderer>();
            altarRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Cave/Resources/Player/CurseAltar/CurseAltarActive.png");
            altarRenderer.sortingOrder = -20;

            GameObject host = new GameObject("False God Playtest Encounter (Development Only)");
            FalseGodPlaytestEncounter encounter = host.AddComponent<FalseGodPlaytestEncounter>();
            encounter.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cave/Prefabs/Player.prefab"));
            EnsureDirectory("Assets/Cave/Scenes/Development");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = host;
            Debug.Log("[Cave] Created development-only False God playtest scene: " + ScenePath);
        }

        private static void ConfigureFolder(string root)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { root });
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Point;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = Mathf.Max(2048, importer.maxTextureSize);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }

        private static void BuildClips(string root, string category)
        {
            BuildClips(root, AnimationRoot, category);
        }

        private static void BuildClips(string root, string animationRoot, string category)
        {
            string[] folders = AssetDatabase.GetSubFolders(root);
            for (int index = 0; index < folders.Length; index++)
            {
                string folder = folders[index];
                string[] frameGuids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
                List<Sprite> frames = new List<Sprite>(frameGuids.Length);
                for (int frameIndex = 0; frameIndex < frameGuids.Length; frameIndex++)
                {
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(frameGuids[frameIndex]));
                    if (sprite != null)
                    {
                        frames.Add(sprite);
                    }
                }

                frames.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
                if (frames.Count == 0)
                {
                    continue;
                }

                string name = folder.Substring(folder.LastIndexOf('/') + 1);
                string outputFolder = animationRoot + "/" + category;
                EnsureDirectory(animationRoot);
                EnsureDirectory(outputFolder);
                string clipPath = outputFolder + "/" + name + ".anim";
                if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) != null)
                {
                    continue;
                }

                AnimationClip clip = new AnimationClip { frameRate = 12f, wrapMode = WrapMode.Loop };
                ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[frames.Count];
                for (int keyIndex = 0; keyIndex < frames.Count; keyIndex++)
                {
                    keys[keyIndex] = new ObjectReferenceKeyframe { time = keyIndex / clip.frameRate, value = frames[keyIndex] };
                }

                EditorCurveBinding binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite");
                AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
                AssetDatabase.CreateAsset(clip, clipPath);
            }
        }

        private static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = path.Substring(0, path.LastIndexOf('/'));
            string name = path.Substring(path.LastIndexOf('/') + 1);
            EnsureDirectory(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
