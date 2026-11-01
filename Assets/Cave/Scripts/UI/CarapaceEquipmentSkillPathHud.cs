using System.Collections;
using Cave.Player;
using Cave.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace Cave.UI
{
    /// <summary>
    /// Legacy migration shim. The equipment presentation now lives in the
    /// Nexus Loadout page; this component removes an old runtime-created Skill
    /// Path row if one is present without affecting Carapace ownership.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerResonantCarapace))]
    public sealed class CarapaceEquipmentSkillPathHud : MonoBehaviour
    {
        private IEnumerator Start()
        {
            PlayerSpecialModeHud modeHud = null;
            for (int attempt = 0; attempt < 10 && modeHud == null; attempt++)
            {
                modeHud = FindObjectOfType<PlayerSpecialModeHud>(true);
                if (modeHud == null) yield return null;
            }

            if (modeHud == null || modeHud.SkillPathMount == null) yield break;
            Transform legacySlot = modeHud.SkillPathMount.Find("Carapace Equipment Slot");
            if (legacySlot != null) Destroy(legacySlot.gameObject);
        }
    }
}
