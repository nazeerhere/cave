using Cave.Projectiles;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    public static class HeavyProjectileSprintVerificationMenu
    {
        // Deliberately mirrors the project's existing verification-menu pattern.
        [MenuItem("Tools/Cave/Verification/Run Heavy Projectile Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (HeavyProjectileSprintVerification.TryRunAll(out failure)
                && HeavyProjectilePrefabVerification.TryVerify(out failure))
            {
                Debug.Log("[Cave] Heavy Projectile verification passed.");
                return;
            }

            Debug.LogError("[Cave] Heavy Projectile verification failed: " + failure);
        }
    }

    internal static class HeavyProjectilePrefabVerification
    {
        private const string StandardPath = "Assets/Cave/Prefabs/Projectiles/PlayerProjectile.prefab";
        private const string HeavyPath = "Assets/Cave/Prefabs/Projectiles/HeavyPlayerProjectile.prefab";

        internal static bool TryVerify(out string failure)
        {
            GameObject standard = AssetDatabase.LoadAssetAtPath<GameObject>(StandardPath);
            GameObject heavy = AssetDatabase.LoadAssetAtPath<GameObject>(HeavyPath);
            if (!VerifyPrefab(standard, "standard", out failure)
                || !VerifyPrefab(heavy, "heavy", out failure))
            {
                return false;
            }

            failure = null;
            return true;
        }

        private static bool VerifyPrefab(GameObject prefab, string label, out string failure)
        {
            if (prefab == null)
            {
                failure = "Missing " + label + " projectile prefab.";
                return false;
            }

            CircleCollider2D collider = prefab.GetComponent<CircleCollider2D>();
            SpriteRenderer rootRenderer = prefab.GetComponent<SpriteRenderer>();
            SpriteRenderer childRenderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
            Vector3 scale = prefab.transform.localScale;
            bool unitRoot = Mathf.Approximately(scale.x, 1f)
                && Mathf.Approximately(scale.y, 1f)
                && Mathf.Approximately(scale.z, 1f);
            bool tightCircle = collider != null && Mathf.Approximately(collider.radius, .2f);
            bool visualChild = childRenderer != null && childRenderer.transform != prefab.transform
                && (rootRenderer == null || !rootRenderer.enabled);
            if (!unitRoot || !tightCircle || !visualChild)
            {
                failure = "Invalid " + label
                    + " projectile geometry: root must be (1,1,1), travel circle radius .2, and the renderer must live on a visual child.";
                return false;
            }

            failure = null;
            return true;
        }
    }
}
