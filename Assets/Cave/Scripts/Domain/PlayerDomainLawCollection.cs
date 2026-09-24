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

    /// <summary>Persistent player-owned authored Laws. The collection itself is the active composition.</summary>
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

        private void Awake() { Load(); }

        public DomainLawAuthoringCommitResult TryAuthor(DomainLaw candidate, DomainAuthoringContext context)
        {
            DomainAuthoringEligibilityResult eligibility = DomainAuthoringEligibility.Evaluate(composition, candidate, context);
            if (committing || !eligibility.IsEligible) return new DomainLawAuthoringCommitResult(false, eligibility, null);

            committing = true;
            try
            {
                string id = Guid.NewGuid().ToString("N");
                DomainAuthoredLaw authored = new DomainAuthoredLaw(id, candidate);
                List<DomainAuthoredLaw> pending = new List<DomainAuthoredLaw>(laws) { authored };
                string payload;
                bool hadPreviousPayload = PlayerPrefs.HasKey(SaveKey);
                string previousPayload = hadPreviousPayload
                    ? PlayerPrefs.GetString(SaveKey, string.Empty)
                    : null;
                try
                {
                    payload = SerializeForPersistence(pending);
                    PlayerPrefs.SetString(SaveKey, payload);
                    PlayerPrefs.Save();
                }
                catch
                {
                    // A failed persistence attempt cannot leave a visible
                    // in-memory commit or a speculative saved payload behind.
                    if (hadPreviousPayload) PlayerPrefs.SetString(SaveKey, previousPayload);
                    else PlayerPrefs.DeleteKey(SaveKey);
                    try { PlayerPrefs.Save(); }
                    catch { }
                    return new DomainLawAuthoringCommitResult(false, eligibility, null);
                }

                laws.Add(authored);
                composition = eligibility.Proposed;
                Changed?.Invoke();
                return new DomainLawAuthoringCommitResult(true, eligibility, authored);
            }
            finally { committing = false; }
        }

        private void Load()
        {
            laws.Clear();
            composition = DomainComposition.Empty;
            string payload = PlayerPrefs.GetString(SaveKey, string.Empty);
            if (string.IsNullOrEmpty(payload)) return;
            List<DomainAuthoredLaw> loaded;
            DomainComposition rebuilt;
            if (!TryDeserializeForPersistence(payload, out loaded, out rebuilt)) return;
            laws.AddRange(loaded);
            composition = rebuilt;
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
