using UnityEngine;

namespace Cave.Audio
{
    /// <summary>
    /// Deterministic coverage for source-independent Batch 0 policy. Runtime
    /// AudioSource lifecycle remains a Play Mode concern.
    /// </summary>
    public static class CaveAudioFoundationVerification
    {
        public static bool TryRunAll(out string failure)
        {
            if (!VerifyLegacyCueContract(out failure)
                || !VerifyLegacyLibraryResolution(out failure)
                || !VerifyVariantPolicy(out failure)
                || !VerifyPitchPolicy(out failure)
                || !VerifyCooldownPolicy(out failure)
                || !VerifyBounds(out failure)
                || !VerifyChannels(out failure))
            {
                return false;
            }

            failure = null;
            return true;
        }

        private static bool VerifyLegacyCueContract(out string failure)
        {
            CaveSfxCue[] expected =
            {
                CaveSfxCue.Bonus, CaveSfxCue.ButtonHover, CaveSfxCue.ButtonPress,
                CaveSfxCue.Explosion, CaveSfxCue.Hit, CaveSfxCue.Landing,
                CaveSfxCue.Shot, CaveSfxCue.Spawn, CaveSfxCue.Whoosh
            };
            System.Array actual = System.Enum.GetValues(typeof(CaveSfxCue));
            if (actual.Length < expected.Length)
            {
                failure = "The legacy CaveSfxCue contract was truncated.";
                return false;
            }

            if (actual.Length <= expected.Length
                || (CaveSfxCue)actual.GetValue(expected.Length) != CaveSfxCue.PlayerJump
                || (CaveSfxCue)actual.GetValue(expected.Length + 17) != CaveSfxCue.GroundSmashImpact
                || (CaveSfxCue)actual.GetValue(expected.Length + 18) != CaveSfxCue.EnemyMeleeSwing
                || (CaveSfxCue)actual.GetValue(expected.Length + 29) != CaveSfxCue.StatusReleased
                || (CaveSfxCue)actual.GetValue(actual.Length - 1) != CaveSfxCue.MasteryMilestone)
            {
                failure = "Batch 1, Batch 2, or Batch 3 cue identities are not appended in their declared order.";
                return false;
            }
            for (int i = 0; i < expected.Length; i++)
            {
                if ((CaveSfxCue)actual.GetValue(i) != expected[i])
                {
                    failure = "Legacy CaveSfxCue ordering changed at index " + i + ".";
                    return false;
                }
            }

            failure = null;
            return true;
        }

        private static bool VerifyLegacyLibraryResolution(out string failure)
        {
            CaveSfxLibrary library = Resources.Load<CaveSfxLibrary>("CaveSfxLibrary");
            if (library == null)
            {
                failure = "CaveSfxLibrary could not resolve from Resources.";
                return false;
            }

            // Only the original serialized slots require a direct library clip.
            // Appended semantic identities intentionally resolve through a
            // profile or a temporary fallback while production assets arrive.
            CaveSfxCue[] legacy =
            {
                CaveSfxCue.Bonus, CaveSfxCue.ButtonHover, CaveSfxCue.ButtonPress,
                CaveSfxCue.Explosion, CaveSfxCue.Hit, CaveSfxCue.Landing,
                CaveSfxCue.Shot, CaveSfxCue.Spawn, CaveSfxCue.Whoosh
            };
            foreach (CaveSfxCue cue in legacy)
            {
                if (library.GetClip(cue) == null)
                {
                    failure = "Legacy cue has no clip: " + cue + ".";
                    return false;
                }
            }

            failure = null;
            return true;
        }

        private static bool VerifyVariantPolicy(out string failure)
        {
            bool valid = CaveSfx.ResolveVariantIndex(1, 0, 0) == 0
                && CaveSfx.ResolveVariantIndex(3, 1, 1) == 2
                && CaveSfx.ResolveVariantIndex(3, 1, 0) == 0
                && CaveSfx.ResolveVariantIndex(0, -1, 0) == -1;
            failure = valid ? null : "Variant selection did not preserve single clips or avoid immediate repeats.";
            return valid;
        }

        private static bool VerifyPitchPolicy(out string failure)
        {
            bool valid = Mathf.Approximately(CaveSfx.ResolvePitch(.95f, 1.05f, 0f), .95f)
                && Mathf.Approximately(CaveSfx.ResolvePitch(.95f, 1.05f, 1f), 1.05f)
                && Mathf.Approximately(CaveSfx.ResolvePitch(1.05f, .95f, .5f), 1f);
            failure = valid ? null : "Pitch variation was not bounded deterministically.";
            return valid;
        }

        private static bool VerifyCooldownPolicy(out string failure)
        {
            bool valid = !CaveSfx.IsCooldownReady(10f, .2f, 10.1f)
                && CaveSfx.IsCooldownReady(10f, .2f, 10.2f)
                && CaveSfx.IsCooldownReady(-1f, .2f, 0f)
                && CaveSfx.IsCooldownReady(10f, 0f, 10f);
            failure = valid ? null : "Cooldown policy did not accept/reject expected timestamps.";
            return valid;
        }

        private static bool VerifyBounds(out string failure)
        {
            bool valid = CaveSfx.ConfiguredWorldVoiceCapacity > 0
                && CaveSfx.ConfiguredUiVoiceCapacity > 0
                && CaveSfx.ConfiguredLoopCapacity > 0;
            failure = valid ? null : "Audio pool capacities must remain bounded and positive.";
            return valid;
        }

        private static bool VerifyChannels(out string failure)
        {
            bool valid = CaveAudioSettings.VolumeFor(CaveAudioChannel.Sfx) >= 0f
                && CaveAudioSettings.VolumeFor(CaveAudioChannel.Ui) >= 0f
                && CaveAudioSettings.VolumeFor(CaveAudioChannel.Ambience) >= 0f;
            failure = valid ? null : "Audio category routing returned an invalid volume.";
            return valid;
        }
    }
}
