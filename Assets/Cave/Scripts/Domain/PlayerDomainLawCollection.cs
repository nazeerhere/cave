using System;
using System.Collections.Generic;
using Cave.Axioms.Mastery;
using UnityEngine;

namespace Cave.Domain
{
    public sealed class DomainAuthoredLaw
    {
        internal DomainAuthoredLaw(string id, DomainLaw law) { Id = id; Law = law; }
        public string Id { get; }
        public DomainLaw Law { get; }
    }

    public sealed class DomainLawAuthoringCommitResult
    {
        internal DomainLawAuthoringCommitResult(bool succeeded, DomainAuthoringEligibilityResult eligibility, DomainAuthoredLaw authoredLaw)
        { Succeeded = succeeded; Eligibility = eligibility; AuthoredLaw = authoredLaw; }
        public bool Succeeded { get; }
        public DomainAuthoringEligibilityResult Eligibility { get; }
        public DomainAuthoredLaw AuthoredLaw { get; }
    }

    /// <summary>
    /// Player-owned authored Laws for the current run. The collection itself is
    /// the active composition; persistent authoring is intentionally excluded.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerDomainLawCollection : MonoBehaviour
    {
        private const string SaveKey = "Cave.Domain.AuthoredLaws.v1";
        private const int SchemaVersion = 1;
        private readonly List<DomainAuthoredLaw> laws = new List<DomainAuthoredLaw>();
        private DomainComposition composition = DomainComposition.Empty;
        private bool committing;

        public event Action Changed;
        public IReadOnlyList<DomainAuthoredLaw> Laws => laws.AsReadOnly();
        public DomainComposition Composition => composition;

        public static PlayerDomainLawCollection EnsureOn(GameObject owner)
        {
            return owner == null ? null : owner.GetComponent<PlayerDomainLawCollection>() ?? owner.AddComponent<PlayerDomainLawCollection>();
        }

        private void Awake()
        {
            // Earlier builds persisted this collection. Clear that retired
            // payload once so a newly created run cannot inherit old Laws.
            if (PlayerPrefs.HasKey(SaveKey))
            {
                PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.Save();
            }
        }

        public DomainLawAuthoringCommitResult TryAuthor(DomainLaw candidate, DomainAuthoringContext context)
        {
            DomainAuthoringEligibilityResult eligibility = DomainAuthoringEligibility.Evaluate(composition, candidate, context);
            if (committing || !eligibility.IsEligible) return new DomainLawAuthoringCommitResult(false, eligibility, null);

            committing = true;
            try
            {
                string id = Guid.NewGuid().ToString("N");
                DomainAuthoredLaw authored = new DomainAuthoredLaw(id, candidate);
                laws.Add(authored);
                composition = eligibility.Proposed;
                Changed?.Invoke();
                return new DomainLawAuthoringCommitResult(true, eligibility, authored);
            }
            finally { committing = false; }
        }

        /// <summary>Called only by the actual player new-run lifecycle.</summary>
        public void ResetRunLaws()
        {
            if (laws.Count == 0 && composition.Count == 0) return;
            laws.Clear();
            composition = DomainComposition.Empty;
            Changed?.Invoke();
        }

        internal static string SerializeForPersistence(IReadOnlyList<DomainAuthoredLaw> source)
        {
            DomainLawSave save = new DomainLawSave { schemaVersion = SchemaVersion, laws = new DomainLawSaveEntry[source.Count] };
            for (int index = 0; index < source.Count; index++)
            {
                DomainLaw law = source[index].Law;
                save.laws[index] = new DomainLawSaveEntry { id = source[index].Id, expression = (int)law.Expression,
                    phenomenon = (int)law.Phenomenon, territory = (int)law.TerritoryPrinciple };
            }
            return JsonUtility.ToJson(save);
        }

        internal static bool TryDeserializeForPersistence(
            string payload,
            out List<DomainAuthoredLaw> loaded,
            out DomainComposition rebuilt)
        {
            loaded = new List<DomainAuthoredLaw>();
            rebuilt = DomainComposition.Empty;
            DomainLawSave save;
            try { save = JsonUtility.FromJson<DomainLawSave>(payload); }
            catch { return false; }
            if (save == null || save.schemaVersion != SchemaVersion || save.laws == null) return false;

            HashSet<string> ids = new HashSet<string>();
            for (int index = 0; index < save.laws.Length; index++)
            {
                DomainLawSaveEntry entry = save.laws[index];
                Guid parsedId;
                DomainLaw law; LawValidationResult validation;
                if (entry == null || !Guid.TryParseExact(entry.id, "N", out parsedId)
                    || !ids.Add(entry.id)
                    || !DomainLaw.TryCreate((LawExpression)entry.expression, (LawPhenomenon)entry.phenomenon,
                        (LawTerritoryPrinciple)entry.territory, out law, out validation))
                {
                    loaded.Clear();
                    rebuilt = DomainComposition.Empty;
                    return false;
                }

                DomainCompositionMutationResult add = DomainCompositionEditor.Add(rebuilt, law);
                if (!add.Succeeded)
                {
                    loaded.Clear();
                    rebuilt = DomainComposition.Empty;
                    return false;
                }

                rebuilt = add.Resulting;
                loaded.Add(new DomainAuthoredLaw(entry.id, law));
            }

            return true;
        }

        [Serializable] private sealed class DomainLawSave { public int schemaVersion; public DomainLawSaveEntry[] laws; }
        [Serializable] private sealed class DomainLawSaveEntry { public string id; public int expression; public int phenomenon; public int territory; }
    }
}
