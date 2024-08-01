using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class UIWidgetWithCloseArea : MonoBehaviour
    {
        [field: SerializeField] internal Button closeAreaButton { get; private set; }
        [field: SerializeField] internal bool autoDeactivate { get; private set; }

        private void Awake()
        {
            closeAreaButton.onClick.AddListener(WidgetClosed);
        }

        public event Action OnWidgetClosed;

        public void CloseWidget()
        {
            WidgetClosed();
        }

        private void WidgetClosed()
        {
            if (autoDeactivate) gameObject.SetActive(false);
            OnWidgetClosed?.Invoke();
        }
    }
}
