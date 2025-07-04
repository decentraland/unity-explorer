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
        [SerializeField] private Button removeButton = null!;

        public string Id { get; private set; } = null!;
        public bool IsWorld { get; private set; }

        public string Text { set => tagText.text = value; }

        private void Awake() =>
            removeButton.onClick.AddListener(() => RemoveButtonClicked?.Invoke());

        private void OnDestroy() =>
            removeButton.onClick.RemoveAllListeners();

        public void Setup(string id, bool isWorld, string text, bool allowRemove)
        {
            Id = id;
            IsWorld = isWorld;
            Text = text;
            removeButton.gameObject.SetActive(allowRemove);
        }
    }
}
