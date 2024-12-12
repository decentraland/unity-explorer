using DCL.Chat.Commands;
using DCL.DebugUtilities;
using DCL.Profiles;
using DCL.Web3.Identities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Utility;

namespace DCL.Chat.MessageBus
{
    public interface IChatMessagesBus : IDisposable
    {
        public event Action<ChatMessage> MessageAdded;
        public void Send(string message, string origin);
    }

    public static class ChatMessageBusExtensions
    {
        public static IChatMessagesBus WithSelfResend(this MultiplayerChatMessagesBus messagesBus, IWeb3IdentityCache web3IdentityCache, IProfileRepository profileRepository) =>
            new SelfResendChatMessageBus(messagesBus, web3IdentityCache, profileRepository);

        public static IChatMessagesBus WithDebugPanel(this IChatMessagesBus messagesBus, IDebugContainerBuilder debugContainerBuilder)
        {
            void CreateTestChatEntry()
            {
                messagesBus.Send(StringUtils.GenerateRandomString(UnityEngine.Random.Range(1, 250)), "debug panel");
            }

            debugContainerBuilder.TryAddWidget("Chat")?.AddControl(new DebugButtonDef("Create chat message", CreateTestChatEntry), null!);

            return messagesBus;
        }

        public static IChatMessagesBus WithCommands(this IChatMessagesBus messagesBus, IReadOnlyList<IChatCommand> commands) =>
            new CommandsHandleChatMessageBus(messagesBus, commands);

        public static IChatMessagesBus WithIgnoreSymbols(this IChatMessagesBus messagesBus) =>
            new IgnoreWithSymbolsChatMessageBus(messagesBus);
    }
}
