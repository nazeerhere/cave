using System.Collections;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(Damageable))]
    public sealed class EyeBrain : MonoBehaviour, IEnemySkillEvolutionReceiver
    {
        private enum EyeState { Orbit, GazeSetup, GazeLocked, Dive, Recover, Sacrifice, Possess }
        private enum EyeRole { Watcher, Harasser }

        private static readonly HashSet<EyeBrain> ActiveEyes = new HashSet<EyeBrain>();
        private static readonly HashSet<Damageable> SacrificeClaims = new HashSet<Damageable>();
        private static readonly HashSet<Damageable> PossessionClaims = new HashSet<Damageable>();

        [Header("Orbit / Watcher")]
        [SerializeField, Min(0.1f)] private float orbitDistance = 3f;
        [SerializeField, Min(0f)] private float orbitHeight = 1.5f;
        [SerializeField, Min(0.1f)] private float orbitSpeed = 4f;
        [SerializeField, Min(0.1f)] private float watcherRadius = 8f;
        [SerializeField, Min(0.1f)] private float sharedLocationMemory = 1.5f;
        [SerializeField] private LayerMask lineOfSightMask = ~0;
        [SerializeField, Min(0.1f)] private float pairedMinimumSeparation = 2.25f;
        [SerializeField, Min(0.1f)] private float broadcastRetreatSpeedThreshold = 1.2f;

        [Header("Gaze / Frenzy")]
        [SerializeField, Min(0.1f)] private float gazeRange = 6f;
        [SerializeField, Min(0.05f)] private float gazeSetupDuration = 0.6f;
        [SerializeField, Min(0.1f)] private float gazeLockDuration = 2.5f;
        [SerializeField, Min(0f)] private float gazeReentryDelay = 0.9f;
        [SerializeField, Min(0.1f)] private float gazeCancelDangerRadius = 2.25f;
        [SerializeField, Min(0.1f)] private float gazeReacquireRadius = 3f;
        [SerializeField, Range(-1f, 1f)] private float turnedAwayDotThreshold = -0.2f;
        [SerializeField, Min(0.1f)] private float frenzyDuration = 4f;
        [SerializeField, Min(1f)] private float frenzyMoveMultiplier = 1.3f;
        [SerializeField, Min(1f)] private float frenzyCadenceMultiplier = 1.35f;

        [Header("Dive")]
        [SerializeField, Range(0f, 1f)] private float lowStaminaFraction = 0.25f;
        [SerializeField, Min(0.1f)] private float diveCooldown = 2.8f;
        [SerializeField, Min(0.1f)] private float diveSpeed = 8f;
        [SerializeField, Min(0.05f)] private float diveDuration = 0.45f;
        [SerializeField, Min(0.05f)] private float diveRecovery = 0.35f;
        [SerializeField, Min(1)] private int diveDamage = 1;

        [Header("Normal Hover Altitude")]
        [SerializeField, Min(0.1f)] private float minimumNormalHoverHeight = 1.6f;
        [SerializeField, Min(0.1f)] private float preferredNormalHoverHeight = 2.1f;
        [SerializeField, Min(0.1f)] private float hoverGroundProbeHeight = 4f;
        [SerializeField, Min(0.1f)] private float hoverGroundProbeDistance = 12f;
        [SerializeField] private LayerMask hoverGroundLayers = ~0;

        [Header("Sacrifice")]
        [SerializeField, Min(0.1f)] private float rescueRadius = 4f;
        [SerializeField, Min(0.1f)] private float playerThreatRange = 2.2f;
        [SerializeField, Range(0.01f, 1f)] private float endangeredHealthFraction = 0.25f;
        [SerializeField, Min(0.05f)] private float sacrificeWindup = 0.35f;
        [SerializeField, Min(0.1f)] private float sacrificeSpeed = 10f;
        [SerializeField, Min(0.1f)] private float sacrificeImpactRadius = 2f;
        [SerializeField, Min(0)] private int sacrificeDamage = 1;
        [SerializeField, Min(0f)] private float sacrificeKnockback = 12f;

        [Header("Possession")]
        [SerializeField] private EnemyEvolutionStage possessionUnlockStage = EnemyEvolutionStage.EvolutionOne;
        [SerializeField, Min(0.1f)] private float possessionRange = 5f;
        [SerializeField, Min(0.1f)] private float possessionSpeed = 9f;
        [SerializeField, Range(0.01f, 0.5f)] private float possessionHealthBonusFraction = 0.12f;
        [SerializeField] private GameObject possessionMarkerPrefab;
        [SerializeField] private Vector2 possessionMarkerOffset = new Vector2(0f, 0.45f);

        [Header("Optional Presentation")]
        [SerializeField] private GameObject gazeVfxPrefab;
        [SerializeField] private GameObject sacrificeVfxPrefab;
        [SerializeField] private GameObject possessionVfxPrefab;

        [Header("Runtime (Read Only)")]
        [SerializeField] private EyeState state;
        [SerializeField] private bool hasLineOfSight;
        [SerializeField] private bool isFrenzied;
        [SerializeField] private EyeRole currentRole = EyeRole.Watcher;

        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private Damageable self;
        private FlyingSwarmController legacyFlight;
        private PlayerHealth player;
        private PlayerAimDirection playerAim;
        private SpinSwordAttack playerStamina;
        private LineRenderer gazeLine;
        private float stateEndsAt;
        private float nextDiveAt;
        private float frenzyEndsAt;
        private float nextGazeAllowedAt;
        private EnemyEvolutionStage evolutionStage;
        private Damageable claimedAlly;
        private Damageable possessionHost;
        private bool diveHit;
        private bool gazeSuppressedByDanger;

        public bool IsMeaningfullyEngaged => player != null
            && hasLineOfSight
            && state != EyeState.Recover;
        public bool IsGazeActive => state == EyeState.GazeSetup || state == EyeState.GazeLocked;
        public bool IsFrenzied => isFrenzied;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            self = GetComponent<Damageable>();
            legacyFlight = GetComponent<FlyingSwarmController>();
            if (legacyFlight != null)
            {
                legacyFlight.enabled = false;
            }

            body.gravityScale = 0f;
            body.freezeRotation = true;
            EnsureGazeLine();
            EnemyWorldStatusIndicators.EnsureOn(gameObject);
        }

        private void OnEnable()
        {
            ActiveEyes.Add(this);
            self.Died += HandleDeath;
            self.DamageResolved += HandleDamageResolved;
            state = EyeState.Orbit;
            nextDiveAt = Time.time + diveCooldown;
            nextGazeAllowedAt = Time.time;
            gazeSuppressedByDanger = false;
        }

        private void OnDisable()
        {
            ActiveEyes.Remove(this);
            SacrificeClaims.Remove(claimedAlly);
            PossessionClaims.Remove(possessionHost);
            EyeWatcherNetwork.Remove(this);
            if (self != null)
            {
                self.Died -= HandleDeath;
                self.DamageResolved -= HandleDamageResolved;
            }

            if (gazeLine != null)
            {
                gazeLine.enabled = false;
            }
        }

        private void FixedUpdate()
        {
            EnsurePlayer();
            if (player == null || self.CurrentHealth <= 0)
            {
                body.velocity = Vector2.zero;
                return;
            }

            hasLineOfSight = HasLineOfSight(player.transform.position);
            if (hasLineOfSight)
            {
                EyeWatcherNetwork.Broadcast(
                    this,
                    player.transform.position,
                    watcherRadius,
                    sharedLocationMemory,
                    BuildBattlefieldSignals());
            }

            isFrenzied = Time.time < frenzyEndsAt;
            switch (state)
            {
                case EyeState.GazeSetup:
                    HoldAtNormalHoverFloor();
                    if (ShouldCancelGaze())
                    {
                        CancelGaze();
                    }
                    else if (Time.time >= stateEndsAt)
                    {
                        state = EyeState.GazeLocked;
                        stateEndsAt = Time.time + gazeLockDuration;
                    }
                    break;
                case EyeState.GazeLocked:
                    HoldAtNormalHoverFloor();
                    if (ShouldCancelGaze())
                    {
                        CancelGaze();
                    }
                    else if (IsPlayerDeliberatelyFacingAway())
                    {
                        frenzyEndsAt = Time.time + frenzyDuration;
                        ResolveGazeIntoFrenzy();
                    }
                    else if (Time.time >= stateEndsAt)
                    {
                        ResolveGazeNormally();
                    }
                    break;
                case EyeState.Dive:
                    body.velocity = body.velocity.normalized * diveSpeed * (isFrenzied ? frenzyMoveMultiplier : 1f);
                    if (Time.time >= stateEndsAt)
                    {
                        state = EyeState.Recover;
                        stateEndsAt = Time.time + diveRecovery;
                    }
                    break;
                case EyeState.Recover:
                    Orbit(0.6f);
                    if (Time.time >= stateEndsAt)
                    {
                        state = EyeState.Orbit;
                    }
                    break;
                case EyeState.Sacrifice:
                    UpdateSacrifice();
                    break;
                case EyeState.Possess:
                    UpdatePossession();
                    break;
                default:
                    UpdateOrbitDecision();
                    break;
            }

            UpdateGazeLine();
        }

        private void UpdateOrbitDecision()
        {
            currentRole = ResolveRole();
            UpdateGazeDangerHysteresis();
            if (TryClaimSacrifice())
            {
                return;
            }

            if (TryClaimPossession())
            {
                return;
            }

            if (!gazeSuppressedByDanger && CanBeginGaze())
            {
                state = EyeState.GazeSetup;
                stateEndsAt = Time.time + gazeSetupDuration;
                Spawn(gazeVfxPrefab, transform.position);
                return;
            }

            if (gazeSuppressedByDanger)
            {
                RetreatFromPlayer();
            }
            else
            {
                Orbit(1f);
            }
            if (Time.time >= nextDiveAt && hasLineOfSight && CanBeginDive() && ShouldDive())
            {
                Vector2 direction = ((Vector2)player.transform.position - body.position).normalized;
                body.velocity = direction.sqrMagnitude > 0.001f ? direction * diveSpeed : Vector2.down * diveSpeed;
                diveHit = false;
                state = EyeState.Dive;
                stateEndsAt = Time.time + diveDuration;
                nextDiveAt = Time.time + diveCooldown / (isFrenzied ? frenzyCadenceMultiplier : 1f);
            }
        }

        private void Orbit(float speedScale)
        {
            float side = ResolveOrbitSide();
            float verticalWave = Mathf.Sin(Time.time * 1.7f + Mathf.Abs(GetInstanceID())) * 0.45f;
            Vector2 desired = (Vector2)player.transform.position
                + new Vector2(side * orbitDistance,
                    Mathf.Max(orbitHeight + verticalWave, preferredNormalHoverHeight));
            desired.y = Mathf.Max(desired.y, ResolveMinimumNormalHoverY());
            Vector2 velocity = (desired - body.position).normalized
                * orbitSpeed * speedScale * (isFrenzied ? frenzyMoveMultiplier : 1f);
            EyeBrain nearest = FindNearestEye();
            if (nearest != null)
            {
                Vector2 separation = body.position - nearest.body.position;
                if (separation.sqrMagnitude > 0.001f
                    && separation.sqrMagnitude < pairedMinimumSeparation * pairedMinimumSeparation)
                {
                    velocity += separation.normalized * orbitSpeed * 0.55f;
                }
            }
            body.velocity = Vector2.Lerp(body.velocity, velocity, 0.2f);
        }

        private float ResolveOrbitSide()
        {
            if (currentRole == EyeRole.Harasser)
            {
                return -ResolveWatcherSide();
            }

            return ResolveWatcherSide();
        }

        private float ResolveWatcherSide()
        {
            int smallerIds = 0;
            foreach (EyeBrain other in ActiveEyes)
            {
                if (other != null && other != this && other.GetInstanceID() < GetInstanceID())
                {
                    smallerIds++;
                }
            }

            return smallerIds % 2 == 0 ? 1f : -1f;
        }

        private bool ShouldDive()
        {
            bool exhausted = playerStamina != null
                && playerStamina.CurrentStamina <= playerStamina.MaximumStamina * lowStaminaFraction;
            float chance = isFrenzied ? 0.78f : exhausted ? 0.65f : 0.22f;
            if (currentRole == EyeRole.Watcher && CountActiveEyes() > 1)
            {
                chance *= exhausted ? 0.45f : 0.15f;
            }
            return Random.value <= chance;
        }

        private bool CanBeginGaze()
        {
            if (Time.time < nextGazeAllowedAt
                || !hasLineOfSight
                || Vector2.Distance(transform.position, player.transform.position) > gazeRange
                || IsPlayerInGazeDanger())
            {
                return false;
            }

            // With a pair, the Watcher owns ordinary Gaze while the Harasser
            // remains available for pressure. A Harasser may take over only if
            // no Watcher is presently active.
            if (currentRole == EyeRole.Harasser && HasActiveWatcher())
            {
                return false;
            }

            foreach (EyeBrain other in ActiveEyes)
            {
                if (other != null && other != this
                    && (other.state == EyeState.GazeSetup || other.state == EyeState.GazeLocked))
                {
                    return false;
                }
            }

            return Random.value < 0.08f;
        }

        private bool CanBeginDive()
        {
            foreach (EyeBrain other in ActiveEyes)
            {
                if (other != null && other != this && other.state == EyeState.Dive)
                {
                    return false;
                }
            }

            return true;
        }

        private EyeRole ResolveRole()
        {
            int lowerIdCount = 0;
            foreach (EyeBrain other in ActiveEyes)
            {
                if (other != null && other != this && other.GetInstanceID() < GetInstanceID())
                {
                    lowerIdCount++;
                }
            }

            return lowerIdCount == 0 ? EyeRole.Watcher : EyeRole.Harasser;
        }

        private int CountActiveEyes()
        {
            int count = 0;
            foreach (EyeBrain eye in ActiveEyes)
            {
                if (eye != null && eye.player == player)
                {
                    count++;
                }
            }

            return count;
        }

        private bool HasActiveWatcher()
        {
            foreach (EyeBrain other in ActiveEyes)
            {
                if (other != null && other != this && other.player == player
                    && other.ResolveRole() == EyeRole.Watcher
                    && (other.IsGazeActive || other.hasLineOfSight))
                {
                    return true;
                }
            }

            return false;
        }

        private EyeBrain FindNearestEye()
        {
            EyeBrain nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (EyeBrain other in ActiveEyes)
            {
                if (other == null || other == this || other.player != player)
                {
                    continue;
                }

                float distance = ((Vector2)other.transform.position - body.position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = other;
                }
            }

            return nearest;
        }

        private BattlefieldSignal BuildBattlefieldSignals()
        {
            BattlefieldSignal signals = BattlefieldContext.BuildReliablePlayerSignals(
                player,
                transform.position,
                broadcastRetreatSpeedThreshold,
                lowStaminaFraction);
            BattlefieldSnapshot nearby = BattlefieldContext.Read(this, watcherRadius);
            if (nearby.HasTag(BattlefieldArchetypeTag.Frontline))
            {
                signals |= BattlefieldSignal.FrontlineEngaged;
            }

            if (nearby.HasTag(BattlefieldArchetypeTag.Support))
            {
                signals |= BattlefieldSignal.SupportAvailable;
            }

            return signals;
        }

        private bool IsPlayerDeliberatelyFacingAway()
        {
            if (playerAim == null)
            {
                return false;
            }

            Vector2 towardEye = ((Vector2)transform.position - (Vector2)player.transform.position).normalized;
            return Vector2.Dot(playerAim.FacingDirection, towardEye) <= turnedAwayDotThreshold;
        }

        private bool TryClaimSacrifice()
        {
            PlayerAttackState attackState = player.GetComponent<PlayerAttackState>();
            if (attackState == null || !attackState.IsActivelyAttacking)
            {
                return false;
            }

            Damageable best = null;
            foreach (Damageable ally in Object.FindObjectsOfType<Damageable>())
            {
                if (!IsEligibleAlly(ally) || SacrificeClaims.Contains(ally)
                    || Vector2.Distance(transform.position, ally.transform.position) > rescueRadius
                    || Vector2.Distance(player.transform.position, ally.transform.position) > playerThreatRange
                    || ally.CurrentHealth > ally.MaximumHealth * endangeredHealthFraction)
                {
                    continue;
                }

                best = ally;
                break;
            }

            if (best == null)
            {
                return false;
            }

            claimedAlly = best;
            SacrificeClaims.Add(best);
            state = EyeState.Sacrifice;
            stateEndsAt = Time.time + sacrificeWindup;
            HoldPosition();
            Spawn(sacrificeVfxPrefab, transform.position);
            return true;
        }

        private void UpdateSacrifice()
        {
            if (claimedAlly == null || claimedAlly.CurrentHealth <= 0)
            {
                SacrificeClaims.Remove(claimedAlly);
                claimedAlly = null;
                state = EyeState.Orbit;
                return;
            }

            if (Time.time < stateEndsAt)
            {
                HoldPosition();
                return;
            }

            Vector2 destination = claimedAlly.transform.position;
            body.velocity = (destination - body.position).normalized * sacrificeSpeed;
            if (Vector2.Distance(body.position, destination) <= 0.4f)
            {
                Vector2 away = ((Vector2)player.transform.position - destination).normalized;
                player.GetComponent<PlayerController>()?.ApplyExternalKnockback(away * sacrificeKnockback, 0.22f);
                player.TryTakeDamage(sacrificeDamage, new DamageContext(gameObject, DamageTrait.AreaOfEffect));
                Spawn(sacrificeVfxPrefab, destination);
                SacrificeClaims.Remove(claimedAlly);
                claimedAlly = null;
                self.TakeDamage(self.CurrentHealth, new DamageContext(gameObject, DamageTrait.AreaOfEffect));
            }
        }

        private bool TryClaimPossession()
        {
            if (evolutionStage < possessionUnlockStage || Random.value > 0.025f)
            {
                return false;
            }

            foreach (Damageable ally in Object.FindObjectsOfType<Damageable>())
            {
                if (!IsEligiblePossessionHost(ally) || PossessionClaims.Contains(ally)
                    || Vector2.Distance(transform.position, ally.transform.position) > possessionRange)
                {
                    continue;
                }

                possessionHost = ally;
                PossessionClaims.Add(ally);
                state = EyeState.Possess;
                Spawn(possessionVfxPrefab, transform.position);
                return true;
            }

            return false;
        }

        private void UpdatePossession()
        {
            if (possessionHost == null || possessionHost.CurrentHealth <= 0)
            {
                PossessionClaims.Remove(possessionHost);
                possessionHost = null;
                state = EyeState.Orbit;
                return;
            }

            body.velocity = ((Vector2)possessionHost.transform.position - body.position).normalized * possessionSpeed;
            if (Vector2.Distance(body.position, possessionHost.transform.position) > 0.38f)
            {
                return;
            }

            EyePossessedHost host = possessionHost.GetComponent<EyePossessedHost>();
            if (host == null)
            {
                host = possessionHost.gameObject.AddComponent<EyePossessedHost>();
            }

            host.Initialize(possessionHealthBonusFraction, watcherRadius, sharedLocationMemory,
                possessionMarkerPrefab, possessionMarkerOffset);

            PossessionClaims.Remove(possessionHost);
            possessionHost = null;
            self.TakeDamage(self.CurrentHealth, new DamageContext(gameObject, DamageTrait.Direct));
        }

        private bool IsEligibleAlly(Damageable candidate)
        {
            return candidate != null && candidate != self && candidate.CurrentHealth > 0
                && HostileMobQuery.IsRealHostile(candidate)
                && candidate.GetComponent<EyeBrain>() == null;
        }

        private bool IsEligiblePossessionHost(Damageable candidate)
        {
            return IsEligibleAlly(candidate)
                && candidate.GetComponent<EyePossessedHost>() == null
                && candidate.GetComponent<DetectiveTower>() == null;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (state != EyeState.Dive || diveHit)
            {
                return;
            }

            PlayerHealth hit = other.GetComponentInParent<PlayerHealth>();
            if (hit == null)
            {
                return;
            }

            diveHit = true;
            hit.TryTakeDamage(diveDamage, new DamageContext(gameObject, DamageTrait.Melee));
            state = EyeState.Recover;
            stateEndsAt = Time.time + diveRecovery;
        }

        private void EnsurePlayer()
        {
            if (player != null && player.gameObject.activeInHierarchy)
            {
                return;
            }

            player = FindObjectOfType<PlayerHealth>();
            playerAim = player != null ? player.GetComponent<PlayerAimDirection>() : null;
            playerStamina = player != null ? player.GetComponent<SpinSwordAttack>() : null;
        }

        private bool HasLineOfSight(Vector2 destination)
        {
            RaycastHit2D[] hits = Physics2D.LinecastAll(transform.position, destination, lineOfSightMask);
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null || hit.collider == bodyCollider
                    || hit.collider.GetComponentInParent<PlayerHealth>() != null
                    || hit.collider.GetComponentInParent<EnemyController>() != null
                    || hit.collider.GetComponentInParent<EyeBrain>() != null)
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private void EnsureGazeLine()
        {
            GameObject lineObject = new GameObject("Eye Gaze Lock");
            lineObject.transform.SetParent(transform, false);
            gazeLine = lineObject.AddComponent<LineRenderer>();
            gazeLine.material = new Material(Shader.Find("Sprites/Default"));
            gazeLine.positionCount = 2;
            gazeLine.startWidth = 0.035f;
            gazeLine.endWidth = 0.035f;
            gazeLine.startColor = new Color(0.72f, 0.22f, 1f, 0.85f);
            gazeLine.endColor = gazeLine.startColor;
            gazeLine.sortingOrder = 6;
            gazeLine.enabled = false;
        }

        private void UpdateGazeLine()
        {
            if (gazeLine == null)
            {
                return;
            }

            bool visible = player != null && (state == EyeState.GazeSetup || state == EyeState.GazeLocked);
            gazeLine.enabled = visible;
            if (visible)
            {
                gazeLine.SetPosition(0, transform.position);
                gazeLine.SetPosition(1, player.transform.position);
            }
        }

        private void HoldPosition()
        {
            body.velocity = Vector2.zero;
        }

        private void HoldAtNormalHoverFloor()
        {
            float minimumY = ResolveMinimumNormalHoverY();
            if (transform.position.y < minimumY)
            {
                body.velocity = Vector2.up * orbitSpeed;
                return;
            }

            HoldPosition();
        }

        private bool ShouldCancelGaze()
        {
            UpdateGazeDangerHysteresis();
            return gazeSuppressedByDanger || !hasLineOfSight;
        }

        private void UpdateGazeDangerHysteresis()
        {
            if (player == null)
            {
                return;
            }

            float distance = Vector2.Distance(transform.position, player.transform.position);
            if (distance <= gazeCancelDangerRadius)
            {
                gazeSuppressedByDanger = true;
            }
            else if (distance >= Mathf.Max(gazeCancelDangerRadius, gazeReacquireRadius))
            {
                gazeSuppressedByDanger = false;
            }
        }

        private bool IsPlayerInGazeDanger()
        {
            return player != null
                && Vector2.Distance(transform.position, player.transform.position)
                    <= gazeCancelDangerRadius;
        }

        private void CancelGaze()
        {
            state = EyeState.Orbit;
            nextGazeAllowedAt = Time.time + gazeReentryDelay;
        }

        private void ResolveGazeNormally()
        {
            state = EyeState.Orbit;
            nextGazeAllowedAt = Time.time + gazeReentryDelay;
        }

        private void ResolveGazeIntoFrenzy()
        {
            state = EyeState.Orbit;
            nextGazeAllowedAt = Time.time + gazeReentryDelay;
        }

        private void RetreatFromPlayer()
        {
            if (player == null)
            {
                HoldAtNormalHoverFloor();
                return;
            }

            float horizontalDirection = Mathf.Sign(transform.position.x - player.transform.position.x);
            if (Mathf.Approximately(horizontalDirection, 0f))
            {
                horizontalDirection = ResolveOrbitSide();
            }

            Vector2 desired = (Vector2)transform.position
                + new Vector2(horizontalDirection * orbitDistance * 0.55f, 0f);
            desired.y = Mathf.Max(ResolveMinimumNormalHoverY(), player.transform.position.y + preferredNormalHoverHeight);
            body.velocity = (desired - body.position).normalized * orbitSpeed;
        }

        private float ResolveMinimumNormalHoverY()
        {
            Vector2 origin = (Vector2)transform.position + Vector2.up * hoverGroundProbeHeight;
            RaycastHit2D[] hits = Physics2D.RaycastAll(
                origin,
                Vector2.down,
                hoverGroundProbeHeight + hoverGroundProbeDistance,
                hoverGroundLayers);
            foreach (RaycastHit2D hit in hits)
            {
                Collider2D candidate = hit.collider;
                if (candidate == null
                    || candidate.isTrigger
                    || candidate == bodyCollider
                    || candidate.GetComponentInParent<PlayerHealth>() != null
                    || candidate.GetComponentInParent<Damageable>() != null)
                {
                    continue;
                }

                return candidate.bounds.max.y + minimumNormalHoverHeight;
            }

            // If a scene has no suitable floor below the Eye, preserve its
            // authored altitude rather than snapping to a fragile global Y value.
            return transform.position.y;
        }

        private void Spawn(GameObject prefab, Vector2 position)
        {
            if (prefab != null)
            {
                Instantiate(prefab, position, Quaternion.identity);
            }
        }

        private void HandleDeath()
        {
            SacrificeClaims.Remove(claimedAlly);
            PossessionClaims.Remove(possessionHost);
        }

        private void HandleDamageResolved(DamageContext context, bool blocked, int appliedDamage)
        {
            if (blocked || appliedDamage <= 0)
            {
                return;
            }

            if (state == EyeState.GazeSetup || state == EyeState.GazeLocked)
            {
                CancelGaze();
                return;
            }

            if (state != EyeState.Sacrifice && state != EyeState.Possess)
            {
                return;
            }

            // These support commits are deliberate but interruptible until their
            // impact: a player can stop the rescue or possession by hitting Eye.
            SacrificeClaims.Remove(claimedAlly);
            PossessionClaims.Remove(possessionHost);
            claimedAlly = null;
            possessionHost = null;
            state = EyeState.Recover;
            stateEndsAt = Time.time + diveRecovery;
        }

        public void ApplyEvolution(EnemyEvolutionStage stage)
        {
            evolutionStage = stage;
        }

        private void OnDestroy()
        {
            if (gazeLine != null && gazeLine.material != null)
            {
                Destroy(gazeLine.material);
            }
        }
    }
}
