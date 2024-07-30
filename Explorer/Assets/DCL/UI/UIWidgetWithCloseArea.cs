using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class UIWidgetWithCloseArea : MonoBehaviour
    {
        public event Action OnWidgetClosed;

        [field: SerializeField] internal Button closeAreaButton { get; private set;}
        [field: SerializeField] internal bool autoDeactivate { get; private set;}
        private void Awake()
        {
            closeAreaButton.onClick.AddListener(WidgetClosed);
        }

        private void WidgetClosed()
        {
            if (autoDeactivate) this.gameObject.SetActive(false);
            OnWidgetClosed?.Invoke();
        }
    }
}
