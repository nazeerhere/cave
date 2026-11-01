using System.Collections.Generic;
using Cave.Axioms.Control;
using Cave.Axioms.Elemental;
using UnityEngine;

namespace Cave.Combat
{
    public enum GeometricStructureKind
    {
        ProjectileTrajectory,
        MeleeChargeTrajectory,
        SpatialManifestationTether,
        SpellInvocationAnchor,
        PhaseCoherence
    }

    public enum PreGeometricPrinciple
    {
        SpatialProjection,
        GeometricTranslation,
        DistributedInteraction
    }

    public enum GeometricStrikeResult
    {
        Rejected,
        Damaged,
        Interrupted,
        Redirected
    }

    public interface IGeometricAttackAuthority
    {
        bool IsGeometricAttackActive { get; }
        Vector2 GeometricVelocity { get; }
        bool TryInterruptGeometricAttack(GameObject interrupter);
    }

    public interface IGeometricRedirectableAttack
    {
        bool TryRedirectGeometricAttack(GameObject newOwner, Vector2 direction);
    }

    [DisallowMultipleComponent]
    public sealed class FrenzyGeometryStructure : MonoBehaviour
    {
        private static readonly List<FrenzyGeometryStructure> Active =
            new List<FrenzyGeometryStructure>();

        [Header("Structure")]
        [SerializeField] private GeometricStructureKind kind = GeometricStructureKind.ProjectileTrajectory;
        [SerializeField] private PreGeometricPrinciple principle = PreGeometricPrinciple.SpatialProjection;
        [SerializeField, Min(1)] private int structuralResistance = 1;
        [SerializeField, Range(0f, 2f)] private float redirectionResistance = 0.5f;
        [SerializeField] private bool permitsRedirection = true;
        [SerializeField, Min(0.05f)] private float weakPointRadius = 0.14f;
        [SerializeField, Min(0.1f)] private float projectionSeconds = 0.75f;

        [Header("Current State (Read Only)")]
        [SerializeField, Min(0)] private int remainingResistance;
        [SerializeField] private bool revealed;
        [SerializeField] private bool redirected;

        private IGeometricAttackAuthority authority;
        private IGeometricRedirectableAttack redirectable;
        private LineRenderer trajectoryLine;
        private SpriteRenderer originNode;
        private SpriteRenderer weakPointNode;
        private CircleCollider2D weakPointCollider;
        private Sprite runtimeNodeSprite;
        private Material runtimeLineMaterial;

        public static IReadOnlyList<FrenzyGeometryStructure> ActiveStructures => Active;
        public GeometricStructureKind Kind => kind;
        public PreGeometricPrinciple Principle => principle;
        public int RemainingResistance => remainingResistance;
        public bool IsRevealed => revealed;
        public bool CanRedirect => permitsRedirection && !redirected && redirectable != null;

        private void Awake()
        {
            ResolveAuthority();
            remainingResistance = Mathf.Max(1, structuralResistance);
        }

        private void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
            SetRevealed(false);
        }

        private void OnDestroy()
        {
            Active.Remove(this);
            if (runtimeNodeSprite != null)
            {
                Destroy(runtimeNodeSprite);
            }
            if (runtimeLineMaterial != null)
            {
                Destroy(runtimeLineMaterial);
            }
        }

        private void LateUpdate()
        {
            if (!revealed || trajectoryLine == null)
            {
                return;
            }

            ResolveAuthority();
            Vector2 velocity = authority != null ? authority.GeometricVelocity : Vector2.zero;
            Vector3 origin = transform.position;
            Vector3 destination = origin + (Vector3)(velocity * projectionSeconds);
            trajectoryLine.SetPosition(0, origin);
            trajectoryLine.SetPosition(1, destination);
            if (originNode != null) originNode.transform.position = origin;
            if (weakPointNode != null) weakPointNode.transform.position = destination;
        }

        public void Configure(
            GeometricStructureKind configuredKind,
            PreGeometricPrinciple configuredPrinciple,
            int configuredResistance,
            float configuredRedirectionResistance,
            bool canRedirect)
        {
            kind = configuredKind;
            principle = configuredPrinciple;
            structuralResistance = Mathf.Max(1, configuredResistance);
            remainingResistance = structuralResistance;
            redirectionResistance = Mathf.Max(0f, configuredRedirectionResistance);
            permitsRedirection = canRedirect;
            ResolveAuthority();
        }

        public void SetRevealed(bool shouldReveal)
        {
            ResolveAuthority();
            bool active = authority == null || authority.IsGeometricAttackActive;
            revealed = shouldReveal && active && !redirected;
            if (revealed && trajectoryLine == null)
            {
                BuildPresentation();
            }

            if (trajectoryLine != null) trajectoryLine.enabled = revealed;
            if (originNode != null) originNode.enabled = revealed;
            if (weakPointNode != null) weakPointNode.enabled = revealed;
            if (weakPointCollider != null) weakPointCollider.enabled = revealed;
        }

