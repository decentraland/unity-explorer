using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunityCreation
{
    public class CommunityPlaceTag : MonoBehaviour
    {
        public Action RemoveButtonClicked;

        [SerializeField] private TMP_Text tagText;
        [SerializeField] private Button removeButton;

        public string Id { get; private set; }
        public bool IsWorld { get; private set; }

        public string Text
        {
            get => tagText.text;
            private set => tagText.text = value;

        }

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
