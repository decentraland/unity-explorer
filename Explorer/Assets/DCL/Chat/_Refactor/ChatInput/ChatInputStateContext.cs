using DCL.Chat.ChatUseCases;
using DCL.Emoji;
using DCL.UI.Profiles.Helpers;
using System.Collections.Generic;
using Utility;

namespace DCL.Chat
{
    public readonly struct ChatInputStateContext
    {
        public readonly ChatInputView ChatInputView;
        public readonly GetParticipantProfilesCommand GetParticipantProfilesCommand;
        public readonly SendMessageCommand SendMessageCommand;
        public readonly ProfileRepositoryWrapper ProfileRepositoryWrapper;
        public readonly EmojiMapping EmojiMapping;
        public readonly IEventBus InputEventBus;
        public readonly IEventBus ChatEventBus;

        public ChatInputStateContext(ChatInputView chatInputView,
            IEventBus inputEventBus,
            IEventBus chatEventBus,
            GetParticipantProfilesCommand getParticipantProfilesCommand,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            SendMessageCommand sendMessageCommand,
            EmojiMapping emojiMapping)
        {
            ChatInputView = chatInputView;
            GetParticipantProfilesCommand = getParticipantProfilesCommand;
            ProfileRepositoryWrapper = profileRepositoryWrapper;
            InputEventBus = inputEventBus;
            SendMessageCommand = sendMessageCommand;
            EmojiMapping = emojiMapping;
            ChatEventBus = chatEventBus;
        }
    }
}
