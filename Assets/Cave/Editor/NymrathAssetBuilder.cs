using System;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>Idempotently converts the approved, individually extracted
    /// Nymrath frames into Cave-owned clips, controllers, and prefabs. It never
    /// slices the variable-geometry source sheets.</summary>
    [InitializeOnLoad]
    public static class NymrathAssetBuilder
    {
        private const string Root = "Assets/Cave/Art/Creatures/Nymrath";
        private const string ClipRoot = Root + "/Animations";
        private const string ControllerRoot = Root + "/Controllers";
        private const string NormalSource = Root + "/Source/Normal/Grotto_Mender_Sprites";
        private const string QilinSource = Root + "/Source/CorruptedQilin/Corrupted_Qilin_Sprites";
        private static readonly string[] NormalActions =
        {
            "idle", "walk", "glide", "alert", "burrow_entry", "burrow_emergence", "hit", "death",
            "restoration_weave", "living_bastion", "tendril_lash", "burrow_ambush"
        };
        private static readonly string[] QilinActions =
        {
            "idle", "walk", "glide", "alert", "burrow_entry", "burrow_emergence", "hit", "death",
            "restoration_weave", "living_bastion", "tendril_lash", "burrow_ambush", "eternal_bloom",
            "preservation_prison", "spectral_stampede"
        };

        static NymrathAssetBuilder()
        {
            EditorApplication.delayCall += BuildMissingArtifacts;
        }

        [MenuItem("Cave/Creatures/Nymrath/Build Animation Assets and Prefabs")]
        private static void BuildAllFromMenu()
        {
            BuildArtifacts(true);
        }

        private static void BuildMissingArtifacts()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode
                || AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cave/Prefabs/Mobs/Support/Nymrath.prefab") != null
                || CountExtractedFrames() < 224)
            {
                return;
            }

            BuildArtifacts(false);
        }

        internal static void BuildIfReady()
        {
            BuildMissingArtifacts();
        }

        private static void BuildArtifacts(bool rebuildExisting)
        {
            if (CountExtractedFrames() < 224)
            {
                Debug.LogWarning("Nymrath asset build deferred: approved extracted frames are not all imported yet.");
                return;
            }

            EnsureFolder("Assets/Cave/Art/Creatures");
            EnsureFolder(Root);
            EnsureFolder(ClipRoot);
            EnsureFolder(ControllerRoot);
            BuildForm("Nymrath", NormalSource, NormalActions, false, rebuildExisting);
            BuildForm("CorruptedQilin", QilinSource, QilinActions, true, rebuildExisting);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Nymrath animation assets and prefabs are ready.");
        }

        private static void BuildForm(string formName, string sourceRoot, string[] actions, bool qilin, bool rebuildExisting)
        {
            string formClipRoot = ClipRoot + "/" + formName;
            EnsureFolder(formClipRoot);
            Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
            for (int index = 0; index < actions.Length; index++)
            {
                string action = actions[index];
                AnimationClip clip = BuildClip(formName, sourceRoot, action, formClipRoot, rebuildExisting);
                if (clip != null)
                {
                    clips.Add(action, clip);
                }
            }

            AnimatorController controller = BuildController(formName, clips, rebuildExisting);
            BuildPrefab(formName, sourceRoot, controller, qilin, rebuildExisting);
        }

        private static AnimationClip BuildClip(string formName, string sourceRoot, string action, string outputFolder, bool rebuildExisting)
        {
            string clipPath = outputFolder + "/" + ToClipName(formName, action) + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip != null && !rebuildExisting)
            {
                return clip;
            }

            List<Sprite> frames = LoadFrames(sourceRoot + "/" + action);
            if (frames.Count == 0)
            {
                Debug.LogWarning("Nymrath has no imported frames for " + formName + " / " + action + ".");
                return null;
            }

            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, clipPath);
            }

            clip.frameRate = 12f;
            ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[frames.Count];
            for (int index = 0; index < frames.Count; index++)
            {
                keys[index] = new ObjectReferenceKeyframe { time = index / clip.frameRate, value = frames[index] };
            }

            EditorCurveBinding binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = string.Empty,
                propertyName = "m_Sprite"
            };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = IsLooping(action);
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimatorController BuildController(string formName, Dictionary<string, AnimationClip> clips, bool rebuildExisting)
        {
            string path = ControllerRoot + "/" + formName + ".controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller != null)
            {
                return controller;
            }

            controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState idleState = null;
            foreach (KeyValuePair<string, AnimationClip> pair in clips)
            {
                AnimatorState state = stateMachine.AddState(ToTriggerName(pair.Key));
                state.motion = pair.Value;
                if (pair.Key == "idle")
                {
                    idleState = state;
                }
            }

            if (idleState == null)
            {
                return controller;
            }

            stateMachine.defaultState = idleState;
            AnimatorState walkState = FindState(stateMachine, "Walk");
            if (walkState != null)
            {
                controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
                AnimatorStateTransition startWalking = idleState.AddTransition(walkState);
                startWalking.hasExitTime = false;
                startWalking.duration = .05f;
                startWalking.AddCondition(AnimatorConditionMode.If, 0f, "Moving");
                AnimatorStateTransition stopWalking = walkState.AddTransition(idleState);
                stopWalking.hasExitTime = false;
                stopWalking.duration = .05f;
                stopWalking.AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");
            }
            foreach (KeyValuePair<string, AnimationClip> pair in clips)
            {
                if (pair.Key == "idle" || pair.Key == "walk")
                {
                    continue;
                }

                string trigger = ToTriggerName(pair.Key);
                controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
                AnimatorState state = FindState(stateMachine, trigger);
                AnimatorStateTransition begin = stateMachine.AddAnyStateTransition(state);
                begin.hasExitTime = false;
                begin.duration = 0.02f;
                begin.AddCondition(AnimatorConditionMode.If, 0f, trigger);
                if (!IsLooping(pair.Key))
                {
                    AnimatorStateTransition end = state.AddTransition(idleState);
                    end.hasExitTime = true;
                    end.exitTime = .98f;
                    end.duration = 0.04f;
                }
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void BuildPrefab(string formName, string sourceRoot, AnimatorController controller, bool qilin, bool rebuildExisting)
        {
            string folder = qilin ? "Assets/Cave/Prefabs/Mobs/Tank" : "Assets/Cave/Prefabs/Mobs/Support";
            EnsureFolder("Assets/Cave/Prefabs");
            EnsureFolder("Assets/Cave/Prefabs/Mobs");
            EnsureFolder(folder);
            string path = folder + "/" + (qilin ? "Corrupted Qilin.prefab" : "Nymrath.prefab");
            if (!rebuildExisting && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                return;
            }

            GameObject root = new GameObject(qilin ? "Corrupted Qilin" : "Nymrath");
            try
            {
                SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
                List<Sprite> idleFrames = LoadFrames(sourceRoot + "/idle");
                renderer.sprite = idleFrames.Count > 0 ? idleFrames[0] : null;
                Animator animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                Rigidbody2D body = root.AddComponent<Rigidbody2D>();
                body.gravityScale = 1f;
                body.freezeRotation = true;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(.55f, .65f);
                collider.offset = new Vector2(0f, .325f);
                root.AddComponent<Damageable>();
                root.AddComponent<EnemyController>();
                root.AddComponent<EnemyStagger>();
                NymrathCore core = root.AddComponent<NymrathCore>();
                SerializedObject serializedCore = new SerializedObject(core);
                serializedCore.FindProperty("corruptedQilin").boolValue = qilin;
                serializedCore.FindProperty("burrowGroundLayers").intValue = LayerMask.GetMask("Ground", "Terrain");
                serializedCore.ApplyModifiedPropertiesWithoutUndo();
                root.AddComponent<NymrathBrain>();
                root.AddComponent<NymrathAnimatorPresenter>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static int CountExtractedFrames()
        {
            return AssetDatabase.FindAssets("t:Sprite", new[] { NormalSource, QilinSource }).Length;
        }

        private static List<Sprite> LoadFrames(string folder)
        {
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
            List<string> paths = new List<string>(guids.Length);
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                if (NymrathSpriteImportSettings.IsExtractedFrame(path))
                {
                    paths.Add(path);
                }
            }

            paths.Sort(StringComparer.Ordinal);
            List<Sprite> frames = new List<Sprite>(paths.Count);
            for (int index = 0; index < paths.Count; index++)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(paths[index]);
                if (sprite != null)
                {
                    frames.Add(sprite);
                }
            }

            return frames;
        }

        private static AnimatorState FindState(AnimatorStateMachine machine, string name)
        {
            ChildAnimatorState[] states = machine.states;
            for (int index = 0; index < states.Length; index++)
            {
                if (states[index].state.name == name)
                {
                    return states[index].state;
                }
            }

            return null;
        }

        private static bool IsLooping(string action)
        {
            return action == "idle" || action == "walk" || action == "glide" || action == "restoration_weave";
        }

        private static string ToClipName(string form, string action)
        {
            return form + "_" + ToTriggerName(action);
        }

        private static string ToTriggerName(string action)
        {
            string[] words = action.Split('_');
            string name = string.Empty;
            for (int index = 0; index < words.Length; index++)
            {
                if (words[index].Length > 0)
                {
                    name += char.ToUpperInvariant(words[index][0]) + words[index].Substring(1);
                }
            }

            return name;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int slash = path.LastIndexOf('/');
            if (slash <= 0)
            {
                return;
            }

            string parent = path.Substring(0, slash);
            string name = path.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
