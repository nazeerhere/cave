using System.Collections.Generic;
using Cave.Combat;
using UnityEngine;

namespace Cave.Enemies
{
    /// <summary>Presentation-only stage and damage selector for the approved crystal Tower.</summary>
    [DisallowMultipleComponent]
    public sealed class DetectiveTowerVisualState : MonoBehaviour
    {
        private const string ResourcePath = "Towers/DetectiveTowerCrystalSheet";
        private static readonly Dictionary<string, Sprite> SpritesByName = new Dictionary<string, Sprite>();
        private static bool loadAttempted;
        private static bool sourceWarningLogged;

        private Damageable health;
        private Renderer proceduralCore;
        private SpriteRenderer crystalRenderer;
        private int stageIndex;
        private int appliedStage = -1;
        private int appliedDamageBand = -1;
        private bool selectionWarningLogged;

        public int LoadedSpriteCount => SpritesByName.Count;
        public string SelectedSpriteName { get; private set; }
        public bool IsUsingCrystalVisual => crystalRenderer != null && crystalRenderer.enabled;

        public void Configure(Damageable towerHealth, Renderer fallbackCore, int initialStage)
        {
            if (health != towerHealth)
            {
                if (health != null)
                {
                    health.DamageResolved -= HandleDamageResolved;
                }

                health = towerHealth;
                if (health != null)
                {
                    health.DamageResolved += HandleDamageResolved;
                }
            }

            proceduralCore = fallbackCore;
            stageIndex = Mathf.Clamp(initialStage, 0, 3);
            EnsureRendererAndSprites();
            RefreshVisual(true);
        }

        public void SetStage(int stage)
        {
            stageIndex = Mathf.Clamp(stage, 0, 3);
            RefreshVisual(false);
        }

        private void RefreshVisual(bool force)
        {
            if (health == null)
            {
                return;
            }

            EnsureRendererAndSprites();
            int damageBand = ResolveDamageBand(health.CurrentHealth, health.MaximumHealth);
            if (!force && appliedStage == stageIndex && appliedDamageBand == damageBand)
            {
                return;
            }

            appliedStage = stageIndex;
            appliedDamageBand = damageBand;
            Sprite sprite = ResolveSprite(stageIndex, damageBand);
            SelectedSpriteName = sprite != null ? sprite.name : string.Empty;
            bool hasCrystal = sprite != null;
            if (crystalRenderer != null)
            {
                if (crystalRenderer.sprite != sprite)
                {
                    crystalRenderer.sprite = sprite;
                }

                crystalRenderer.enabled = hasCrystal;
            }

            if (proceduralCore != null)
            {
                proceduralCore.enabled = !hasCrystal;
            }
        }

        private void EnsureRendererAndSprites()
        {
            if (!loadAttempted)
            {
                loadAttempted = true;
                Sprite[] sprites = Resources.LoadAll<Sprite>(ResourcePath);
                for (int index = 0; index < sprites.Length; index++)
                {
                    Sprite sprite = sprites[index];
                    if (sprite != null)
                    {
                        SpritesByName[sprite.name] = sprite;
                    }
                }

                if (SpritesByName.Count != 16 && !sourceWarningLogged)
                {
                    sourceWarningLogged = true;
                    Debug.LogWarning(
                        "Detective Tower crystal art loaded " + SpritesByName.Count
                        + " of 16 sprites from Resources/" + ResourcePath
                        + "; the procedural core remains available as fallback.");
                }
            }

            if (SpritesByName.Count == 0 || crystalRenderer != null)
            {
                return;
            }

            Transform existing = transform.Find("Tower Crystal Visual");
            GameObject visual = existing != null
                ? existing.gameObject
                : new GameObject("Tower Crystal Visual", typeof(SpriteRenderer));
            if (existing == null)
            {
                visual.transform.SetParent(transform, false);
            }

            crystalRenderer = visual.GetComponent<SpriteRenderer>();
            crystalRenderer.sortingLayerID = proceduralCore != null ? proceduralCore.sortingLayerID : 0;
            crystalRenderer.sortingOrder = proceduralCore != null ? proceduralCore.sortingOrder + 1 : 6;
            crystalRenderer.color = Color.white;
        }

        private Sprite ResolveSprite(int stage, int damageBand)
        {
            string name = "Tower_Stage" + (stage + 1).ToString("00")
                + "_Damage" + (damageBand + 1).ToString("00");
            SpritesByName.TryGetValue(name, out Sprite sprite);
            if (sprite == null && !selectionWarningLogged)
            {
                selectionWarningLogged = true;
                Debug.LogWarning("Detective Tower crystal sprite is missing: " + name + ".", this);
            }

            return sprite;
        }

        private static int ResolveDamageBand(int current, int maximum)
        {
            float fraction = maximum > 0 ? Mathf.Clamp01(current / (float)maximum) : 0f;
            if (fraction > 0.75f)
            {
                return 0;
            }

            if (fraction > 0.50f)
            {
                return 1;
            }

            return fraction > 0.25f ? 2 : 3;
        }

        private void HandleDamageResolved(DamageContext _, bool wasBlocked, int appliedDamage)
        {
            if (!wasBlocked && appliedDamage > 0)
            {
                RefreshVisual(false);
            }
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.DamageResolved -= HandleDamageResolved;
            }
        }
    }
}