        public GeometricStrikeResult TryStrike(
            GameObject attacker,
            Vector2 requestedDirection,
            bool accurateStrike)
        {
            PlayerCombatFlow flow = attacker != null
                ? attacker.GetComponentInParent<PlayerCombatFlow>()
                : null;
            ResolveAuthority();
            float control = 1f;
            if (attacker != null)
            {
                AxiomControlState controlState = attacker.GetComponentInParent<AxiomControlState>();
                if (controlState != null && controlState.HasStrongCurrentControl(Time.time))
                {
                    control += 0.5f;
                }
            }

            GeometricStrikeResult policyResult = MovementFrenzyGeometryRules.ResolveGeometricStrike(
                revealed && flow != null && flow.IsFrenzyActive,
                flow != null ? flow.FrenzyMasteryLevel : 0,
                accurateStrike,
                CanRedirect,
                control,
                redirectionResistance,
                remainingResistance,
                out int resolvedResistance);
            if (policyResult == GeometricStrikeResult.Rejected)
            {
                return policyResult;
            }

            if (policyResult == GeometricStrikeResult.Redirected)
            {
                if (redirectable.TryRedirectGeometricAttack(attacker, requestedDirection))
                {
                    redirected = true;
                    SetRevealed(false);
                    return GeometricStrikeResult.Redirected;
                }

                // A runtime owner may reject an otherwise eligible transfer;
                // fail closed without consuming structure durability.
                return GeometricStrikeResult.Rejected;
            }

            remainingResistance = resolvedResistance;
            if (policyResult == GeometricStrikeResult.Damaged)
            {
                return policyResult;
            }

            bool interrupted = authority != null
                && authority.TryInterruptGeometricAttack(attacker);
            SetRevealed(false);
            return interrupted
                ? GeometricStrikeResult.Interrupted
                : GeometricStrikeResult.Damaged;
        }

        public static bool TryStrikeCollider(
            Collider2D collider,
            GameObject attacker,
            Vector2 direction,
            bool accurateStrike)
        {
            FrenzyGeometryWeakPoint weakPoint = collider != null
                ? collider.GetComponent<FrenzyGeometryWeakPoint>()
                : null;
            return weakPoint != null
                && weakPoint.Owner != null
                && weakPoint.Owner.TryStrike(attacker, direction, accurateStrike)
                    != GeometricStrikeResult.Rejected;
        }

        private void ResolveAuthority()
        {
            if (authority == null) authority = GetComponent<IGeometricAttackAuthority>();
            if (redirectable == null) redirectable = GetComponent<IGeometricRedirectableAttack>();
        }

        private void BuildPresentation()
        {
            GameObject lineObject = new GameObject("Geometric Trajectory");
            lineObject.transform.SetParent(transform, false);
            trajectoryLine = lineObject.AddComponent<LineRenderer>();
            trajectoryLine.useWorldSpace = true;
            trajectoryLine.positionCount = 2;
            trajectoryLine.startWidth = 0.035f;
            trajectoryLine.endWidth = 0.012f;
            trajectoryLine.startColor = new Color(0.25f, 0.95f, 1f, 0.9f);
            trajectoryLine.endColor = new Color(0.65f, 0.3f, 1f, 0.25f);
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                runtimeLineMaterial = new Material(shader);
                trajectoryLine.material = runtimeLineMaterial;
            }
            trajectoryLine.sortingOrder = 30;

            runtimeNodeSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            originNode = CreateNode("Projection Node", new Color(0.3f, 0.9f, 1f, 0.8f), weakPointRadius * 0.65f);
            weakPointNode = CreateNode("Structural Weak Point", new Color(0.95f, 0.35f, 1f, 0.95f), weakPointRadius);
            weakPointCollider = weakPointNode.gameObject.AddComponent<CircleCollider2D>();
            weakPointCollider.isTrigger = true;
            weakPointCollider.radius = 0.5f;
            FrenzyGeometryWeakPoint marker = weakPointNode.gameObject.AddComponent<FrenzyGeometryWeakPoint>();
            marker.Initialize(this);
            int damageableLayer = LayerMask.NameToLayer("Damageable");
            if (damageableLayer >= 0) weakPointNode.gameObject.layer = damageableLayer;
        }

        private SpriteRenderer CreateNode(string objectName, Color color, float scale)
        {
            GameObject node = new GameObject(objectName);
            node.transform.SetParent(transform, false);
            node.transform.localScale = Vector3.one * Mathf.Max(0.05f, scale);
            SpriteRenderer renderer = node.AddComponent<SpriteRenderer>();
            renderer.sprite = runtimeNodeSprite;
            renderer.color = color;
            renderer.sortingOrder = 31;
            return renderer;
        }
    }

    public sealed class FrenzyGeometryWeakPoint : MonoBehaviour
    {
        public FrenzyGeometryStructure Owner { get; private set; }

        public void Initialize(FrenzyGeometryStructure owner)
        {
            Owner = owner;
        }
    }
}
