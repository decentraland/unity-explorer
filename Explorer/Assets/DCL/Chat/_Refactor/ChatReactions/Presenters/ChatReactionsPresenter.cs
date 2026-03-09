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
        private readonly ChatReactionFavoritesService favoritesService;
        private readonly ChatReactionButtonView buttonView;
        private readonly ChatReactionsAtlasConfig atlasConfig;
        private readonly EmojiPanelPresenter emojiPanelPresenter;
        private readonly RectTransform emojiPanelRect;
        private readonly RectTransform addButtonRect;

        private bool emojiPanelOpenedByReactions;

        public ChatReactionsPresenter(
            ChatReactionButtonView buttonView,
            ChatReactionsSelectorView selectorView,
            ISituationalReactionService reactionService,
            ChatReactionsAtlasConfig atlasConfig,
            ChatReactionFavoritesService favoritesService,
            EmojiPanelView emojiPanelView,
            EmojiPanelPresenter emojiPanelPresenter)
        {
            this.buttonView = buttonView;
            this.emojiPanelPresenter = emojiPanelPresenter;
            this.favoritesService = favoritesService;
            this.atlasConfig = atlasConfig;

            emojiPanelRect = (RectTransform)emojiPanelView.transform;
            addButtonRect = selectorView.AddButtonRect;

            selectorPresenter = new ChatReactionsSelectorPresenter(
                selectorView,
                favoritesService,
                atlasConfig,
                selectorView.ItemPrefab);

            buttonPresenter = new ChatReactionButtonPresenter(buttonView, reactionService);
            buttonPresenter.HoldTriggered += OnHoldTriggered;
            buttonPresenter.PickerEmojiSelected += OnPickerEmojiSelected;

            selectorPresenter.ReactionClicked += OnSelectorReactionClicked;
            selectorPresenter.ReactionRemoved += OnSelectorReactionRemoved;
            selectorPresenter.AddClicked += OnAddClicked;

            int selected = favoritesService.SelectedIndex;

            if (selected < 0)
                favoritesService.TryGetFirstFavorite(out selected);

            if (selected >= 0)
            {
                buttonView.SetEmoji(selected, atlasConfig);
                buttonPresenter.SetSelectedEmoji(selected);
            }
        }

        private void OnHoldTriggered()
        {
            selectorPresenter.Show();
        }

        private void OnPickerEmojiSelected(int atlasIndex)
        {
            favoritesService.SetSelected(atlasIndex);
            buttonView.SetEmoji(atlasIndex, atlasConfig);
        }

        private void OnSelectorReactionClicked(int atlasIndex)
        {
            SelectEmoji(atlasIndex);
            HideEmojiPanel();
            selectorPresenter.Hide();
        }

        private void OnSelectorReactionRemoved(int atlasIndex)
        {
            favoritesService.TryGetFirstFavorite(out int fallback);
            SelectEmoji(fallback);
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
            emojiPanelRect.position = addButtonRect.position;
            emojiPanelPresenter.EmojiSelected += OnEmojiPanelEmojiSelected;
            emojiPanelPresenter.SetPanelVisibility(true);
            emojiPanelOpenedByReactions = true;
        }

        private void HideEmojiPanel()
        {
            if (!emojiPanelOpenedByReactions) return;

            emojiPanelPresenter.EmojiSelected -= OnEmojiPanelEmojiSelected;
            emojiPanelPresenter.SetPanelVisibility(false);
            emojiPanelOpenedByReactions = false;
        }

        private void OnEmojiPanelEmojiSelected(string emojiUnicode)
        {
            if (string.IsNullOrEmpty(emojiUnicode)) return;

            if (!TryGetSingleCodepoint(emojiUnicode, out uint codepoint)) return;

            int atlasIndex = atlasConfig.GetTileIndexFromUnicode(codepoint);
            if (atlasIndex < 0) return;

            selectorPresenter.AddReaction(atlasIndex);
            SelectEmoji(atlasIndex);
            HideEmojiPanel();
            selectorPresenter.Hide();
        }

        private static bool TryGetSingleCodepoint(string text, out uint codepoint)
        {
            codepoint = 0;
            if (text.Length == 0) return false;

            if (char.IsHighSurrogate(text[0]))
            {
                if (text.Length < 2 || !char.IsLowSurrogate(text[1])) return false;
                codepoint = (uint)char.ConvertToUtf32(text[0], text[1]);
            }
            else
            {
                codepoint = text[0];
            }

            return true;
        }

        private void SelectEmoji(int atlasIndex)
        {
            favoritesService.SetSelected(atlasIndex);
            buttonPresenter.SetSelectedEmoji(atlasIndex);

            if (atlasIndex >= 0)
                buttonView.SetEmoji(atlasIndex, atlasConfig);
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
            buttonPresenter.PickerEmojiSelected -= OnPickerEmojiSelected;
            selectorPresenter.ReactionClicked -= OnSelectorReactionClicked;
            selectorPresenter.ReactionRemoved -= OnSelectorReactionRemoved;
            selectorPresenter.AddClicked -= OnAddClicked;

            buttonPresenter.Dispose();
            selectorPresenter.Dispose();
        }
    }
}