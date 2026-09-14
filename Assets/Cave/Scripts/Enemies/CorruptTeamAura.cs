using System.Collections.Generic;
using Cave.Combat;
using UnityEngine;

namespace Cave.Enemies
{
    public enum CorruptTeamBuffKind
    {
        TyrantsPresence,
        BloodFrenzy,
        CommandersMark,
        AncientDiscipline,
        ArcaneResonance,
        DeathCovenant,
        UnnaturalInsight,
        TacticalCalculation,
        FieldCommunion
    }

    /// <summary>
    /// One shared, bounded aura source for authored corrupted enemies. Membership
    /// is resolved against the registered enemy set at a low cadence; no scene scan
    /// or per-frame allocation is required. Same-kind contributions use strongest-wins.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CorruptTeamAura : MonoBehaviour
    {
        private static readonly HashSet<CorruptTeamAura> ActiveAuras = new HashSet<CorruptTeamAura>();

        [SerializeField] private CorruptTeamBuffKind buffKind;
        [SerializeField, Range(5f, 8f)] private float radius = 6f;
        [SerializeField, Min(0.1f)] private float magnitude = 1f;
        [SerializeField, Range(0.1f, 1f)] private float refreshInterval = 0.35f;

        private readonly HashSet<EnemyTeamBuffState> recipients = new HashSet<EnemyTeamBuffState>();
        private readonly List<EnemyTeamBuffState> staleRecipients = new List<EnemyTeamBuffState>(16);
        private Damageable damageable;
        private float nextRefreshTime;

        public CorruptTeamBuffKind BuffKind => buffKind;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ActiveAuras.Clear();
        }

        public static CorruptTeamAura EnsureConfigured(GameObject owner)
        {
            if (owner == null || owner.GetComponent<BossPhaseController>() != null)
            {
                return null;
            }

            CorruptTeamAura aura = owner.GetComponent<CorruptTeamAura>();
            if (aura == null) aura = owner.AddComponent<CorruptTeamAura>();
            aura.ConfigureFromOwner();
            return aura;
        }

        public static void NotifyEnemyDied(EnemyTeamBuffState victim)
        {
            if (victim == null) return;
            foreach (CorruptTeamAura aura in ActiveAuras)
            {
                if (aura != null && aura.buffKind == CorruptTeamBuffKind.DeathCovenant)
                {
                    aura.ResolveDeathCovenant(victim);
                }
            }
        }

        private void Awake()
        {
            damageable = GetComponent<Damageable>();
        }

        private void OnEnable()
        {
            ActiveAuras.Add(this);
            nextRefreshTime = 0f;
        }

        private void Update()
        {
            if (damageable == null) damageable = GetComponent<Damageable>();
            if (damageable == null || damageable.CurrentHealth <= 0)
            {
                ClearRecipients();
                return;
            }

            if (Time.time < nextRefreshTime) return;
            nextRefreshTime = Time.time + refreshInterval;
            RefreshRecipients();
        }

        private void ConfigureFromOwner()
        {
            radius = 6f;
            magnitude = 1f;
            EnemyMeleeCombat melee = GetComponentInChildren<EnemyMeleeCombat>(true);
            if (GetComponent<EyeBrain>() != null)
            {
                buffKind = CorruptTeamBuffKind.UnnaturalInsight;
                radius = 7f;
            }
            else if (GetComponent<DetectiveBrain>() != null)
            {
                buffKind = CorruptTeamBuffKind.TacticalCalculation;
            }
            else if (GetComponent<GothArtilleryBrain>() != null)
            {
                buffKind = CorruptTeamBuffKind.FieldCommunion;
                radius = 7f;
            }
            else if (GetComponent<NecromancerBrain>() != null)
            {
                buffKind = CorruptTeamBuffKind.DeathCovenant;
                radius = 7f;
            }
            else if (GetComponent<WizardBrain>() != null)
            {
                buffKind = CorruptTeamBuffKind.ArcaneResonance;
                radius = 7f;
            }
            else if (GetComponent<SkeletonBrain>() != null)
            {
                buffKind = CorruptTeamBuffKind.AncientDiscipline;
            }
            else if (GetComponent<LightBanditBrain>() != null)
            {
                buffKind = CorruptTeamBuffKind.CommandersMark;
            }
            else if (melee != null && melee.IsTrollPreset)
            {
                buffKind = CorruptTeamBuffKind.BloodFrenzy;
            }
            else
            {
                buffKind = CorruptTeamBuffKind.TyrantsPresence;
            }
        }

