using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunityCreation
{
    public class CommunityPlaceTag : MonoBehaviour
    {
        public Action? RemoveButtonClicked;

        [SerializeField] private TMP_Text tagText = null!;
        [SerializeField] private TMP_Text ownerNameText = null!;
        [SerializeField] private Button removeButton = null!;

        public string Id { get; private set; } = null!;
        public bool IsWorld { get; private set; }

        private void Awake() =>
            removeButton.onClick.AddListener(() => RemoveButtonClicked?.Invoke());

        private void OnDestroy() =>
            removeButton.onClick.RemoveAllListeners();

        public void Setup(string id, bool isWorld, string text, string ownerName, bool allowRemove)
        {
            Id = id;
            IsWorld = isWorld;
            tagText.text = text;
            ownerNameText.text = ownerName;
            removeButton.gameObject.SetActive(allowRemove);
        }
    }
}
