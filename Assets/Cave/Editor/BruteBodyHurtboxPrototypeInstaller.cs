using Cave.Combat;
using Cave.Enemies;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>
    /// Authors the Brute-only body/hurtbox prototype without touching other
    /// enemies or rebuilding any presentation assets.
    /// </summary>
    internal static class BruteBodyHurtboxPrototypeInstaller
    {
        private const string BrutePrefabPath = "Assets/Cave/Prefabs/Mobs/Melee/Brute .prefab";
        private const string VisualRootName = "Approved Enemy Sheet Visual";
        private const string HurtboxesRootName = "Hurtboxes";
        private const string TorsoHurtboxName = "Brute Torso Hurtbox";
        private const string LowerBodyHurtboxName = "Brute Lower Body Hurtbox";

        [MenuItem("Tools/Cave/Enemies/Install Brute Body-Hurtbox Prototype")]
        private static void Install()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(BrutePrefabPath);
            try
            {
                Damageable damageable = root.GetComponent<Damageable>();
                Rigidbody2D body = root.GetComponent<Rigidbody2D>();
                BoxCollider2D bodyCollider = root.GetComponent<BoxCollider2D>();
                if (damageable == null || body == null || bodyCollider == null)
                {
                    throw new System.InvalidOperationException(
                        "Brute prototype requires its existing Damageable, Rigidbody2D, and BoxCollider2D.");
                }

                // The existing root collider remains the authored movement body.
                // Default is the project’s established physical-contact layer;
                // children on Damageable become the only incoming hit targets.
                root.layer = 0;
                bodyCollider.enabled = true;
                bodyCollider.isTrigger = false;

                Transform hurtboxRoot = EnsureChild(root.transform, HurtboxesRootName);
                hurtboxRoot.gameObject.layer = 0;
                BruteHurtbox torso = ConfigureHurtbox(
                    hurtboxRoot,
                    TorsoHurtboxName,
                    damageable,
                    new Vector2(-0.25f, 0.52f),
                    new Vector2(0.96f, 0.88f));
                BruteHurtbox lowerBody = ConfigureHurtbox(
                    hurtboxRoot,
                    LowerBodyHurtboxName,
                    damageable,
                    new Vector2(-0.25f, -0.30f),
                    new Vector2(0.82f, 0.58f));

                BruteBodyHurtboxRig rig = root.GetComponent<BruteBodyHurtboxRig>();
                if (rig == null)
                {
                    rig = root.AddComponent<BruteBodyHurtboxRig>();
                }

                rig.Configure(
                    bodyCollider,
                    FindDescendant(root.transform, VisualRootName),
                    new[] { torso, lowerBody });

                PrefabUtility.SaveAsPrefabAsset(root, BrutePrefabPath);
                Debug.Log("[Cave] Installed Brute body/hurtbox prototype: stable root body plus two damage triggers.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static BruteHurtbox ConfigureHurtbox(
            Transform parent,
            string objectName,
            Damageable owner,
            Vector2 localPosition,
            Vector2 size)
        {
            Transform child = EnsureChild(parent, objectName);
            GameObject hurtboxObject = child.gameObject;
            hurtboxObject.layer = LayerMask.NameToLayer("Damageable");
            if (hurtboxObject.layer < 0)
            {
                throw new System.InvalidOperationException("The project Damageable layer is required for Brute hurtboxes.");
            }

            child.localPosition = localPosition;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;

            BoxCollider2D collider = hurtboxObject.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = hurtboxObject.AddComponent<BoxCollider2D>();
            }

            collider.isTrigger = true;
            collider.offset = Vector2.zero;
            collider.size = size;

            BruteHurtbox hurtbox = hurtboxObject.GetComponent<BruteHurtbox>();
            if (hurtbox == null)
            {
                hurtbox = hurtboxObject.AddComponent<BruteHurtbox>();
            }

            hurtbox.Configure(owner, collider);
            return hurtbox;
        }

        private static Transform EnsureChild(Transform parent, string objectName)
        {
            Transform existing = parent.Find(objectName);
            if (existing != null)
            {
                return existing;
            }

            GameObject child = new GameObject(objectName);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root.name == objectName)
            {
                return root;
            }

            for (int index = 0; index < root.childCount; index++)
            {
                Transform result = FindDescendant(root.GetChild(index), objectName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}
