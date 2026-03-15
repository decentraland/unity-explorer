using System;
using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.ChatServices;
using DCL.Emoji;
using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Top-level presenter that coordinates the reaction button, shortcuts bar,
    /// and emoji panel. Button click toggles the bar. Clicking any emoji in the
    /// bar or panel sends it immediately without closing panels.
    /// </summary>
    public sealed class ChatReactionsPresenter : IDisposable
    {
        private readonly ChatReactionButtonPresenter buttonPresenter;
        private readonly ChatReactionsSelectorPresenter selectorPresenter;
        private readonly ISituationalReactionService reactionService;
        private readonly ChatReactionsAtlasConfig atlasConfig;
        private readonly EmojiPanelPresenter emojiPanelPresenter;
        private readonly ChatClickDetectionHandler clickDetectionHandler;
        private readonly RectTransform emojiPanelRect;
        private readonly RectTransform addButtonRect;
        private readonly RectTransform buttonRect;

        private bool emojiPanelOpenedByReactions;

        public ChatReactionsPresenter(
            ChatReactionButtonView buttonView,
            ChatReactionsSelectorView selectorView,
            ISituationalReactionService reactionService,
            ChatReactionsAtlasConfig atlasConfig,
            ChatReactionRecentsService recentsService,
            int[] fixedDefaults,
            EmojiPanelView emojiPanelView,
            EmojiPanelPresenter emojiPanelPresenter)
        {
            this.reactionService = reactionService;
            this.emojiPanelPresenter = emojiPanelPresenter;
            this.atlasConfig = atlasConfig;

            emojiPanelRect = (RectTransform)emojiPanelView.transform;
            addButtonRect = selectorView.AddButtonRect;

            selectorPresenter = new ChatReactionsSelectorPresenter(
                selectorView,
                recentsService,
                atlasConfig,
                selectorView.ItemPrefab,
                fixedDefaults);

            buttonPresenter = new ChatReactionButtonPresenter(buttonView);
            buttonRect = buttonPresenter.ButtonRect;

            // Click-outside detection: target is the selector, ignore the button and emoji panel.
            clickDetectionHandler = new ChatClickDetectionHandler(
                selectorView.transform,
                buttonView.transform,
                emojiPanelView.transform);

            clickDetectionHandler.OnClickOutside += OnClickOutside;
            clickDetectionHandler.Pause();

            buttonPresenter.ButtonClicked += OnButtonClicked;
            selectorPresenter.ReactionClicked += OnSelectorReactionClicked;
            selectorPresenter.AddClicked += OnAddClicked;
        }

        private void OnButtonClicked()
        {
            if (selectorPresenter.IsVisible)
            {
                HideBar();
            }
            else
            {
                selectorPresenter.Show();
                clickDetectionHandler.Resume();
            }
        }

        private void OnSelectorReactionClicked(int atlasIndex)
        {
            // Send the reaction immediately — do NOT close the bar.
            // Only record usage; the bar refreshes next time it opens to avoid
            // visually shuffling icons while the user is actively clicking.
            reactionService.TriggerUIReactionFromRect(buttonRect, atlasIndex, count: 1);
            selectorPresenter.RecordUsage(atlasIndex);
        }

        private void OnClickOutside()
        {
            HideBar();
        }

        private void HideBar()
        {
            HideEmojiPanel();
            selectorPresenter.Hide();
            clickDetectionHandler.Pause();
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

            // Send the reaction immediately — do NOT close panels.
            reactionService.TriggerUIReactionFromRect(buttonRect, atlasIndex, count: 1);
            selectorPresenter.RecordUsage(atlasIndex);
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

        public void Show()
        {
            HideBar();
            buttonPresenter.Show();
        }

        public void Hide()
        {
            HideBar();
            buttonPresenter.Hide();
        }

        public void Dispose()
        {
            HideEmojiPanel();
            clickDetectionHandler.OnClickOutside -= OnClickOutside;
            clickDetectionHandler.Dispose();
            buttonPresenter.ButtonClicked -= OnButtonClicked;
            selectorPresenter.ReactionClicked -= OnSelectorReactionClicked;
            selectorPresenter.AddClicked -= OnAddClicked;

            buttonPresenter.Dispose();
            selectorPresenter.Dispose();
        }
    }
}
