using System;
using System.Collections.Generic;
using Cave.Combat;
using UnityEngine;

namespace Cave.Enemies
{
    /// <summary>
    /// Presentation-only playback for the supplied regular and General Skeleton
    /// frames. Existing AI, combat, damage, hitbox, and pooling owners remain
    /// authoritative.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    public sealed class SkeletonFrameAnimator : MonoBehaviour
    {
        private enum State { Idle, Walk, Attack, Block, Hurt, GuardBreak, Death }

        private const string RegularRoot = "Enemies/Skeleton/Regular/";
        private const string GeneralRoot = "Enemies/Skeleton/CorruptGeneral/";

        [Header("Playback")]
        [SerializeField, Min(.01f)] private float movingThreshold = .05f;
        [SerializeField, Min(1f)] private float idleFramesPerSecond = 4f;
        [SerializeField, Min(1f)] private float walkFramesPerSecond = 8f;
        [SerializeField, Min(1f)] private float actionFramesPerSecond = 10f;
        [SerializeField, Min(.01f)] private float hurtDuration = .22f;

        private readonly Dictionary<State, Sprite[]> frames = new Dictionary<State, Sprite[]>(7);
        private SpriteRenderer visualRenderer;
        private Rigidbody2D body;
        private Damageable damageable;
        private EnemyMeleeCombat melee;
        private EnemyDefenseController defense;
        private SkeletonInheritance inheritance;
        private State currentState;
        private float stateStartedAt;
        private float hurtUntil;
        private bool deathTriggered;
        private float facing = 1f;
        private Sprite displayed;

        public static SkeletonFrameAnimator EnsureOn(GameObject owner)
        {
            return owner == null ? null : owner.GetComponent<SkeletonFrameAnimator>()
                ?? owner.AddComponent<SkeletonFrameAnimator>();
        }

        private void Awake()
        {
            visualRenderer = GetComponent<SpriteRenderer>();
            body = GetComponent<Rigidbody2D>();
            damageable = GetComponent<Damageable>();
            melee = GetComponent<EnemyMeleeCombat>();
            defense = GetComponent<EnemyDefenseController>();
            inheritance = GetComponent<SkeletonInheritance>();
            LoadFrames();
            currentState = State.Idle;
            stateStartedAt = Time.time;
        }

        private void OnEnable()
        {
            if (inheritance != null)
            {
                inheritance.RankChanged += HandleRankChanged;
            }

            if (damageable != null)
            {
                damageable.DamageResolved += HandleDamageResolved;
                damageable.Died += HandleDied;
                if (damageable.CurrentHealth > 0) ResetForReuse();
            }
        }

        private void OnDisable()
        {
            if (inheritance != null)
            {
                inheritance.RankChanged -= HandleRankChanged;
            }

            if (damageable != null)
            {
                damageable.DamageResolved -= HandleDamageResolved;
                damageable.Died -= HandleDied;
            }
        }

        private void LateUpdate()
        {
            if (visualRenderer == null || frames.Count == 0) return;

            State next = ResolveState();
            if (next != currentState)
            {
                currentState = next;
                stateStartedAt = Time.time;
            }

            UpdateFacing();
            Sprite[] stateFrames;
            if (!frames.TryGetValue(currentState, out stateFrames) || stateFrames.Length == 0) return;
            bool loop = currentState == State.Idle || currentState == State.Walk || currentState == State.Block;
            int index = Mathf.FloorToInt((Time.time - stateStartedAt) * FramesPerSecond(currentState));
            index = loop ? index % stateFrames.Length : Mathf.Min(index, stateFrames.Length - 1);
            Sprite nextSprite = stateFrames[index];
            if (nextSprite != displayed)
            {
                displayed = nextSprite;
                visualRenderer.sprite = nextSprite;
            }
        }

        private void LoadFrames()
        {
            frames.Clear();
            string root = inheritance != null && inheritance.Rank == SkeletonRank.General
                ? GeneralRoot
                : RegularRoot;
            Load(root, State.Idle, "Idle");
            Load(root, State.Walk, "Walk");
            Load(root, State.Attack, "Attack");
            Load(root, State.Block, "Block");
            Load(root, State.Hurt, "Hurt");
            Load(root, State.GuardBreak, "GuardBreak");
            Load(root, State.Death, "Death");
        }

        private void Load(string root, State state, string folder)
        {
            Sprite[] loaded = Resources.LoadAll<Sprite>(root + folder);
            Array.Sort(loaded, (left, right) => string.CompareOrdinal(left.name, right.name));
            if (loaded.Length == 8) frames[state] = loaded;
        }

        private State ResolveState()
        {
            if (deathTriggered || (damageable != null && damageable.CurrentHealth <= 0)) return State.Death;
            if (Time.time < hurtUntil) return State.Hurt;
            if (melee != null && melee.IsGuardBreakInProgress) return State.GuardBreak;
            if (defense != null && defense.IsActivelyBlocking) return State.Block;
            if (melee != null && melee.IsAttacking) return State.Attack;
            return body != null && Mathf.Abs(body.velocity.x) >= movingThreshold ? State.Walk : State.Idle;
        }

        private float FramesPerSecond(State state)
        {
            if (state == State.Idle) return idleFramesPerSecond;
            if (state == State.Walk) return walkFramesPerSecond;
            return actionFramesPerSecond;
        }

        private void UpdateFacing()
        {
            if (body != null && Mathf.Abs(body.velocity.x) >= movingThreshold)
            {
                facing = Mathf.Sign(body.velocity.x);
            }

            visualRenderer.flipX = facing < 0f;
        }

        private void HandleDamageResolved(DamageContext _, bool blocked, int appliedDamage)
        {
            if (!blocked && appliedDamage > 0 && !deathTriggered
                && damageable != null && damageable.CurrentHealth > 0)
            {
                hurtUntil = Time.time + hurtDuration;
            }
        }

        private void HandleDied()
        {
            deathTriggered = true;
            stateStartedAt = Time.time;
        }

        private void HandleRankChanged(SkeletonRank _)
        {
            LoadFrames();
            displayed = null;
            stateStartedAt = Time.time;
        }

        private void ResetForReuse()
        {
            deathTriggered = false;
            hurtUntil = 0f;
            currentState = State.Idle;
            stateStartedAt = Time.time;
            displayed = null;
        }
    }
}
