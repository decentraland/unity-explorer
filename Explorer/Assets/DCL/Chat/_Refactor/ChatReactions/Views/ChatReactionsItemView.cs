using System;
using DCL.Chat.ChatReactions.Configs;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Chat.ChatReactions
{
    public sealed class ChatReactionItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button selectButton;
        [SerializeField] private RawImage emojiImage;
        [SerializeField] private Button closeButton;

        public int AtlasIndex { get; private set; }

        public event Action<int>? OnClicked;
        public event Action<int>? OnCloseClicked;

        private bool listenersAttached;

        public void Initialize(int atlasIndex, ChatReactionsAtlasConfig atlasConfig)
        {
            AtlasIndex = atlasIndex;

            emojiImage.texture = atlasConfig.Atlas;
            emojiImage.uvRect = atlasConfig.GetUVRect(atlasIndex);

            if (!listenersAttached)
            {
                selectButton.onClick.AddListener(HandleClicked);

                if (closeButton != null)
                    closeButton.onClick.AddListener(HandleCloseClicked);

                listenersAttached = true;
            }

            HideCloseButton();
        }

        public void Show() => gameObject.SetActive(true);

        public void Hide() => gameObject.SetActive(false);

        public void ResetForPool()
        {
            OnClicked = null;
            OnCloseClicked = null;
            AtlasIndex = -1;
        }

        private void OnDestroy()
        {
            if (!listenersAttached) return;

            selectButton.onClick.RemoveListener(HandleClicked);

            if (closeButton != null)
                closeButton.onClick.RemoveListener(HandleCloseClicked);

            listenersAttached = false;
        }

        public void OnPointerEnter(PointerEventData eventData) => SetCloseButtonVisible(true);

        public void OnPointerExit(PointerEventData eventData) => SetCloseButtonVisible(false);

        public void HideCloseButton() => SetCloseButtonVisible(false);

        private void SetCloseButtonVisible(bool visible)
        {
            if (closeButton != null)
                closeButton.gameObject.SetActive(visible);
        }

        private void HandleClicked() => OnClicked?.Invoke(AtlasIndex);
        private void HandleCloseClicked() => OnCloseClicked?.Invoke(AtlasIndex);
    }
}