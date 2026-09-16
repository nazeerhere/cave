using System;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Enemies;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>
    /// Idempotently authors the standard three-region damage rig for existing
    /// non-Brute mob prefabs. It does not alter combat scripts, health values,
    /// movement settings, visual hierarchies, or spawn configuration.
    /// </summary>
    internal static class EnemyThreeHurtboxRigInstaller
    {
        private const int DefaultLayer = 0;
        private const string DamageableLayerName = "Damageable";
        private const string HurtboxesRootName = "Hurtboxes";
        private const string LowerName = "Lower Hurtbox";
        private const string MiddleName = "Middle Hurtbox";
        private const string UpperName = "Upper Hurtbox";

        private static readonly string[] TargetPrefabPaths =
        {
            "Assets/Cave/Prefabs/Mobs/Melee/Corrupt Brute .prefab",
            "Assets/Cave/Prefabs/Mobs/Melee/LightBandit Variant.prefab",
            "Assets/Cave/Prefabs/Mobs/Range/Detective.prefab",
            "Assets/Cave/Prefabs/Mobs/Range/Corrupt Detective.prefab",
            "Assets/Cave/Prefabs/Mobs/Range/Goth .prefab",
            "Assets/Cave/Prefabs/Mobs/Range/Corrupt Goth .prefab",
            "Assets/Cave/Prefabs/Mobs/Support/Wizard.prefab",
            "Assets/Cave/Prefabs/Mobs/Support/DarkWizard.prefab",
            "Assets/Cave/Prefabs/Mobs/Swarm/Eye.prefab",
            "Assets/Cave/Prefabs/Mobs/Swarm/Corrupted Eye.prefab",
            "Assets/Cave/Prefabs/Mobs/Swarm/Skeleton.prefab",
            "Assets/Cave/Prefabs/Mobs/Swarm/GeneralSkeleton Variant.prefab",
            "Assets/Cave/Prefabs/Mobs/Tank/Troll.prefab",
            "Assets/Cave/Prefabs/Mobs/Tank/Corrupt Troll.prefab"
        };

        [MenuItem("Tools/Cave/Enemies/Install Standard Three-Hurtbox Rigs")]
        private static void InstallAll()
        {
            int installed = 0;
            for (int index = 0; index < TargetPrefabPaths.Length; index++)
            {
                InstallPrefab(TargetPrefabPaths[index]);
                installed++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Cave] Installed standard three-hurtbox rigs on " + installed + " mob prefabs. Brute remains on its prototype rig.");
        }

        private static void InstallPrefab(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Damageable damageable = root.GetComponent<Damageable>();
                Rigidbody2D body = root.GetComponent<Rigidbody2D>();
                if (damageable == null || body == null)
                {
                    throw new InvalidOperationException(
                        "Three-hurtbox rig requires existing Damageable and Rigidbody2D: " + prefabPath);
                }

                Collider2D[] stableBodies = EnsureStableBodies(root, prefabPath);
                // The root remains the existing Damageable authority, but its
                // solid body must never be an attack-target collider. Incoming
                // attacks resolve the three child triggers through that parent.
                root.layer = DefaultLayer;
                Rect bodyBounds = CalculateLocalBodyBounds(root.transform, stableBodies);
                Transform hurtboxesRoot = EnsureChild(root.transform, HurtboxesRootName);
                hurtboxesRoot.gameObject.layer = DefaultLayer;

                EnemyAnatomicalHurtbox lower;
                EnemyAnatomicalHurtbox middle;
                EnemyAnatomicalHurtbox upper;
                if (!UsesPointedHatBodyProfile(prefabPath)
                    || !TryGetExistingThreeHurtboxes(hurtboxesRoot, out lower, out middle, out upper))
                {
                    lower = ConfigureBoxHurtbox(
                        hurtboxesRoot,
                        LowerName,
                        EnemyHurtboxRegion.Lower,
                        damageable,
                        RegionCenter(bodyBounds, 0.15f),
                        RegionSize(bodyBounds, 0.66f, 0.27f));
                    middle = ConfigureBoxHurtbox(
                        hurtboxesRoot,
                        MiddleName,
                        EnemyHurtboxRegion.Middle,
                        damageable,
                        RegionCenter(bodyBounds, 0.475f),
                        RegionSize(bodyBounds, 0.80f, 0.35f));
                    upper = ConfigureCapsuleHurtbox(
                        hurtboxesRoot,
                        UpperName,
                        damageable,
                        RegionCenter(bodyBounds, 0.81f),
                        RegionSize(bodyBounds, 0.52f, 0.22f));
                }

                EnemyThreeHurtboxRig rig = root.GetComponent<EnemyThreeHurtboxRig>();
                if (rig == null)
                {
                    rig = root.AddComponent<EnemyThreeHurtboxRig>();
                }

                rig.Configure(
                    stableBodies,
                    FindVisualRoot(root.transform),
                    lower,
                    middle,
                    upper);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Collider2D[] EnsureStableBodies(GameObject root, string prefabPath)
        {
            if (UsesPointedHatBodyProfile(prefabPath))
            {
                return ConfigurePointedHatWizardBodies(root);
            }

            Collider2D[] rootColliders = root.GetComponents<Collider2D>();
            // Capture any legacy traced shape while it is still enabled. Unity
            // reports empty bounds for disabled colliders, so doing this after
            // retiring an Edge/Polygon collider would create a tiny fallback.
            Rect legacyBounds = CalculateLegacyLocalBounds(root.transform, rootColliders);
            List<Collider2D> stableBodies = new List<Collider2D>(2);
            for (int index = 0; index < rootColliders.Length; index++)
            {
                Collider2D collider = rootColliders[index];
                if (collider == null || collider.isTrigger)
                {
                    continue;
                }

                if (IsSimpleBodyShape(collider))
                {
                    collider.enabled = true;
                    collider.gameObject.layer = DefaultLayer;
                    stableBodies.Add(collider);
                }
                else
                {
                    // Preserve the component for comparison, but retire sharp
                    // traced geometry after a simple replacement is authored.
                    collider.enabled = false;
                }
            }

            if (stableBodies.Count == 0)
            {
                CapsuleCollider2D replacement = root.GetComponent<CapsuleCollider2D>();
                if (replacement == null)
                {
                    replacement = root.AddComponent<CapsuleCollider2D>();
                }

                replacement.direction = CapsuleDirection2D.Vertical;
                replacement.offset = legacyBounds.center;
                replacement.size = new Vector2(
                    Mathf.Max(0.12f, legacyBounds.width),
                    Mathf.Max(0.18f, legacyBounds.height));
                replacement.isTrigger = false;
                replacement.enabled = true;
                root.layer = DefaultLayer;
                stableBodies.Add(replacement);
            }

            return stableBodies.ToArray();
        }

        // The regular Wizard's original traced outline spans its staff and its
        // entire tall cone hat. A single fallback capsule made that whole area
        // solid. Keep the lower mass compact and give the broad hat/head its
        // own rounded obstruction instead.
        private static bool UsesPointedHatBodyProfile(string prefabPath)
        {
            return prefabPath == "Assets/Cave/Prefabs/Mobs/Support/Wizard.prefab";
        }

        private static Collider2D[] ConfigurePointedHatWizardBodies(GameObject root)
        {
            Collider2D[] rootColliders = root.GetComponents<Collider2D>();
            for (int index = 0; index < rootColliders.Length; index++)
            {
                Collider2D collider = rootColliders[index];
                if (collider != null && !IsSimpleBodyShape(collider))
                {
                    // Keep the former pixel-traced outline serialized but out
                    // of physical collision. The two simple shapes below are
                    // the complete physical body for this profile.
                    collider.enabled = false;
                }
            }

            BoxCollider2D lowerBody = root.GetComponent<BoxCollider2D>();
            if (lowerBody == null)
            {
                lowerBody = root.AddComponent<BoxCollider2D>();
            }

            lowerBody.isTrigger = false;
            lowerBody.enabled = true;
            lowerBody.offset = new Vector2(-0.05f, 0.70f);
            lowerBody.size = new Vector2(1.00f, 1.35f);

            CapsuleCollider2D headAndHat = root.GetComponent<CapsuleCollider2D>();
            if (headAndHat == null)
            {
                headAndHat = root.AddComponent<CapsuleCollider2D>();
            }

            headAndHat.isTrigger = false;
            headAndHat.enabled = true;
            headAndHat.direction = CapsuleDirection2D.Vertical;
            headAndHat.offset = new Vector2(-0.08f, 2.15f);
            headAndHat.size = new Vector2(1.42f, 1.85f);
            root.layer = DefaultLayer;
            return new Collider2D[] { lowerBody, headAndHat };
        }

        private static bool IsSimpleBodyShape(Collider2D collider)
        {
            return collider is BoxCollider2D
                || collider is CapsuleCollider2D
                || collider is CircleCollider2D;
        }

        private static Rect CalculateLocalBodyBounds(Transform root, Collider2D[] bodies)
        {
            bool hasBounds = false;
            Rect combined = default;
            for (int index = 0; index < bodies.Length; index++)
            {
                Collider2D collider = bodies[index];
                if (collider == null)
                {
                    continue;
                }

                Rect bounds = LocalBounds(root, collider.bounds);
                combined = hasBounds ? Encapsulate(combined, bounds) : bounds;
                hasBounds = true;
            }

            return hasBounds ? combined : new Rect(-0.4f, -0.8f, 0.8f, 1.6f);
        }

        private static Rect CalculateLegacyLocalBounds(Transform root, Collider2D[] colliders)
        {
            bool hasBounds = false;
            Rect combined = default;
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider2D collider = colliders[index];
                if (collider == null)
                {
                    continue;
                }

                Rect bounds = LocalBounds(root, collider.bounds);
                combined = hasBounds ? Encapsulate(combined, bounds) : bounds;
                hasBounds = true;
            }

            return hasBounds ? combined : new Rect(-0.4f, -0.8f, 0.8f, 1.6f);
        }

        private static Rect LocalBounds(Transform root, Bounds worldBounds)
        {
            Vector3 min = root.InverseTransformPoint(worldBounds.min);
            Vector3 max = root.InverseTransformPoint(worldBounds.max);
            return Rect.MinMaxRect(
                Mathf.Min(min.x, max.x),
                Mathf.Min(min.y, max.y),
                Mathf.Max(min.x, max.x),
                Mathf.Max(min.y, max.y));
        }

        private static Rect Encapsulate(Rect first, Rect second)
        {
            return Rect.MinMaxRect(
                Mathf.Min(first.xMin, second.xMin),
                Mathf.Min(first.yMin, second.yMin),
                Mathf.Max(first.xMax, second.xMax),
                Mathf.Max(first.yMax, second.yMax));
        }

        private static Vector2 RegionCenter(Rect bodyBounds, float verticalFraction)
        {
            return new Vector2(bodyBounds.center.x, bodyBounds.yMin + bodyBounds.height * verticalFraction);
        }

        private static Vector2 RegionSize(Rect bodyBounds, float widthFraction, float heightFraction)
        {
            return new Vector2(
                Mathf.Max(0.08f, bodyBounds.width * widthFraction),
                Mathf.Max(0.08f, bodyBounds.height * heightFraction));
        }

        private static EnemyAnatomicalHurtbox ConfigureBoxHurtbox(
            Transform parent,
            string objectName,
            EnemyHurtboxRegion region,
            Damageable owner,
            Vector2 localPosition,
            Vector2 size)
        {
            Transform child = PrepareHurtboxChild(parent, objectName, localPosition);
            BoxCollider2D collider = child.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = child.gameObject.AddComponent<BoxCollider2D>();
            }

            collider.offset = Vector2.zero;
            collider.size = size;
            collider.isTrigger = true;
            return ConfigureHurtbox(child.gameObject, region, owner, collider);
        }

        private static EnemyAnatomicalHurtbox ConfigureCapsuleHurtbox(
            Transform parent,
            string objectName,
            Damageable owner,
            Vector2 localPosition,
            Vector2 size)
        {
            Transform child = PrepareHurtboxChild(parent, objectName, localPosition);
            CapsuleCollider2D collider = child.GetComponent<CapsuleCollider2D>();
            if (collider == null)
            {
                collider = child.gameObject.AddComponent<CapsuleCollider2D>();
            }

            collider.direction = CapsuleDirection2D.Vertical;
            collider.offset = Vector2.zero;
            collider.size = size;
            collider.isTrigger = true;
            return ConfigureHurtbox(child.gameObject, EnemyHurtboxRegion.Upper, owner, collider);
        }

        private static Transform PrepareHurtboxChild(Transform parent, string objectName, Vector2 localPosition)
        {
            Transform child = EnsureChild(parent, objectName);
            int damageableLayer = LayerMask.NameToLayer(DamageableLayerName);
            if (damageableLayer < 0)
            {
                throw new InvalidOperationException("The project Damageable layer is required for anatomical hurtboxes.");
            }

            child.gameObject.layer = damageableLayer;
            child.localPosition = localPosition;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            return child;
        }

        private static EnemyAnatomicalHurtbox ConfigureHurtbox(
            GameObject objectToConfigure,
            EnemyHurtboxRegion region,
            Damageable owner,
            Collider2D collider)
        {
            EnemyAnatomicalHurtbox hurtbox = objectToConfigure.GetComponent<EnemyAnatomicalHurtbox>();
            if (hurtbox == null)
            {
                hurtbox = objectToConfigure.AddComponent<EnemyAnatomicalHurtbox>();
            }

            hurtbox.Configure(region, owner, collider);
            return hurtbox;
        }

        private static Transform EnsureChild(Transform parent, string objectName)
        {
            Transform child = parent.Find(objectName);
            if (child != null)
            {
                return child;
            }

            GameObject created = new GameObject(objectName);
            created.transform.SetParent(parent, false);
            return created.transform;
        }

        private static bool TryGetExistingThreeHurtboxes(
            Transform parent,
            out EnemyAnatomicalHurtbox lower,
            out EnemyAnatomicalHurtbox middle,
            out EnemyAnatomicalHurtbox upper)
        {
            lower = FindHurtbox(parent, LowerName);
            middle = FindHurtbox(parent, MiddleName);
            upper = FindHurtbox(parent, UpperName);
            return lower != null && middle != null && upper != null;
        }

        private static EnemyAnatomicalHurtbox FindHurtbox(Transform parent, string objectName)
        {
            Transform child = parent.Find(objectName);
            return child != null ? child.GetComponent<EnemyAnatomicalHurtbox>() : null;
        }

        private static Transform FindVisualRoot(Transform root)
        {
            for (int index = 0; index < root.childCount; index++)
            {
                Transform child = root.GetChild(index);
                if (child.GetComponentInChildren<SpriteRenderer>(true) != null)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
