using System.Collections.Generic;
using UnityEngine;

namespace Cave.Domain
{
    /// <summary>
    /// Cosmetic-only structural presentation for a player Domain manifestation.
    /// It reads the authoritative manifestation lifecycle but never changes it.
    /// The visual motion is a projected orbital plane; it never writes world Z,
    /// collider state, target state, or any Domain gameplay authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DomainManifestationVisual : MonoBehaviour
    {
        private const string ResourceRoot = "Domain/Manifestation/";
        private const float ManifestDuration = .5f;
        private const float CollapseDuration = .12f;
        private const int BackOrder = -8;

        [Header("Presentation")]
        [SerializeField, Range(.25f, 1.5f)] private float motionSpeed = 1f;
        [SerializeField] private bool combatReadabilityMode = true;

#if UNITY_EDITOR
        [Header("Development Preview (Editor only)")]
        [SerializeField] private bool developmentPreview;
        [SerializeField] private DomainManifestationState previewState = DomainManifestationState.Active;
        [SerializeField, Min(.1f)] private float previewRadius = 6f;
        [SerializeField] private bool showFragments = true;
        [SerializeField] private bool showOrbits = true;
        [SerializeField] private bool showAxis = true;
        [SerializeField] private bool pulseAxis = true;
        [SerializeField] private bool pauseRotation;
#endif

        private readonly List<Part> parts = new List<Part>(14);
        private readonly List<Sprite> runtimeSprites = new List<Sprite>(14);
        private DomainManifestationState presentedState = DomainManifestationState.Dormant;
        private float presentedRadius;
        private float stateElapsed;
        private bool constructed;

        private sealed class Part
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Sprite Sprite;
            public Vector2 Aspect;
            public float OrbitRadius;
            public float BaseAngle;
            public float AngularVelocity;
            public float TiltDegrees;
            public float PlaneDegrees;
            public float ArtDegrees;
            public float Opacity;
            public float FrontAlpha;
            public float DepthScale;
            public int SortingBase;
            public int SortingDepthRange;
            public float ManifestDelay;
            public float ManifestSpan;
            public bool TravelsOrbit;
            public PartKind Kind;
        }

        private enum PartKind
        {
            Fragment,
            Orbit,
            Halo,
            Axis
        }

        /// <summary>Projected point in the cosmetic orbital plane. Positive depth is foreground.</summary>
        internal struct OrbitSample
        {
            public readonly Vector2 Position;
            public readonly float Depth;

            public OrbitSample(Vector2 position, float depth)
            {
                Position = position;
                Depth = depth;
            }
        }

        private void Awake()
        {
            EnsureStructure();
            ApplyVisibility(0f);
        }

        private void OnDisable()
        {
            ApplyVisibility(0f);
        }

        private void OnDestroy()
        {
            for (int index = 0; index < runtimeSprites.Count; index++)
            {
                if (runtimeSprites[index] != null) Destroy(runtimeSprites[index]);
            }
        }

        /// <summary>Called by the gameplay authority whenever its lifecycle or radius is refreshed.</summary>
        public void Present(DomainManifestationState state, float radius)
        {
            EnsureStructure();

#if UNITY_EDITOR
            if (developmentPreview)
            {
                state = previewState;
                radius = previewRadius;
            }
#endif

            radius = Mathf.Max(.1f, radius);
            if (presentedState != state)
            {
                presentedState = state;
                stateElapsed = 0f;
            }

            presentedRadius = radius;
            if (!IsVisibleState(state)) ApplyVisibility(0f);
        }

        private void Update()
        {
            if (!constructed) return;

#if UNITY_EDITOR
            if (developmentPreview) Present(previewState, previewRadius);
#endif

            if (!IsVisibleState(presentedState)) return;

            stateElapsed += Time.unscaledDeltaTime;
            float assembly = ResolveAssembly();
            ApplyVisibility(assembly);
            if (assembly <= 0f) return;

#if UNITY_EDITOR
            if (pauseRotation) return;
#endif

            float time = Time.unscaledTime * motionSpeed;
            for (int index = 0; index < parts.Count; index++)
            {
                Part part = parts[index];
                if (!IsPartEnabled(part.Kind)) continue;

                float partAssembly = ResolvePartAssembly(part, assembly);
                if (part.TravelsOrbit)
                {
                    PresentTravellingPart(part, partAssembly, time);
                }
                else
                {
                    PresentAnchoredPart(part, partAssembly, time);
                }
            }
        }

