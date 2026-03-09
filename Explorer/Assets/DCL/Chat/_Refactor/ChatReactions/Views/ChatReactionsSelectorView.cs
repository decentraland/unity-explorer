using System;
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

        public RectTransform Container => container;
        public ChatReactionItemView ItemPrefab => reactionItemPrefab;
        public RectTransform AddButtonRect => (RectTransform)addButton.transform;

        public event Action? OnAddClicked;

        public void Show() => gameObject.SetActive(true);

        public void Hide() => gameObject.SetActive(false);

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
