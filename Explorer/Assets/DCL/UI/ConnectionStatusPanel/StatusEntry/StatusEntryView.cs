using MVC;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.ConnectionStatusPanel.StatusEntry
{
    public class StatusEntryView : ViewBase, IView, IStatusEntry
    {
        [SerializeField] private Button reloadButton = null!;
        [SerializeField] private GameObject statusEntry = null!;

        private Action? cachedAction;

        public void ShowReloadButton(Action onClick)
        {
            reloadButton.gameObject.SetActive(true);
            statusEntry.SetActive(false);
            cachedAction = onClick;
        }

        public void ShowStatus(IStatusEntry.Status status)
        {
            reloadButton.gameObject.SetActive(false);
            statusEntry.SetActive(true);

            //TODO update text
        }

        private void Awake()
        {
            reloadButton.onClick!.AddListener(() => cachedAction?.Invoke());
            reloadButton.gameObject.SetActive(false);
            statusEntry.SetActive(false);
        }
    }
}
