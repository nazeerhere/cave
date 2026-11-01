using System.Collections;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    public sealed class StageTwoSkeletonPhase : MonoBehaviour, IGeometricAttackAuthority
    {
        [SerializeField, Min(0.1f)] private float phaseDuration = 0.45f;
        [SerializeField, Min(0.1f)] private float cooldown = 4.5f;
        private float phaseEndsAt;
        private float nextPhaseAt;
        private FrenzyGeometryStructure geometry;
        public bool IsGeometricAttackActive => Time.time < phaseEndsAt;
        public Vector2 GeometricVelocity => Vector2.up * 0.75f;

        public bool TryPhaseIncomingAttack(DamageContext context)
        {
            StageTwoCorruptionState state = GetComponent<StageTwoCorruptionState>();
            if (state == null || !state.IsStageTwoActive || Time.time < nextPhaseAt
                || !context.IsPlayerDamage || context.HasTrait(DamageTrait.Periodic)) return false;
            phaseEndsAt = Time.time + phaseDuration;
            nextPhaseAt = Time.time + cooldown;
            EnsureGeometry();
            CombatShapeEffect.Create(transform.position, CombatShape.Diamond, 0.65f,
                new Color(0.45f, 0.25f, 1f, 0.85f), phaseDuration);
            return true;
        }

        public bool TryInterruptGeometricAttack(GameObject interrupter)
        {
            if (!IsGeometricAttackActive) return false;
            phaseEndsAt = 0f;
            if (geometry != null) geometry.SetRevealed(false);
            return true;
        }

        private void EnsureGeometry()
        {
            if (geometry == null) geometry = gameObject.AddComponent<FrenzyGeometryStructure>();
            geometry.Configure(GeometricStructureKind.PhaseCoherence,
                PreGeometricPrinciple.GeometricTranslation, 2, 0.8f, false);
        }
        private void OnDisable() { phaseEndsAt = 0f; }
    }

    [DisallowMultipleComponent]
    public sealed class StageTwoBruteProjection : MonoBehaviour, IEnemyInterruptible, IGeometricAttackAuthority
    {
        private enum ProjectionKind { None, DistancePin, DistanceAttack }
        [SerializeField, Min(0.1f)] private float minimumRange = 2f;
        [SerializeField, Min(0.1f)] private float maximumRange = 7f;
        [SerializeField, Min(0.1f)] private float pinDuration = 0.8f;
        [SerializeField, Min(0.1f)] private float telegraphSeconds = 0.55f;
        [SerializeField, Min(0f)] private float cooldown = 6f;
        [SerializeField, Min(0)] private int distanceAttackDamage = 1;
        private ProjectionKind kind;
        private PlayerHealth target;
        private Coroutine routine;
        private float nextUseAt;
        private Vector2 projectedPoint;
        private FrenzyGeometryStructure geometry;
        public bool IsGeometricAttackActive => routine != null;
        public Vector2 GeometricVelocity => projectedPoint - (Vector2)transform.position;

        private void Update()
        {
            StageTwoCorruptionState state = GetComponent<StageTwoCorruptionState>();
            BruteControlAbilities baseKit = GetComponent<BruteControlAbilities>();
            if (state == null || !state.IsStageTwoActive || routine != null || Time.time < nextUseAt
                || (baseKit != null && baseKit.IsBusy)) return;
            PlayerHealth player = FindObjectOfType<PlayerHealth>();
            if (player == null) return;
            float distance = Vector2.Distance(transform.position, player.transform.position);
            if (distance < minimumRange || distance > maximumRange) return;
            target = player;
            kind = Random.value < 0.5f ? ProjectionKind.DistancePin : ProjectionKind.DistanceAttack;
            routine = StartCoroutine(kind == ProjectionKind.DistancePin ? PinRoutine() : AttackRoutine());
        }

        private IEnumerator PinRoutine()
        {
            projectedPoint = target.transform.position;
            BeginGeometry(GeometricStructureKind.SpatialManifestationTether, 3);
            CombatShapeEffect.Create(projectedPoint, CombatShape.Ring, 0.7f,
                new Color(0.8f, 0.15f, 1f, 0.85f), telegraphSeconds);
            yield return new WaitForSeconds(telegraphSeconds);
            if (!ValidTarget()) { End(); yield break; }
            target.GetComponent<PlayerController>()?.SetBrutePinMovementLock(this, true);
            float endsAt = Time.time + pinDuration;
            while (Time.time < endsAt && ValidTarget()) { projectedPoint = target.transform.position; yield return null; }
            ReleasePin(); End();
        }

        private IEnumerator AttackRoutine()
        {
            projectedPoint = target.transform.position;
            BeginGeometry(GeometricStructureKind.MeleeChargeTrajectory, 2);
            CombatShapeEffect.Create(projectedPoint, CombatShape.Chevron, 0.8f,
                new Color(1f, 0.25f, 0.75f, 0.9f), telegraphSeconds);
            yield return new WaitForSeconds(telegraphSeconds);
            if (ValidTarget() && Vector2.Distance(target.transform.position, projectedPoint) <= 1.05f)
            {
                EnemyDamageModifiers modifiers = GetComponent<EnemyDamageModifiers>();
                int damage = modifiers != null ? modifiers.ResolveDamage(distanceAttackDamage) : distanceAttackDamage;
                target.TryTakeDamage(damage, new DamageContext(gameObject, DamageTrait.Direct | DamageTrait.Melee));
            }
            End();
        }

        public bool TryBreakPinFromGuardBreak(PlayerHealth player)
        {
            if (routine == null || kind != ProjectionKind.DistancePin || target != player) return false;
            Interrupt(); return true;
        }
        public bool TryInterruptGeometricAttack(GameObject interrupter) { if (routine == null) return false; Interrupt(); return true; }
        public void Interrupt() { if (routine != null) StopCoroutine(routine); ReleasePin(); End(); }
        private bool ValidTarget() => target != null && target.gameObject.activeInHierarchy;
        private void ReleasePin() { if (target != null) target.GetComponent<PlayerController>()?.SetBrutePinMovementLock(this, false); }
        private void BeginGeometry(GeometricStructureKind geometryKind, int resistance)
        { geometry = gameObject.AddComponent<FrenzyGeometryStructure>(); geometry.Configure(geometryKind, PreGeometricPrinciple.SpatialProjection, resistance, 1f, false); }
        private void End() { if (geometry != null) Destroy(geometry); geometry = null; routine = null; target = null; kind = ProjectionKind.None; nextUseAt = Time.time + cooldown; }
        private void OnDisable() { Interrupt(); }
    }

    [DisallowMultipleComponent]
    public sealed class StageTwoWizardInvocation : MonoBehaviour, IEnemyInterruptible, IGeometricAttackAuthority
    {
        private static readonly HashSet<StageTwoWizardInvocation> Active = new HashSet<StageTwoWizardInvocation>();
        [SerializeField, Min(0.1f)] private float invocationRadius = 7f;
        [SerializeField, Min(0.1f)] private float castSeconds = 1.2f;
        [SerializeField, Min(0.1f)] private float cooldown = 12f;
        [SerializeField, Range(0f, 1f)] private float areaHealFraction = 0.08f;
        [SerializeField, Range(0f, 1f)] private float empowermentBonus = 0.15f;
        [SerializeField, Min(0.1f)] private float empowermentSeconds = 6f;
        private Coroutine cast;
        private float nextCastAt;
        private FrenzyGeometryStructure geometry;
        public bool IsGeometricAttackActive => cast != null;
        public Vector2 GeometricVelocity => Vector2.up;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetStatics() { Active.Clear(); }
        private void OnEnable() { Active.Add(this); }
        private void Update()
        {
            if (cast != null || Time.time < nextCastAt || !IsValidSource()) return;
            int nearby = 0;
            foreach (StageTwoWizardInvocation wizard in Active)
                if (wizard != null && wizard != this && wizard.IsValidSource()
                    && Vector2.Distance(transform.position, wizard.transform.position) <= invocationRadius) nearby++;
            if (nearby > 0) cast = StartCoroutine(InvokeRoutine());
        }
        private IEnumerator InvokeRoutine()
        {
            geometry = gameObject.AddComponent<FrenzyGeometryStructure>();
            geometry.Configure(GeometricStructureKind.SpellInvocationAnchor,
                PreGeometricPrinciple.DistributedInteraction, 3, 1.2f, false);
            CombatShapeEffect.Create(transform.position, CombatShape.Ring, invocationRadius,
                new Color(0.3f, 0.7f, 1f, 0.45f), castSeconds, 0f, 0.06f);
            yield return new WaitForSeconds(castSeconds);
            int contributors = 1;
            foreach (StageTwoWizardInvocation wizard in Active)
                if (wizard != null && wizard != this && wizard.IsValidSource()
                    && Vector2.Distance(transform.position, wizard.transform.position) <= invocationRadius) contributors++;
            if (contributors >= 2)
            {
                foreach (EnemyTeamBuffState ally in EnemyTeamBuffState.ActiveStates)
                {
                    if (ally == null || !ally.CanReceive || Vector2.Distance(transform.position, ally.transform.position) > invocationRadius) continue;
                    Damageable health = ally.GetComponent<Damageable>();
                    health.RestoreHealthResolved(Mathf.Max(1, Mathf.CeilToInt(health.MaximumHealth * areaHealFraction)));
                    EnemyDamageModifiers modifiers = ally.GetComponent<EnemyDamageModifiers>();
                    if (modifiers == null) modifiers = ally.gameObject.AddComponent<EnemyDamageModifiers>();
                    modifiers.ApplyModifier(EnemyDamageModifierType.WizardBuff, empowermentBonus,
                        empowermentSeconds, gameObject);
                }
            }
            EndCast();
        }
        private bool IsValidSource() { StageTwoCorruptionState state = GetComponent<StageTwoCorruptionState>(); Damageable health = GetComponent<Damageable>(); return state != null && state.IsStageTwoActive && health != null && health.CurrentHealth > 0; }
        public bool TryInterruptGeometricAttack(GameObject interrupter) { if (cast == null) return false; Interrupt(); return true; }
        public void Interrupt() { if (cast != null) StopCoroutine(cast); EndCast(); }
        private void EndCast() { if (geometry != null) Destroy(geometry); geometry = null; cast = null; nextCastAt = Time.time + cooldown; }
        private void OnDisable() { Active.Remove(this); Interrupt(); }
    }
}
