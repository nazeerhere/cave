using System.Collections.Generic;
using Cave.InputSystem;
using UnityEngine;

namespace Cave.Player
{
    [DisallowMultipleComponent]
    public sealed class CurseAltar : MonoBehaviour
    {
        private static readonly HashSet<CurseAltar> ActiveAltars = new HashSet<CurseAltar>();

        private PlayerCurseAltarController owner;
        private bool permanent;
        private float expiresAt;
        private Material runeMaterial;
        private LineRenderer rune;
        private TextMesh prompt;

        public bool IsPermanent => permanent;

        public static CurseAltar Create(
            PlayerCurseAltarController controller,
            Vector2 position,
            bool isPermanent,
            float lifetime)
        {
            GameObject altarObject = new GameObject(
                isPermanent ? "Permanent Curse Altar" : "Summoned Curse Altar");
            altarObject.transform.position = position;
            CurseAltar altar = altarObject.AddComponent<CurseAltar>();
            altar.owner = controller;
            altar.permanent = isPermanent;
            altar.expiresAt = isPermanent ? float.PositiveInfinity : Time.time + Mathf.Max(1f, lifetime);
            altar.BuildPresentation();
            return altar;
        }

        public static CurseAltar FindNearest(Vector2 position, float maximumDistance)
        {
            CurseAltar nearest = null;
            float bestDistanceSquared = maximumDistance * maximumDistance;
            foreach (CurseAltar altar in ActiveAltars)
            {
                if (altar == null || !altar.gameObject.activeInHierarchy)
                {
                    continue;
                }

                float distanceSquared = ((Vector2)altar.transform.position - position).sqrMagnitude;
                if (distanceSquared <= bestDistanceSquared)
                {
                    nearest = altar;
                    bestDistanceSquared = distanceSquared;
                }
            }

            return nearest;
        }

        public void ConsumeAfterBargain()
        {
            if (!permanent)
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            ActiveAltars.Add(this);
        }

        private void Update()
        {
            if (!permanent && Time.time >= expiresAt)
            {
                Destroy(gameObject);
                return;
            }

            if (rune != null)
            {
                float pulse = 0.78f + Mathf.Sin(Time.time * 2.4f) * 0.16f;
                Color color = new Color(0.55f, 0.2f, 1f, pulse);
                rune.startColor = color;
                rune.endColor = color;
            }

            if (prompt != null && Camera.main != null)
            {
                float distance = Vector2.Distance(transform.position, owner.transform.position);
                prompt.gameObject.SetActive(distance <= owner.InteractionRange);
                prompt.text = "[" + FormatInteractKey() + "] "
                    + (owner != null ? owner.GetContextualInteractLabel() : "BARGAIN");
            }
        }

        private void BuildPresentation()
        {
            runeMaterial = new Material(Shader.Find("Sprites/Default"));
            rune = gameObject.AddComponent<LineRenderer>();
            rune.material = runeMaterial;
            rune.useWorldSpace = false;
            rune.loop = true;
            rune.positionCount = 4;
            rune.startWidth = 0.09f;
            rune.endWidth = 0.09f;
            rune.sortingOrder = 12;
            rune.SetPosition(0, new Vector3(0f, 1.4f));
            rune.SetPosition(1, new Vector3(0.75f, 0.55f));
            rune.SetPosition(2, new Vector3(0f, 0f));
            rune.SetPosition(3, new Vector3(-0.75f, 0.55f));

            GameObject promptObject = new GameObject("Curse Altar Prompt");
            promptObject.transform.SetParent(transform, false);
            promptObject.transform.localPosition = new Vector3(0f, 1.72f, 0f);
            promptObject.transform.localScale = Vector3.one * 0.08f;
            prompt = promptObject.AddComponent<TextMesh>();
            prompt.anchor = TextAnchor.MiddleCenter;
            prompt.alignment = TextAlignment.Center;
            prompt.fontSize = 28;
            prompt.characterSize = 0.12f;
            prompt.color = new Color(1f, 0.82f, 0.42f, 1f);
            prompt.GetComponent<MeshRenderer>().sortingOrder = 13;
        }

        private static string FormatInteractKey()
        {
            KeyCode key = GameInput.Bindings.GetBinding(GameAction.Interact).Primary;
            return key == KeyCode.None ? "UNBOUND" : key.ToString().ToUpperInvariant();
        }

        private void OnDisable()
        {
            ActiveAltars.Remove(this);
            owner?.NotifyAltarRemoved(this);
        }

        private void OnDestroy()
        {
            ActiveAltars.Remove(this);
            if (runeMaterial != null)
            {
                Destroy(runeMaterial);
            }
        }
    }
}
