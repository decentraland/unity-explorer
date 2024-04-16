using DCL.Chat.MessageBus;
using DCL.DebugUtilities;
using DCL.Profiles;
using DCL.Web3.Identities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DCL.Chat
{
    public interface IChatMessagesBus : IDisposable
    {
        public event Action<ChatMessage> OnMessageAdded;

        public void Send(string message);
    }

    public static class ChatMessageBusExtensions
    {
        public static IChatMessagesBus WithSelfResend(this IChatMessagesBus messagesBus, IWeb3IdentityCache web3IdentityCache, IProfileRepository profileRepository) =>
            new SelfResendChatMessageBus(messagesBus, web3IdentityCache, profileRepository);

        public static IChatMessagesBus WithDebugPanel(this IChatMessagesBus messagesBus, IDebugContainerBuilder debugContainerBuilder) =>
            new DebugPanelChatMessageBus(messagesBus, debugContainerBuilder);

        public static IChatMessagesBus WithCommands(this IChatMessagesBus messagesBus, IReadOnlyDictionary<Regex, Func<IChatCommand>> commandsFactory) =>
            new CommandsHandleChatMessageBus(messagesBus, commandsFactory);
    }
}
