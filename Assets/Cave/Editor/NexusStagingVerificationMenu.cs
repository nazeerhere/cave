using System;
using Cave.Player;
using Cave.UI;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    public static class NexusStagingVerificationMenu
    {
        [MenuItem("Tools/Cave/Verification/Run Nexus Staging Verification")]
        private static void Run()
        {
            string failure;
            if (!NexusStagingVerification.TryRunAll(out failure))
            {
                throw new InvalidOperationException("Nexus Staging verification failed: " + failure);
            }

            for (int index = 1; index < PlayerSkinLibrary.Count; index++)
            {
                PlayerSkinSelection skin = (PlayerSkinSelection)index;
                VerifyFrames(skin, "Idle");
                VerifyFrames(skin, "Run");
                VerifyFrames(skin, "Jump");
                VerifyFrames(skin, "Landing");
                VerifyFrames(skin, "Spin");
                VerifyFrames(skin, "Guard");
                VerifyFrames(skin, "Parry");
                VerifyFrames(skin, "GuardBreak");
                VerifyFrames(skin, "Dash");
                VerifyFrames(skin, "Hit");
                VerifyFrames(skin, "Knockback");
                VerifyFrames(skin, "Death");
                VerifyFrames(skin, "Interact");
                VerifyFrames(skin, "Cast");
                VerifyFrames(skin, "Brace");
                for (int heavy = 1; heavy <= 5; heavy++)
                {
                    VerifyFrames(skin, "Heavy_" + heavy);
                }
            }

            Debug.Log("[Cave] Nexus Staging verification passed.");
        }

        private static void VerifyFrames(PlayerSkinSelection skin, string action)
        {
            Sprite[] frames = PlayerSkinLibrary.LoadFrames(skin, action);
            if (frames.Length == 0)
            {
                throw new InvalidOperationException("Skin library has no frames: " + skin + "/" + action + ".");
            }

            for (int index = 0; index < frames.Length; index++)
            {
                if (frames[index] == null || Mathf.Abs(frames[index].pixelsPerUnit - 800f) > .01f)
                {
                    throw new InvalidOperationException("Skin importer contract failed: " + skin + "/" + action + ".");
                }
            }
        }
    }
}
