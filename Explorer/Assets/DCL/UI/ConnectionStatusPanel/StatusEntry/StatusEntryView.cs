using DCL.UI.ConnectionStatusPanel.Badge;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.ConnectionStatusPanel.StatusEntry
{
    public class StatusEntryView : MonoBehaviour, IStatusEntry
    {
        [SerializeField] private List<StatusValues> statusValues = new ();
        [Space]
        [SerializeField] private Button reloadButton = null!;
        [SerializeField] private BadgeView statusEntry = null!;
        [Header("Debug")]
        [SerializeField] private IStatusEntry.Status currentStatus;

        private Action? cachedAction;
        private IReadOnlyDictionary<IStatusEntry.Status, (string title, float predefinedWidth, Color text, Color background)>? cache;

        private IReadOnlyDictionary<IStatusEntry.Status, (string title, float predefinedWidth, Color text, Color background)> values
        {
            get
            {
                cache = statusValues
                   .ToDictionary(e => e.status, e => (e.status.ToString()!, e.predefinedWidth, e.textColor, e.backgroundColor));
                return cache;
            }
        }

        public void ShowReloadButton(Action onClick)
        {
            reloadButton.gameObject.SetActive(true);
            statusEntry.gameObject.SetActive(false);
            cachedAction = onClick;
        }

        public void ShowStatus(IStatusEntry.Status status)
        {
            reloadButton.gameObject.SetActive(false);
            statusEntry.gameObject.SetActive(true);
            (string title, float predefinedWidth, Color text, Color background) = values[status];
            statusEntry.UpdateText(title, predefinedWidth);
            statusEntry.ApplyColor(text, background);
        }

        private void Awake()
        {
            reloadButton.onClick!.AddListener(() => cachedAction?.Invoke());
        }

        [ContextMenu(nameof(UpdateStatus))]
        public void UpdateStatus()
        {
            ShowStatus(currentStatus);
        }

        [Serializable]
        public class StatusValues
        {
            public IStatusEntry.Status status;
            public float predefinedWidth;
            public Color textColor;
            public Color backgroundColor;
        }
    }
}
