using DCL.UI.SceneDebugConsole.LogHistory;
using DCL.UI.Utilities;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.SceneDebugConsole
{
    public class SceneDebugConsoleLogViewerElement : MonoBehaviour
    {
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Transform logEntriesParent;
        [SerializeField] private GameObject logEntryPrefab;

        private PooledConsoleLogEntriesList pooledConsoleLogEntriesList;
        private IReadOnlyList<SceneDebugConsoleLogEntry> logEntries;
        private bool needsUiUpdate = false;

        public bool IsScrollAtBottom => scrollRect.normalizedPosition.y <= 0.001f;
        public bool IsScrollAtTop => scrollRect.normalizedPosition.y >= 0.999f;

        public void Initialize(IReadOnlyList<SceneDebugConsoleLogEntry> logEntries)
        {
            this.logEntries = logEntries;
            scrollRect.SetScrollSensitivityBasedOnPlatform();

            pooledConsoleLogEntriesList = new PooledConsoleLogEntriesList(
                logEntries,
                logEntriesParent,
                logEntryPrefab
            );
        }

        public void OnLogEntryAdded()
        {
            needsUiUpdate = true;
        }

        private void Update()
        {
            if (needsUiUpdate)
            {
                pooledConsoleLogEntriesList.ConfigureUiItem(logEntries.Count - 1);
                needsUiUpdate = false;

                ShowLastLogEntry();
            }
        }

        public void ShowLastLogEntry()
        {
            scrollRect.verticalNormalizedPosition = 0;
        }
    }
}