        private void EnsureStructure()
        {
            if (constructed) return;
            constructed = true;

            Transform fragments = CreateGroup("Structural Fragments");
            Transform orbits = CreateGroup("Projected Orbits");
            Transform axis = CreateGroup("Celestial Axis");

            // Stone pieces follow different projected planes. Their art follows the tangent of
            // that plane rather than locally spinning like a flat, screen-aligned disc.
            AddPart(fragments, "Outer Stone Arc A", "Structure/01_Outer_Stone_Arc_A", PartKind.Fragment,
                .98f, 145f, 8f, 34f, -11f, 0f, .48f, .62f, .10f, new Vector2(1f, 1f), BackOrder + 2, 2, true);
            AddPart(fragments, "Outer Stone Arc B", "Structure/02_Outer_Stone_Arc_B", PartKind.Fragment,
                .98f, -35f, -5.5f, 47f, 17f, 0f, .46f, .60f, .10f, new Vector2(1f, 1f), BackOrder + 2, 2, true);
            AddPart(fragments, "Inner Stone Arc", "Structure/03_Inner_Stone_Arc", PartKind.Fragment,
                .70f, 48f, 6.5f, 39f, 24f, 0f, .35f, .64f, .08f, new Vector2(.9f, .9f), BackOrder + 3, 2, true);
            AddPart(fragments, "Stone Connector", "Structure/04_Stone_Connector", PartKind.Fragment,
                .74f, -132f, -4.5f, 55f, -29f, 0f, .29f, .66f, .07f, new Vector2(.68f, .68f), BackOrder + 3, 2, true);

            // Full-ring art is intentionally anchored. The art is already elliptical; we use it
            // as a spatial band and reserve actual orbital motion for arc/fragment elements.
            AddPart(orbits, "Astrolabe Void Track", "Orbits/ornate_cosmic_astrolabe_ring", PartKind.Orbit,
                0f, 22f, .15f, 32f, 0f, 22f, .28f, .52f, .02f, new Vector2(2.12f, .72f), BackOrder, 1, false);
            AddPart(orbits, "Shattered Void Track", "Orbits/shattered_celestial_orbital_ring", PartKind.Orbit,
                0f, -38f, -.12f, 48f, 0f, -38f, .25f, .50f, .02f, new Vector2(1.75f, .54f), BackOrder + 1, 1, false);
            AddPart(orbits, "Violet Cosmic Drift", "Orbits/ethereal_violet_cosmic_ring", PartKind.Orbit,
                0f, 71f, .18f, 41f, 0f, 71f, .13f, .48f, .015f, new Vector2(1.36f, .44f), BackOrder + 4, 1, false);
            AddPart(orbits, "Iridescent Orbital Halo", "Orbits/iridescent_orbital_halo", PartKind.Halo,
                0f, -12f, .22f, 37f, 0f, -12f, .24f, .42f, .025f, new Vector2(2.3f, .76f), BackOrder + 1, 1, false);
            AddPart(orbits, "Inner Orbit Arc A", "Structure/07_Subtractive_Orbit_Arc_A", PartKind.Orbit,
                .18f, -15f, -24f, 35f, -8f, 0f, .20f, .58f, .06f, new Vector2(1.36f, .42f), BackOrder + 5, 2, true);
            AddPart(orbits, "Inner Orbit Arc B", "Structure/08_Subtractive_Orbit_Arc_B", PartKind.Orbit,
                .22f, 165f, -24f, 35f, -8f, 180f, .18f, .58f, .06f, new Vector2(1.36f, .42f), BackOrder + 5, 2, true);

            AddPart(axis, "Celestial Axis", "Structure/05_Celestial_Axis", PartKind.Axis,
                0f, 0f, 0f, 0f, 0f, 0f, .28f, .58f, .015f, new Vector2(.45f, 1.25f), BackOrder + 6, 0, false);
            AddPart(axis, "Compass Ornament", "Structure/06_Crescent_Compass_Ornament", PartKind.Axis,
                0f, 0f, 0f, 0f, 0f, 0f, .19f, .55f, .02f, new Vector2(.45f, .45f), BackOrder + 7, 0, false);
        }

        private Transform CreateGroup(string groupName)
        {
            GameObject group = new GameObject(groupName);
            group.transform.SetParent(transform, false);
            return group.transform;
        }

