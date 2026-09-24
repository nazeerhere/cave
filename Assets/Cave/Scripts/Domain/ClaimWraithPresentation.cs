using System;
using System.Collections.Generic;
using Cave.Enemies;
using UnityEngine;

namespace Cave.Domain
{
    /// <summary>Sprite-only Wraith presentation. Combat and movement remain Skeleton authority.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ClaimWraith))]
    public sealed class ClaimWraithPresentation : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float framesPerSecond = 10f;
        private readonly Dictionary<string, Sprite[]> clips = new Dictionary<string, Sprite[]>();
        private ClaimWraith wraith;
        private EnemyMeleeCombat melee;
        private EnemyDefenseController defense;
        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private ClaimWraithAction action;
        private Sprite[] frames;
        private float elapsed;
        private Material affinityMaterial;

        private void Awake()
        {
            wraith = GetComponent<ClaimWraith>(); melee = GetComponent<EnemyMeleeCombat>();
            defense = GetComponent<EnemyDefenseController>(); body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null) spriteRenderer = new GameObject("Claim Wraith Visual").AddComponent<SpriteRenderer>();
            if (spriteRenderer.transform.parent == null) spriteRenderer.transform.SetParent(transform, false);
            spriteRenderer.sortingOrder = 20;
            CreateAffinityCue();
            SetAction(ClaimWraithAction.Idle);
        }

        private void Update()
        {
            if (wraith == null || wraith.CurrentAction == ClaimWraithAction.Death) { SetAction(wraith != null ? wraith.CurrentAction : ClaimWraithAction.Idle); }
            else if (wraith.CurrentAction == ClaimWraithAction.ShadowStep) SetAction(ClaimWraithAction.ShadowStep);
            else if (defense != null && defense.IsActivelyBlocking) SetAction(ClaimWraithAction.Block);
            else if (melee != null && melee.IsGuardBreakInProgress) SetAction(ClaimWraithAction.GuardBreak);
            else if (melee != null && melee.IsAttacking) SetAction(ClaimWraithAction.BasicAttack);
            else if (body != null && body.velocity.sqrMagnitude > 0.04f) SetAction(ClaimWraithAction.MoveFloat);
            else SetAction(ClaimWraithAction.Idle);

            if (frames == null || frames.Length == 0) return;
            int index = Mathf.FloorToInt(elapsed * framesPerSecond) % frames.Length;
            spriteRenderer.sprite = frames[index]; elapsed += Time.deltaTime;
        }

        public void SetAction(ClaimWraithAction requested)
        {
            if (action == requested && frames != null) return;
            action = requested; elapsed = 0f; frames = Load(Sequence(requested));
        }

        private void OnDestroy()
        {
            if (affinityMaterial != null) Destroy(affinityMaterial);
        }

        private Sprite[] Load(string sequence)
        {
            if (clips.TryGetValue(sequence, out Sprite[] result)) return result;
            result = Resources.LoadAll<Sprite>("Domain/ClaimWraith/wraith_cropped_frames_fixed/" + sequence);
            Array.Sort(result, (left, right) => string.CompareOrdinal(left.name, right.name));
            clips.Add(sequence, result); return result;
        }

        private void CreateAffinityCue()
        {
            GameObject cue = new GameObject("Echo Affinity Cue");
            cue.transform.SetParent(transform, false);
            LineRenderer line = cue.AddComponent<LineRenderer>();
            line.useWorldSpace = false; line.loop = true; line.positionCount = 10;
            line.startWidth = line.endWidth = 0.025f; line.sortingOrder = 19;
            line.startColor = line.endColor = wraith.EchoAffinity == ClaimWraithEchoAffinity.Poison
                ? new Color(0.35f, 0.9f, 0.3f, 0.55f)
                : wraith.EchoAffinity == ClaimWraithEchoAffinity.Slow
                    ? new Color(0.3f, 0.65f, 1f, 0.55f)
                    : new Color(1f, 0.8f, 0.25f, 0.55f);
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                affinityMaterial = new Material(shader);
                line.sharedMaterial = affinityMaterial;
            }
            for (int index = 0; index < line.positionCount; index++)
            {
                float angle = index * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(index, new Vector3(Mathf.Cos(angle) * 0.42f, Mathf.Sin(angle) * 0.26f, 0f));
            }
        }

        private static string Sequence(ClaimWraithAction value)
        {
            switch (value)
            {
                case ClaimWraithAction.MoveFloat: return "move_float";
                case ClaimWraithAction.BasicAttack: return "basic_attack";
                case ClaimWraithAction.Block: return "block";
                case ClaimWraithAction.GuardBreak: return "guard_break";
                case ClaimWraithAction.ShadowStep: return "shadow_step";
                case ClaimWraithAction.Hurt: return "hurt";
                case ClaimWraithAction.Death: return "death";
                default: return "idle";
            }
        }
    }
}
