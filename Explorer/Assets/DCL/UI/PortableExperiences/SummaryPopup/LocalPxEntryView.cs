using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.PortableExperiences.SummaryPopup
{
    public class LocalPxEntryView : MonoBehaviour
    {
        [field: SerializeField]
        internal TMP_Text pxName = null!;

        [field: SerializeField]
        internal Button removeButton = null!;

        public Action<string>? RemoveRequested;

        private string currentId = string.Empty;

        public void Configure(string id)
        {
            currentId = id;
            pxName.text = id;
        }

        public void SetRemoveInteractable(bool interactable) =>
            removeButton.interactable = interactable;

        private void Awake()
        {
            removeButton.onClick.AddListener(() => RemoveRequested?.Invoke(currentId));
        }
    }
}
