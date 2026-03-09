using System;
using DCL.Chat.ChatReactions.Configs;
using DCL.Emoji;
using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Top-level presenter that owns and coordinates <see cref="ChatReactionButtonPresenter"/>
    /// and <see cref="ChatReactionsSelectorPresenter"/>.
    /// </summary>
    public sealed class ChatReactionsPresenter : IDisposable
    {
        private readonly ChatReactionButtonPresenter buttonPresenter;
        private readonly ChatReactionsSelectorPresenter selectorPresenter;
        private readonly ChatReactionButtonView buttonView;
        private readonly ChatReactionsAtlasConfig atlasConfig;
        private readonly EmojiPanelView emojiPanelView;
        private readonly RectTransform emojiPanelRect;
        private readonly RectTransform addButtonRect;

        private Vector3 originalPanelPosition;
        private bool emojiPanelOpenedByReactions;

        public ChatReactionsPresenter(
            ChatReactionButtonView buttonView,
            ChatReactionsSelectorView selectorView,
            ISituationalReactionService reactionService,
            ChatReactionsConfig reactionsConfig,
            EmojiPanelView emojiPanelView)
        {
            this.buttonView = buttonView;
            this.emojiPanelView = emojiPanelView;

            emojiPanelRect = (RectTransform)emojiPanelView.transform;
            addButtonRect = selectorView.AddButtonRect;

            var messageConfig = reactionsConfig.MessageReactions;
            atlasConfig = reactionsConfig.SituationalReactions.Atlas;

            var favoritesService = new ChatReactionFavoritesService(messageConfig.DefaultFavoriteEmojiIndices);

            selectorPresenter = new ChatReactionsSelectorPresenter(
                selectorView,
                favoritesService,
                atlasConfig);

            buttonPresenter = new ChatReactionButtonPresenter(buttonView, reactionService);
            buttonPresenter.HoldTriggered += OnHoldTriggered;

            selectorPresenter.ReactionClicked += OnSelectorReactionClicked;
            selectorPresenter.AddClicked += OnAddClicked;

            // Set initial button icon from first favorite (if any)
            if (favoritesService.Favorites.Count > 0)
                buttonView.SetEmoji(favoritesService.Favorites[0], atlasConfig);
        }

        private void OnHoldTriggered()
        {
            selectorPresenter.Show();
        }

        private void OnSelectorReactionClicked(int atlasIndex)
        {
            buttonView.SetEmoji(atlasIndex, atlasConfig);
            HideEmojiPanel();
            selectorPresenter.Hide();
        }

        private void OnAddClicked()
        {
            if (emojiPanelOpenedByReactions)
            {
                HideEmojiPanel();
                return;
            }

            ShowEmojiPanel();
        }

        private void ShowEmojiPanel()
        {
            originalPanelPosition = emojiPanelRect.position;
            emojiPanelRect.position = addButtonRect.position;
            emojiPanelView.gameObject.SetActive(true);
            emojiPanelView.EmojiContainer.gameObject.SetActive(true);
            emojiPanelOpenedByReactions = true;
        }

        private void HideEmojiPanel()
        {
            if (!emojiPanelOpenedByReactions) return;

            emojiPanelView.gameObject.SetActive(false);
            emojiPanelView.EmojiContainer.gameObject.SetActive(false);
            emojiPanelRect.position = originalPanelPosition;
            emojiPanelOpenedByReactions = false;
        }

        public void Show()
        {
            HideEmojiPanel();
            buttonPresenter.Show();
            selectorPresenter.Hide();
        }

        public void Hide()
        {
            HideEmojiPanel();
            buttonPresenter.Hide();
            selectorPresenter.Hide();
        }

        public void Dispose()
        {
            HideEmojiPanel();
            buttonPresenter.HoldTriggered -= OnHoldTriggered;
            selectorPresenter.ReactionClicked -= OnSelectorReactionClicked;
            selectorPresenter.AddClicked -= OnAddClicked;

            buttonPresenter.Dispose();
            selectorPresenter.Dispose();
        }
    }
}