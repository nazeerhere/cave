using System.Collections.Generic;
using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    public enum WizardElement
    {
        None,
        Fire,
        Frost,
        Magic
    }

    [DisallowMultipleComponent]
    public sealed class EnemyElementalEmpowerment : MonoBehaviour
    {
        [Header("Current Empowerment (Read Only)")]
        [SerializeField] private WizardElement currentElement;
        [SerializeField, Min(0f)] private float remainingDuration;
        [SerializeField] private CorruptionElement corruptionElement;
        [SerializeField] private bool lastAttackTriggeredReaction;

        private float expiresAt;
        private float frostMovementMultiplier = 0.85f;
        private float frostDuration = 1.5f;
        private EnemyCorruptionLifecycle corruptionOwner;
        private readonly Dictionary<int, SourceEmpowerment> sourceEmpowerments =
            new Dictionary<int, SourceEmpowerment>();
        private readonly List<int> expiredSourceIds = new List<int>();

        private struct SourceEmpowerment
        {
            public GameObject Source;
            public WizardElement Element;
            public float ExpiresAt;
            public float FrostMovementMultiplier;
            public float FrostDuration;
        }

        public WizardElement CurrentElement
        {
            get
            {
                RefreshExpirations();
                ResolveAggregateElement();
                return currentElement;
            }
        }

        public bool IsEmpowered => CurrentElement != WizardElement.None;

        public void SetCorruptionElement(
            CorruptionElement element,
            EnemyCorruptionLifecycle owner)
        {
            corruptionElement = element;
            corruptionOwner = owner;
        }

        private void Update()
        {
            RefreshExpirations();
            ResolveAggregateElement();
            remainingDuration = Mathf.Max(0f, expiresAt - Time.time);
        }

        public bool Apply(
            WizardElement element,
            float additiveDamageBonus,
            float duration,
            float requestedFrostMovementMultiplier,
            float requestedFrostDuration,
            GameObject source,
            GameObject visualPrefab)
        {
            RefreshExpirations();
            if (element == WizardElement.None || duration <= 0f || source == null)
            {
                return false;
            }

            EnemyWorldStatusIndicators.EnsureOn(gameObject);

            int sourceId = source.GetInstanceID();
            SourceEmpowerment entry = new SourceEmpowerment
            {
                Source = source,
                Element = element,
                ExpiresAt = Time.time + duration,
                FrostMovementMultiplier = Mathf.Clamp(
                    requestedFrostMovementMultiplier,
                    0.1f,
                    1f),
                FrostDuration = Mathf.Max(0.1f, requestedFrostDuration)
            };
            sourceEmpowerments[sourceId] = entry;
            ResolveAggregateElement();

            EnemyDamageModifiers modifiers = GetComponent<EnemyDamageModifiers>();
            if (modifiers == null)
            {
                modifiers = gameObject.AddComponent<EnemyDamageModifiers>();
            }

            modifiers.ApplyModifier(
                EnemyDamageModifierType.WizardBuff,
                additiveDamageBonus,
                duration,
                source);
            Damageable damageable = GetComponent<Damageable>();
            if (damageable != null)
            {
                EnemyBuffVfxAttachment.Show(
                    damageable,
                    EnemyDamageModifierType.WizardBuff,
                    visualPrefab,
                    Vector2.zero,
                    1f,
                    0.05f,
                    duration,
                    0.5f);
            }

            return true;
        }

        public bool IsEmpoweredBy(GameObject source)
        {
            RefreshExpirations();
            return source != null && sourceEmpowerments.ContainsKey(source.GetInstanceID());
        }

        public void RemoveFrom(GameObject source)
        {
            if (source == null || !sourceEmpowerments.Remove(source.GetInstanceID()))
            {
                return;
            }

            GetComponent<EnemyDamageModifiers>()?.RemoveModifier(
                EnemyDamageModifierType.WizardBuff,
                source);
            ResolveAggregateElement();
        }

        public void ApplyOnHit(PlayerHealth player)
        {
            if (player == null)
            {
                return;
            }

            RefreshExpirations();
            bool hasFire = corruptionElement == CorruptionElement.Fire;
            bool hasFrost = corruptionElement == CorruptionElement.Frost;
            float resolvedFrostMultiplier = 1f;
            float resolvedFrostDuration = 0f;
            foreach (SourceEmpowerment entry in sourceEmpowerments.Values)
            {
                hasFire |= entry.Element == WizardElement.Fire;
                if (entry.Element == WizardElement.Frost)
                {
                    hasFrost = true;
                    resolvedFrostMultiplier = Mathf.Min(
                        resolvedFrostMultiplier,
                        entry.FrostMovementMultiplier);
                    resolvedFrostDuration = Mathf.Max(
                        resolvedFrostDuration,
                        entry.FrostDuration);
                }
            }

            lastAttackTriggeredReaction = hasFire && hasFrost;
            if (lastAttackTriggeredReaction)
            {
                PlayerCurseController curses = PlayerCurseController.Active;
                int reactionDamage = curses != null ? curses.ElementalReactionDamage : 2;
                float reactionForce = curses != null ? curses.ElementalReactionForce : 6f;
                float reactionRadius = curses != null ? curses.ElementalReactionRadius : 2f;
                GameObject reactionVfx = curses != null
                    ? curses.ElementalReactionVfxPrefab
                    : null;
                Vector2 reactionOrigin = player.transform.position;
                foreach (PlayerHealth affectedPlayer in FindObjectsOfType<PlayerHealth>())
                {
                    if (affectedPlayer != null
                        && ((Vector2)affectedPlayer.transform.position - reactionOrigin).sqrMagnitude
                            <= reactionRadius * reactionRadius)
                    {
                        affectedPlayer.ApplyElementalReactionDamage(
                            reactionDamage,
                            gameObject,
                            reactionOrigin,
                            reactionForce);
                    }
                }
                if (reactionVfx != null)
                {
                    GameObject spawned = Instantiate(
                        reactionVfx,
                        reactionOrigin,
                        Quaternion.identity);
                    Destroy(spawned, 3f);
                }
                else
                {
                    AreaPulseEffect.Create(
                        reactionOrigin,
                        reactionRadius,
                        new Color(0.7f, 0.25f, 1f, 0.95f),
                        0.45f);
                }

                ClearWizardEmpowerments();
                corruptionElement = CorruptionElement.None;
                if (corruptionOwner != null)
                {
                    corruptionOwner.LastFireFrostReaction = true;
                }

                return;
            }

            if (!hasFrost)
            {
                return;
            }

            PlayerSlowStatus slow = player.GetComponent<PlayerSlowStatus>();
            if (slow == null)
            {
                slow = player.gameObject.AddComponent<PlayerSlowStatus>();
            }

            if (corruptionElement == CorruptionElement.Frost)
            {
                resolvedFrostMultiplier = Mathf.Min(
                    resolvedFrostMultiplier,
                    frostMovementMultiplier);
                resolvedFrostDuration = Mathf.Max(resolvedFrostDuration, frostDuration);
            }

            slow.ApplySlow(
                resolvedFrostMultiplier < 1f ? resolvedFrostMultiplier : frostMovementMultiplier,
                resolvedFrostDuration > 0f ? resolvedFrostDuration : frostDuration);
        }

        private void RefreshExpirations()
        {
            expiredSourceIds.Clear();
            foreach (KeyValuePair<int, SourceEmpowerment> pair in sourceEmpowerments)
            {
                if (pair.Value.Source == null || Time.time >= pair.Value.ExpiresAt)
                {
                    expiredSourceIds.Add(pair.Key);
                }
            }

            foreach (int sourceId in expiredSourceIds)
            {
                if (sourceEmpowerments.TryGetValue(sourceId, out SourceEmpowerment expired))
                {
                    GetComponent<EnemyDamageModifiers>()?.RemoveModifier(
                        EnemyDamageModifierType.WizardBuff,
                        expired.Source);
                }

                sourceEmpowerments.Remove(sourceId);
            }
        }

        private void ResolveAggregateElement()
        {
            bool hasFire = false;
            bool hasFrost = false;
            expiresAt = 0f;
            foreach (SourceEmpowerment entry in sourceEmpowerments.Values)
            {
                hasFire |= entry.Element == WizardElement.Fire;
                hasFrost |= entry.Element == WizardElement.Frost;
                expiresAt = Mathf.Max(expiresAt, entry.ExpiresAt);
            }

            currentElement = hasFire && hasFrost
                ? WizardElement.Magic
                : hasFire
                    ? WizardElement.Fire
                    : hasFrost
                        ? WizardElement.Frost
                        : WizardElement.None;
        }

        private void ClearWizardEmpowerments()
        {
            EnemyDamageModifiers modifiers = GetComponent<EnemyDamageModifiers>();
            foreach (SourceEmpowerment entry in sourceEmpowerments.Values)
            {
                modifiers?.RemoveModifier(EnemyDamageModifierType.WizardBuff, entry.Source);
            }

            sourceEmpowerments.Clear();
            currentElement = WizardElement.None;
            expiresAt = 0f;
            remainingDuration = 0f;
        }

        private void OnDisable()
        {
            ClearWizardEmpowerments();
            corruptionElement = CorruptionElement.None;
            lastAttackTriggeredReaction = false;
        }
    }
}
