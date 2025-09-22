using DCL.Chat.Commands;
using DCL.Chat.History;
using DCL.DebugUtilities;
using DCL.RealmNavigation;
using DCL.Web3.Identities;
using System;
using System.Collections.Generic;
using Utility;
using Random = UnityEngine.Random;

namespace DCL.Chat.MessageBus
{
    public interface IChatMessagesBus : IDisposable
    {
        public event Action<ChatChannel.ChannelId, ChatChannel.ChatChannelType, ChatMessage> MessageAdded;

        public void Send(ChatChannel channel, string message, ChatMessageOrigin origin, double timestamp, string topic = "");
    }

    public static class ChatMessageBusExtensions
    {
        public static IChatMessagesBus WithSelfResend(this MultiplayerChatMessagesBus messagesBus, IWeb3IdentityCache web3IdentityCache, ChatMessageFactory messageFactory) =>
            new SelfResendChatMessageBus(messagesBus, web3IdentityCache, messageFactory);

        public static IChatMessagesBus WithDebugPanel(this IChatMessagesBus messagesBus, IDebugContainerBuilder debugContainerBuilder)
        {
            void CreateTestChatEntry()
            {
                messagesBus.Send(ChatChannel.NEARBY_CHANNEL, StringUtils.GenerateRandomString(Random.Range(1, 250)), ChatMessageOrigin.DEBUG_PANEL, DateTime.UtcNow.ToOADate());
            }

            debugContainerBuilder.TryAddWidget("Chat")?.AddControl(new DebugButtonDef("Create chat message", CreateTestChatEntry), null!);

            return messagesBus;
        }

        public static IChatMessagesBus WithCommands(this IChatMessagesBus messagesBus, IReadOnlyList<IChatCommand> commands, ILoadingStatus loadingStatus) =>
            new CommandsHandleChatMessageBus(messagesBus, commands, loadingStatus);

        public static IChatMessagesBus WithIgnoreSymbols(this IChatMessagesBus messagesBus) =>
            new IgnoreWithSymbolsChatMessageBus(messagesBus);
    }
}
