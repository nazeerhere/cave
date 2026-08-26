using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Cave.UI
{
    [DisallowMultipleComponent]
    public sealed class HoldRepeatButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField, Min(0f)] private float initialRepeatDelay = 0.42f;
        [SerializeField, Min(0.02f)] private float repeatInterval = 0.11f;

        private Func<bool> purchase;
        private Action<bool> reportResult;
        private Coroutine repeatRoutine;

        public void Configure(Func<bool> purchaseAction, Action<bool> resultReporter)
        {
            purchase = purchaseAction;
            reportResult = resultReporter;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            StopRepeating();
            bool succeeded = TryPurchase();
            if (succeeded)
            {
                repeatRoutine = StartCoroutine(RepeatPurchases());
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            StopRepeating();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            StopRepeating();
        }

        private IEnumerator RepeatPurchases()
        {
            yield return new WaitForSecondsRealtime(initialRepeatDelay);
            while (TryPurchase())
            {
                yield return new WaitForSecondsRealtime(repeatInterval);
            }

            repeatRoutine = null;
        }

        private bool TryPurchase()
        {
            bool succeeded = purchase != null && purchase();
            reportResult?.Invoke(succeeded);
            return succeeded;
        }

        private void StopRepeating()
        {
            if (repeatRoutine == null)
            {
                return;
            }

            StopCoroutine(repeatRoutine);
            repeatRoutine = null;
        }

        private void OnDisable()
        {
            StopRepeating();
        }
    }
}
