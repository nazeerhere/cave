using Cave.Player;
using UnityEngine;

namespace Cave.Missions
{
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class ExtractionPoint : MonoBehaviour
    {
        private bool activePoint;

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
            gameObject.SetActive(false);
        }

        public void Activate()
        {
            gameObject.SetActive(true);
            activePoint = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!activePoint || other.GetComponentInParent<PlayerHealth>() == null) return;
            activePoint = false;
            MissionRunContext.Current.SetState(MissionLifecycleState.Success);
        }

        public static void ActivateScenePoint(Component owner)
        {
            ExtractionPoint point = FindObjectOfType<ExtractionPoint>(true);
            if (point != null) point.Activate();
            else Debug.LogWarning("Mission requires extraction, but no ExtractionPoint is authored.", owner);
        }
    }
}