        private void AddPart(
            Transform parent, string partName, string resourcePath, PartKind kind, float orbitRadius,
            float baseAngleDegrees, float angularVelocityDegrees, float tiltDegrees, float planeDegrees,
            float artDegrees, float opacity, float frontAlpha, float depthScale, Vector2 aspect,
            int sortingBase, int sortingDepthRange, bool travelsOrbit)
        {
            Sprite sprite = LoadSprite(ResourceRoot + resourcePath);
            if (sprite == null) return;

            GameObject partObject = new GameObject(partName);
            partObject.transform.SetParent(parent, false);
            SpriteRenderer renderer = partObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingBase;

            parts.Add(new Part
            {
                Transform = partObject.transform,
                Renderer = renderer,
                Sprite = sprite,
                Aspect = aspect,
                OrbitRadius = orbitRadius,
                BaseAngle = baseAngleDegrees * Mathf.Deg2Rad,
                AngularVelocity = angularVelocityDegrees * Mathf.Deg2Rad,
                TiltDegrees = tiltDegrees,
                PlaneDegrees = planeDegrees,
                ArtDegrees = artDegrees,
                Opacity = opacity,
                FrontAlpha = frontAlpha,
                DepthScale = depthScale,
                SortingBase = sortingBase,
                SortingDepthRange = sortingDepthRange,
                ManifestDelay = kind == PartKind.Axis ? 0f : kind == PartKind.Fragment ? .16f : .06f,
                ManifestSpan = kind == PartKind.Axis ? .20f : kind == PartKind.Fragment ? .34f : .30f,
                TravelsOrbit = travelsOrbit,
                Kind = kind
            });
        }

        private void PresentTravellingPart(Part part, float assembly, float time)
        {
            float angle = part.BaseAngle + time * part.AngularVelocity;
            float distance = presentedRadius * part.OrbitRadius;
            if (presentedState == DomainManifestationState.Manifesting) distance *= assembly;
            else if (presentedState == DomainManifestationState.Collapsing) distance *= 1f + (1f - assembly) * .22f;

            OrbitSample sample = ProjectOrbit(angle, distance, part.TiltDegrees, part.PlaneDegrees);
            part.Transform.localPosition = new Vector3(sample.Position.x, sample.Position.y, 0f);
            part.Transform.localRotation = Quaternion.Euler(0f, 0f,
                ResolveOrbitTangentDegrees(angle, part.TiltDegrees, part.PlaneDegrees) + part.ArtDegrees);
            ApplyScale(part, assembly, time, sample.Depth);
            ApplyDepthPresentation(part, assembly, sample.Depth);
        }

        private void PresentAnchoredPart(Part part, float assembly, float time)
        {
            // A tiny depth cycle gives stacked rings a shared central axis without rotating full sprites.
            float depth = Mathf.Sin(part.BaseAngle + time * part.AngularVelocity) * .28f;
            float bob = part.Kind == PartKind.Axis ? Mathf.Sin(time * Mathf.PI * 2f / 1.4f) * .025f : 0f;
            part.Transform.localPosition = new Vector3(0f, bob * presentedRadius, 0f);
            part.Transform.localRotation = Quaternion.Euler(0f, 0f,
                part.ArtDegrees + (part.Kind == PartKind.Axis ? Mathf.Sin(time * .7f) * 2f : 0f));
            ApplyScale(part, assembly, time, depth);
            ApplyDepthPresentation(part, assembly, depth);
        }

        private void ApplyDepthPresentation(Part part, float assembly, float depth)
        {
            float depth01 = Mathf.InverseLerp(-1f, 1f, depth);
            float alpha = part.Opacity * assembly * Mathf.Lerp(1f, part.FrontAlpha, depth01);
            if (combatReadabilityMode && depth > 0f) alpha *= Mathf.Lerp(1f, .72f, depth);
            part.Renderer.color = new Color(1f, 1f, 1f, alpha);
            part.Renderer.sortingOrder = part.SortingBase + Mathf.RoundToInt(depth * part.SortingDepthRange);
        }

