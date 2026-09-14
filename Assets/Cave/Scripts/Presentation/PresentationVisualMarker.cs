using UnityEngine;

namespace Cave.Presentation
{
    /// <summary>
    /// Documents a prefab as appearance-only. These prefabs intentionally carry
    /// no collider, Rigidbody2D, damage, AI, or field-geometry authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PresentationVisualMarker : MonoBehaviour
    {
        [SerializeField, TextArea] private string editorNote =
            "Presentation only. Keep gameplay colliders and combat scripts on the owning runtime root.";
    }
}
