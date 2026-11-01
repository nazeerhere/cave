using System;
using System.Collections.Generic;
using Cave.Domain;
using UnityEngine;

namespace Cave.Environmental
{
    public enum EnvironmentalDestructionPresentation { AuthoredSpriteSwap, AuthoredAnimation, None }

    [CreateAssetMenu(fileName = "EnvironmentalMaterial", menuName = "Cave/Environment/Material Profile")]
    public sealed class EnvironmentalMaterialProfile : ScriptableObject
    {
        [SerializeField] private EnvironmentalMaterialFamily family;
        [SerializeField, Min(0f)] private float structuralResistance = 1f;
        [SerializeField, Min(0f)] private float impactSensitivity = 1f;
        [SerializeField, Min(0f)] private float heavyImpactMultiplier = 1.5f;
        [SerializeField] private EnvironmentalCollisionBehavior collisionBehavior;
        [SerializeField] private bool supportsRegeneration;
        [SerializeField, Min(0f)] private float defaultRegenerationDelay = 12f;
        [SerializeField, Min(.01f)] private float defaultRegenerationSeconds = 3f;
        [SerializeField] private EnvironmentalDestructionPresentation destructionPresentation;
        [SerializeField, Range(0, 8)] private int maximumAuthoredDebrisPieces;
        [SerializeField] private PhenomenonStructuralResponse[] phenomenonResponses;

        public EnvironmentalMaterialFamily Family => family;
        public EnvironmentalCollisionBehavior CollisionBehavior => collisionBehavior;
        public bool SupportsRegeneration => supportsRegeneration;
        public float DefaultRegenerationDelay => defaultRegenerationDelay;
        public float DefaultRegenerationSeconds => defaultRegenerationSeconds;
        public EnvironmentalDestructionPresentation DestructionPresentation => destructionPresentation;
        public int MaximumAuthoredDebrisPieces => maximumAuthoredDebrisPieces;

        public bool HasExplicitResponseFor(LawPhenomenon phenomenon)
        {
            if (phenomenonResponses == null) return false;
            for (int i = 0; i < phenomenonResponses.Length; i++)
                if (phenomenonResponses[i].phenomenon == phenomenon) return true;
            return false;
        }

        private void OnValidate()
        {
            Array values = Enum.GetValues(typeof(LawPhenomenon));
            List<PhenomenonStructuralResponse> expanded = phenomenonResponses != null
                ? new List<PhenomenonStructuralResponse>(phenomenonResponses)
                : new List<PhenomenonStructuralResponse>();
            foreach (LawPhenomenon phenomenon in values)
            {
                bool found = false;
                for (int i = 0; i < expanded.Count; i++)
                    if (expanded[i].phenomenon == phenomenon) { found = true; break; }
                if (!found) expanded.Add(new PhenomenonStructuralResponse
                {
                    phenomenon = phenomenon, integrityMultiplier = 1f, stabilityChange = 0f
                });
            }
            phenomenonResponses = expanded.ToArray();
        }

        public float ResolveImpact(float magnitude, bool heavy)
        {
            return Mathf.Max(0f, magnitude) * Mathf.Max(0f, impactSensitivity)
                * (heavy ? Mathf.Max(0f, heavyImpactMultiplier) : 1f)
                / Mathf.Max(0.01f, structuralResistance);
        }

        public PhenomenonStructuralResponse ResponseFor(LawPhenomenon phenomenon)
        {
            if (phenomenonResponses != null)
                for (int i = 0; i < phenomenonResponses.Length; i++)
                    if (phenomenonResponses[i].phenomenon == phenomenon) return phenomenonResponses[i];
            return new PhenomenonStructuralResponse { phenomenon = phenomenon, integrityMultiplier = 1f };
        }
    }
}