        private Sprite LoadSprite(string resourcePath)
        {
            Sprite imported = Resources.Load<Sprite>(resourcePath);
            if (imported != null) return imported;
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null) return null;
            Sprite fallback = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(.5f, .5f), 128f);
            runtimeSprites.Add(fallback);
            return fallback;
        }

        private float ResolveAssembly()
        {
            switch (presentedState)
            {
                case DomainManifestationState.Manifesting:
                    return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(stateElapsed / ManifestDuration));
                case DomainManifestationState.Active:
                    return 1f;
                case DomainManifestationState.Collapsing:
                    return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(stateElapsed / CollapseDuration));
                default:
                    return 0f;
            }
        }

        private void ApplyVisibility(float assembly)
        {
            for (int index = 0; index < parts.Count; index++)
            {
                Part part = parts[index];
                float partAssembly = ResolvePartAssembly(part, assembly);
                bool enabled = partAssembly > 0f && IsPartEnabled(part.Kind);
                part.Renderer.enabled = enabled;
                if (enabled) part.Renderer.color = new Color(1f, 1f, 1f, part.Opacity * partAssembly);
            }
        }

        private float ResolvePartAssembly(Part part, float assembly)
        {
            if (presentedState == DomainManifestationState.Manifesting)
            {
                return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((stateElapsed - part.ManifestDelay) / part.ManifestSpan));
            }
            return assembly;
        }

        private void ApplyScale(Part part, float assembly, float time, float depth)
        {
            Vector2 spriteSize = part.Sprite.bounds.size;
            float targetWidth = presentedRadius * part.Aspect.x;
            float targetHeight = presentedRadius * part.Aspect.y;
            float settle = part.Kind == PartKind.Fragment ? Mathf.Lerp(.58f, 1f, assembly) : assembly;
            float depthMultiplier = 1f + depth * part.DepthScale;
            float axisPulse = part.Kind == PartKind.Axis ? ResolveAxisPulse(time) : 1f;
            part.Transform.localScale = new Vector3(
                targetWidth / Mathf.Max(.01f, spriteSize.x) * settle * depthMultiplier * axisPulse,
                targetHeight / Mathf.Max(.01f, spriteSize.y) * settle * depthMultiplier * axisPulse, 1f);
        }

        /// <summary>
        /// Converts a point in a tilted circular orbit to the screen plane. The resulting depth
        /// only affects visual scale, order, and alpha; the gameplay world remains entirely 2D.
        /// </summary>
        internal static OrbitSample ProjectOrbit(float angleRadians, float radius, float tiltDegrees, float planeDegrees)
        {
            float tilt = Mathf.Clamp(tiltDegrees, 5f, 85f) * Mathf.Deg2Rad;
            float plane = planeDegrees * Mathf.Deg2Rad;
            float x = Mathf.Cos(angleRadians) * radius;
            float y = Mathf.Sin(angleRadians) * radius * Mathf.Sin(tilt);
            float cosine = Mathf.Cos(plane);
            float sine = Mathf.Sin(plane);
            Vector2 position = new Vector2(x * cosine - y * sine, x * sine + y * cosine);
            return new OrbitSample(position, Mathf.Sin(angleRadians) * Mathf.Cos(tilt));
        }

        private static float ResolveOrbitTangentDegrees(float angleRadians, float tiltDegrees, float planeDegrees)
        {
            float tilt = Mathf.Clamp(tiltDegrees, 5f, 85f) * Mathf.Deg2Rad;
            float plane = planeDegrees * Mathf.Deg2Rad;
            float tangentX = -Mathf.Sin(angleRadians);
            float tangentY = Mathf.Cos(angleRadians) * Mathf.Sin(tilt);
            float cosine = Mathf.Cos(plane);
            float sine = Mathf.Sin(plane);
            return Mathf.Atan2(tangentX * sine + tangentY * cosine, tangentX * cosine - tangentY * sine)
                * Mathf.Rad2Deg;
        }

        private float ResolveAxisPulse(float time)
        {
#if UNITY_EDITOR
            if (!pulseAxis) return 1f;
#endif
            return 1f + Mathf.Sin(time * Mathf.PI * 2f / 1.4f) * .025f;
        }

        private bool IsPartEnabled(PartKind kind)
        {
#if UNITY_EDITOR
            if (kind == PartKind.Fragment) return showFragments;
            if (kind == PartKind.Orbit || kind == PartKind.Halo) return showOrbits;
            return showAxis;
#else
            return true;
#endif
        }

        private static bool IsVisibleState(DomainManifestationState state)
        {
            return state == DomainManifestationState.Manifesting || state == DomainManifestationState.Active
                || state == DomainManifestationState.Collapsing;
        }

#if UNITY_EDITOR
        [ContextMenu("Preview Manifestation")]
        private void PreviewManifestation() { developmentPreview = true; previewState = DomainManifestationState.Manifesting; Present(previewState, previewRadius); }
        [ContextMenu("Preview Active")]
        private void PreviewActive() { developmentPreview = true; previewState = DomainManifestationState.Active; Present(previewState, previewRadius); }
        [ContextMenu("Preview Collapse")]
        private void PreviewCollapse() { developmentPreview = true; previewState = DomainManifestationState.Collapsing; Present(previewState, previewRadius); }
        [ContextMenu("Restore Gameplay Presentation")]
        private void RestoreGameplayPresentation() { developmentPreview = false; }
        [ContextMenu("Increase Motion Speed")]
        private void IncreaseMotionSpeed() { motionSpeed = Mathf.Min(1.5f, motionSpeed + .1f); }
        [ContextMenu("Decrease Motion Speed")]
        private void DecreaseMotionSpeed() { motionSpeed = Mathf.Max(.25f, motionSpeed - .1f); }
#endif
    }
}