        private void RefreshRecipients()
        {
            float radiusSquared = radius * radius;
            staleRecipients.Clear();
            foreach (EnemyTeamBuffState recipient in recipients)
            {
                staleRecipients.Add(recipient);
            }

            foreach (EnemyTeamBuffState candidate in EnemyTeamBuffState.ActiveStates)
            {
                if (candidate == null || candidate.gameObject == gameObject || !candidate.CanReceive
                    || ((Vector2)(candidate.transform.position - transform.position)).sqrMagnitude > radiusSquared
                    || !IsValidRecipient(candidate))
                {
                    continue;
                }

                candidate.SetContribution(this, buffKind, magnitude);
                recipients.Add(candidate);
                staleRecipients.Remove(candidate);
            }

            for (int index = 0; index < staleRecipients.Count; index++)
            {
                EnemyTeamBuffState stale = staleRecipients[index];
                if (stale != null) stale.RemoveContribution(this);
                recipients.Remove(stale);
            }
        }

        private bool IsValidRecipient(EnemyTeamBuffState candidate)
        {
            if (buffKind != CorruptTeamBuffKind.ArcaneResonance) return true;
            return candidate.GetComponent<WizardBrain>() != null
                || candidate.GetComponent<NecromancerBrain>() != null
                || candidate.GetComponent<DetectiveBrain>() != null
                || candidate.GetComponent<GothArtilleryBrain>() != null;
        }

        private void ResolveDeathCovenant(EnemyTeamBuffState victim)
        {
            if (!isActiveAndEnabled || victim.gameObject == gameObject || damageable == null
                || damageable.CurrentHealth <= 0) return;
            float radiusSquared = radius * radius;
            foreach (EnemyTeamBuffState ally in EnemyTeamBuffState.ActiveStates)
            {
                if (ally != null && ally != victim && ally.CanReceive
                    && ((Vector2)(ally.transform.position - transform.position)).sqrMagnitude <= radiusSquared)
                {
                    ally.RestoreBoundedHealth(1);
                }
            }
        }

        private void ClearRecipients()
        {
            foreach (EnemyTeamBuffState recipient in recipients)
            {
                if (recipient != null) recipient.RemoveContribution(this);
            }
            recipients.Clear();
        }

