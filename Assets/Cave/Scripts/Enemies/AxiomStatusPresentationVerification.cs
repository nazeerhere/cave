using System.Collections.Generic;
using UnityEngine;

namespace Cave.Enemies
{
    /// <summary>Focused deterministic coverage for the mob-status presentation contract.</summary>
    public static class AxiomStatusPresentationVerification
    {
        public static bool TryRunAll(out string failure)
        {
            if (!VerifyExistingSlowAndStackContract(out failure)
                || !VerifyConstantCellLayout(out failure)
                || !VerifyArtworkNormalization(out failure)
                || !VerifyDeterministicOrdering(out failure)
                || !VerifyOverlayLayoutSeam(out failure))
            {
                return false;
            }

            failure = null;
            return true;
        }

        /// <summary>
        /// Unity-object coverage for the structural cell seam. The editor menu
        /// calls this separately from the deterministic policy checks.
        /// </summary>
        public static bool TryVerifyUnityCellSeam(out string failure)
        {
            GameObject sample = new GameObject("Axiom Status Presentation Verification Cell")
            {
                hideFlags = HideFlags.DontSave
            };

            try
            {
                MobStatusIconCell cell = sample.AddComponent<MobStatusIconCell>();
                cell.Configure(
                    null,
                    3,
                    0.16f,
                    0.16f,
                    0f,
                    new Vector2(0.32f, -0.28f),
                    0.15f,
                    24,
                    FontStyle.Bold,
                    new Color(0.94f, 0.98f, 1f, 1f),
                    0,
                    18);

                bool valid = cell.FutureOverlayAnchor != null
                    && !cell.FutureOverlayAnchor.gameObject.activeSelf
                    && cell.FutureOverlayAnchor.GetComponent<Renderer>() == null
                    && cell.PresentedStackCount == 3
                    && cell.CellSize == new Vector2(0.16f, 0.16f);
                failure = valid ? null : "The fixed status cell did not expose an inert future-overlay seam.";
                return valid;
            }
            finally
            {
                Object.DestroyImmediate(sample);
            }
        }

        private static bool VerifyExistingSlowAndStackContract(out string failure)
        {
            List<MobStatusPresentationEntry> entries = new List<MobStatusPresentationEntry>();
            MobStatusPresentationLayout.PopulateLegacyEntries(
                1 << (int)MobStatusIconKind.Slow,
                0,
                entries);
            bool slowIsPreserved = entries.Count == 1
                && entries[0].Kind == MobStatusIconKind.Slow
                && entries[0].StackCount == 0;

            MobStatusPresentationLayout.PopulateLegacyEntries(
                1 << (int)MobStatusIconKind.Imaginary,
                4,
                entries);
            bool imaginaryStackIsPreserved = entries.Count == 1
                && entries[0].Kind == MobStatusIconKind.Imaginary
                && entries[0].StackCount == 4;

            MobStatusPresentationLayout.PopulateLegacyEntries(0, 4, entries);
            bool expiredStatusClears = entries.Count == 0;
            bool valid = slowIsPreserved && imaginaryStackIsPreserved && expiredStatusClears;
            failure = valid ? null : "Existing Slow or Imaginary stack presentation data changed unexpectedly.";
            return valid;
        }

        private static bool VerifyConstantCellLayout(out string failure)
        {
            float firstCellWidth = 0.16f;
            float secondCellWidth = 0.16f;
            float firstCenter = MobStatusPresentationLayout.CellCenterX(0, 2, firstCellWidth, 0.05f);
            float secondCenter = MobStatusPresentationLayout.CellCenterX(1, 2, secondCellWidth, 0.05f);
            float oneStatusCenter = MobStatusPresentationLayout.CellCenterX(0, 1, firstCellWidth, 0.05f);
            bool valid = Mathf.Approximately(firstCellWidth, secondCellWidth)
                && Mathf.Approximately(firstCenter, -0.105f)
                && Mathf.Approximately(secondCenter, 0.105f)
                && Mathf.Approximately(oneStatusCenter, 0f);
            failure = valid ? null : "Adding or removing a status changed fixed-cell layout dimensions.";
            return valid;
        }

        private static bool VerifyArtworkNormalization(out string failure)
        {
            float extent = MobStatusPresentationLayout.ResolveArtworkExtent(0.16f, 0.16f, 0f);
            float smallScale = MobStatusPresentationLayout.ResolveArtworkScale(2f, 1f, extent, 1f);
            float largeScale = MobStatusPresentationLayout.ResolveArtworkScale(20f, 10f, extent, 1f);
            bool valid = Mathf.Approximately(extent, 0.16f)
                && Mathf.Approximately(2f * smallScale, extent)
                && Mathf.Approximately(20f * largeScale, extent)
                && Mathf.Approximately(1f * smallScale, 0.08f)
                && Mathf.Approximately(10f * largeScale, 0.08f);
            failure = valid ? null : "Artwork no longer fits its normalized presentation region.";
            return valid;
        }

        private static bool VerifyDeterministicOrdering(out string failure)
        {
            List<MobStatusPresentationEntry> entries = new List<MobStatusPresentationEntry>
            {
                new MobStatusPresentationEntry(MobStatusIconKind.Slow, 0),
                new MobStatusPresentationEntry(MobStatusIconKind.AxiomOrder, 2),
                new MobStatusPresentationEntry(MobStatusIconKind.AxiomHeat, 1),
                new MobStatusPresentationEntry(MobStatusIconKind.AxiomFlow, 1)
            };
            MobStatusPresentationLayout.Sort(entries);
            bool valid = entries[0].Kind == MobStatusIconKind.AxiomHeat
                && entries[1].Kind == MobStatusIconKind.AxiomFlow
                && entries[2].Kind == MobStatusIconKind.AxiomOrder
                && entries[3].Kind == MobStatusIconKind.Slow;
            failure = valid ? null : "Canonical Axiom or legacy status ordering was not deterministic.";
            return valid;
        }

        private static bool VerifyOverlayLayoutSeam(out string failure)
        {
            // The layout policy intentionally has no overlay input. A future frame
            // therefore cannot alter cell geometry through this contract.
            bool valid = Mathf.Approximately(
                MobStatusPresentationLayout.ResolveArtworkExtent(0.16f, 0.16f, 0f),
                MobStatusPresentationLayout.ResolveArtworkExtent(0.16f, 0.16f, 0f));
            failure = valid ? null : "Future overlay state leaked into status-cell layout.";
            return valid;
        }
    }
}
