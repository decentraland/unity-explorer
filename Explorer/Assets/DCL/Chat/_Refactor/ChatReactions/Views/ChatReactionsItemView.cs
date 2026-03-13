using System;
using DCL.Chat.ChatReactions.Configs;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Individual emoji item in the shortcuts bar. Displays an emoji from the atlas
    /// and fires <see cref="OnClicked"/> when tapped.
    /// </summary>
    public sealed class ChatReactionItemView : MonoBehaviour
    {
        [SerializeField] private Button selectButton;
        [SerializeField] private RawImage emojiImage;

        public int AtlasIndex { get; private set; }

        public event Action<int>? OnClicked;

        private bool listenersAttached;

        public void Initialize(int atlasIndex, ChatReactionsAtlasConfig atlasConfig)
        {
            AtlasIndex = atlasIndex;

            emojiImage.texture = atlasConfig.Atlas;
            emojiImage.uvRect = atlasConfig.GetUVRect(atlasIndex);

            if (!listenersAttached)
            {
                selectButton.onClick.AddListener(HandleClicked);
                listenersAttached = true;
            }
        }

        public void Show() => gameObject.SetActive(true);

        public void Hide() => gameObject.SetActive(false);

        public void ResetForPool()
        {
            OnClicked = null;
            AtlasIndex = -1;
        }

        private void OnDestroy()
        {
            if (!listenersAttached) return;

            selectButton.onClick.RemoveListener(HandleClicked);
            listenersAttached = false;
        }

        private void HandleClicked() => OnClicked?.Invoke(AtlasIndex);
    }
}
