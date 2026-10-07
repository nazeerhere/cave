using UnityEngine;

namespace Cave.Axioms.Elemental
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class AxiomMassStateModel : MonoBehaviour
    {
        [SerializeField] private float naturalMassBaseline;
        private Rigidbody2D body;
        public float NaturalMassBaseline => naturalMassBaseline;
        public bool HasValidNaturalMassBaseline => naturalMassBaseline > 0f && !float.IsNaN(naturalMassBaseline) && !float.IsInfinity(naturalMassBaseline);
        public float EffectiveMass(float multiplier) => HasValidNaturalMassBaseline ? naturalMassBaseline * multiplier : 0f;
        public static AxiomMassStateModel EnsureOn(GameObject owner)
        {
            if (owner == null || owner.GetComponent<Rigidbody2D>() == null)
                return null;

            AxiomMassStateModel model = owner.GetComponent<AxiomMassStateModel>() ?? owner.AddComponent<AxiomMassStateModel>();
            model.CaptureNaturalMassBaseline();
            return model;
        }

        private void Awake()
        {
            CaptureNaturalMassBaseline();
        }

        private void CaptureNaturalMassBaseline()
        {
            if (body == null)
                body = GetComponent<Rigidbody2D>();
            if (naturalMassBaseline <= 0f && body != null)
                naturalMassBaseline = body.mass;
        }
    }
}