        private void OnDisable()
        {
            ActiveAuras.Remove(this);
            ClearRecipients();
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    public sealed class EnemyTeamBuffState : MonoBehaviour
    {
        internal static readonly HashSet<EnemyTeamBuffState> ActiveStates = new HashSet<EnemyTeamBuffState>();
        private readonly Dictionary<CorruptTeamAura, Contribution> contributions =
            new Dictionary<CorruptTeamAura, Contribution>(4);
        private Damageable damageable;
        private EnemyController groundMovement;
        private FlyingSwarmController flyingMovement;
        private WizardFlightMotor wizardMovement;
        private EnemyMeleeCombat melee;
        private EnemyStagger stagger;
        private KnockbackReceiver knockback;
        private int activeIconMask;

        private struct Contribution
        {
            public CorruptTeamBuffKind Kind;
            public float Magnitude;
        }

        public bool CanReceive => isActiveAndEnabled && damageable != null && damageable.CurrentHealth > 0;
        public int ActiveIconMask => activeIconMask;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { ActiveStates.Clear(); }

        public static EnemyTeamBuffState EnsureOn(GameObject owner)
        {
            if (owner == null) return null;
            EnemyTeamBuffState state = owner.GetComponent<EnemyTeamBuffState>();
            return state != null ? state : owner.AddComponent<EnemyTeamBuffState>();
        }

        private void Awake()
        {
            damageable = GetComponent<Damageable>();
            groundMovement = GetComponent<EnemyController>();
            flyingMovement = GetComponent<FlyingSwarmController>();
            wizardMovement = GetComponent<WizardFlightMotor>();
            melee = GetComponentInChildren<EnemyMeleeCombat>(true);
            stagger = GetComponent<EnemyStagger>();
            knockback = GetComponent<KnockbackReceiver>();
        }

        private void OnEnable()
        {
            ActiveStates.Add(this);
            damageable.Died += HandleDied;
        }

        internal void SetContribution(CorruptTeamAura source, CorruptTeamBuffKind kind, float magnitude)
        {
            contributions[source] = new Contribution { Kind = kind, Magnitude = magnitude };
            Recompute();
        }

        internal void RemoveContribution(CorruptTeamAura source)
        {
            if (contributions.Remove(source)) Recompute();
        }

        internal void RestoreBoundedHealth(int amount)
        {
            if (CanReceive) damageable.RestoreHealthResolved(Mathf.Max(0, amount));
        }

        public bool HasIcon(MobStatusIconKind kind)
        {
            return (activeIconMask & (1 << (int)kind)) != 0;
        }

        private void Recompute()
        {
            float speed = 1f;
            float attackSpeed = 1f;
            float staggerDuration = 1f;
            float knockbackResistance = 0f;
            activeIconMask = 0;
            foreach (Contribution contribution in contributions.Values)
            {
                switch (contribution.Kind)
                {
                    case CorruptTeamBuffKind.TyrantsPresence:
                        staggerDuration = Mathf.Min(staggerDuration, 0.7f);
                        knockbackResistance = Mathf.Max(knockbackResistance, 0.35f);
                        AddIcon(MobStatusIconKind.Stagger);
                        break;
                    case CorruptTeamBuffKind.BloodFrenzy:
                        attackSpeed = Mathf.Max(attackSpeed, 1.15f);
                        AddIcon(MobStatusIconKind.Frenzied);
                        break;
                    case CorruptTeamBuffKind.CommandersMark:
                        speed = Mathf.Max(speed, 1.12f);
                        AddIcon(MobStatusIconKind.StrengthBuff);
                        break;
                    case CorruptTeamBuffKind.AncientDiscipline:
                        staggerDuration = Mathf.Min(staggerDuration, 0.85f);
                        knockbackResistance = Mathf.Max(knockbackResistance, 0.15f);
                        AddIcon(MobStatusIconKind.Stagger);
                        break;
                    case CorruptTeamBuffKind.ArcaneResonance:
                        attackSpeed = Mathf.Max(attackSpeed, 1.1f);
                        AddIcon(MobStatusIconKind.ElementallyBuffed);
                        break;
                    case CorruptTeamBuffKind.DeathCovenant:
                        AddIcon(MobStatusIconKind.Regeneration);
                        break;
                    case CorruptTeamBuffKind.UnnaturalInsight:
                        speed = Mathf.Max(speed, 1.08f);
                        AddIcon(MobStatusIconKind.GazeLock);
                        break;
                    case CorruptTeamBuffKind.TacticalCalculation:
                        speed = Mathf.Max(speed, 1.08f);
                        AddIcon(MobStatusIconKind.TowerSuppression);
                        break;
                    case CorruptTeamBuffKind.FieldCommunion:
                        knockbackResistance = Mathf.Max(knockbackResistance, 0.3f);
                        AddIcon(MobStatusIconKind.PinRoot);
                        break;
                }
            }

            groundMovement?.SetTeamAuraSpeedMultiplier(speed);
            flyingMovement?.SetTeamAuraSpeedMultiplier(speed);
            wizardMovement?.SetTeamAuraSpeedMultiplier(speed);
            melee?.SetRuntimeTeamAuraAttackSpeedMultiplier(attackSpeed);
            stagger?.SetTeamAuraDurationMultiplier(staggerDuration);
            knockback?.SetTeamAuraKnockbackResistance(knockbackResistance);
        }

        private void AddIcon(MobStatusIconKind kind) { activeIconMask |= 1 << (int)kind; }

        private void HandleDied()
        {
            CorruptTeamAura.NotifyEnemyDied(this);
        }

        private void OnDisable()
        {
            ActiveStates.Remove(this);
            if (damageable != null) damageable.Died -= HandleDied;
            contributions.Clear();
            Recompute();
        }
    }
}
