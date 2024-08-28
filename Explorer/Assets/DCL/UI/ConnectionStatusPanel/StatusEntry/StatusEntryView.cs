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
        private IReadOnlyDictionary<string, (float predefinedWidth, Color text, Color background)>? cache;

        private IReadOnlyDictionary<string, (float predefinedWidth, Color text, Color background)> values
        {
            get
            {
                cache = statusValues
                   .ToDictionary(e => e.statusText, e => (e.predefinedWidth, e.textColor, e.backgroundColor));

                return cache;
            }
        }

        public void ShowReloadButton(Action onClick)
        {
            reloadButton.gameObject.SetActive(true);
            statusEntry.gameObject.SetActive(false);
            cachedAction = onClick;
        }

        public void ShowStatus(string status)
        {
            reloadButton.gameObject.SetActive(false);
            statusEntry.gameObject.SetActive(true);
            (float predefinedWidth, Color text, Color background) = values[status];
            statusEntry.UpdateText(status, predefinedWidth);
            statusEntry.ApplyColor(text, background);
        }

        public void HideStatus()
        {
            reloadButton.gameObject.SetActive(false);
            statusEntry.gameObject.SetActive(false);
        }

        private void Awake()
        {
            reloadButton.onClick!.AddListener(() => cachedAction?.Invoke());
        }

        [ContextMenu(nameof(UpdateStatus))]
        public void UpdateStatus()
        {
            ShowStatus(currentStatus.ToString()!);
        }

        [Serializable]
        public class StatusValues
        {
            public string statusText = string.Empty;
            public float predefinedWidth;
            public Color textColor;
            public Color backgroundColor;
        }
    }
}
