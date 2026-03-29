using System;
using DCL.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat.ChatReactions
{
    public class ChatReactionsSelectorView : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private RectTransform container;
        [SerializeField] private ChatReactionItemView reactionItemPrefab;
        [SerializeField] private Button addButton;
        [SerializeField] private GameObject divider;

        [Header("Options")]
        [SerializeField] private GameObject optionsArea;
        [SerializeField] private ToggleView showOthersToggle;

        public RectTransform Container => container;
        public ChatReactionItemView ItemPrefab => reactionItemPrefab;
        public RectTransform AddButtonRect => (RectTransform)addButton.transform;
        public GameObject Divider => divider;
        public ToggleView ShowOthersToggle => showOthersToggle;

        public RectTransform RectTransform => (RectTransform)transform;

        public event Action? OnAddClicked;

        public void Show() => gameObject.SetActive(true);

        public void Hide() => gameObject.SetActive(false);

        public void SetOptionsVisible(bool visible)
        {
            if (optionsArea != null)
                optionsArea.SetActive(visible);
        }

        private void Awake()
        {
            addButton.onClick.AddListener(HandleAddClicked);
        }

        private void OnDestroy()
        {
            addButton.onClick.RemoveListener(HandleAddClicked);
        }

        private void HandleAddClicked() => OnAddClicked?.Invoke();
    }
}
