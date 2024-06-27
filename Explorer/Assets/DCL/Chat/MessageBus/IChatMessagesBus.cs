using DCL.Chat.MessageBus;
using DCL.DebugUtilities;
using DCL.Profiles;
using DCL.Web3.Identities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Utility;

namespace DCL.Chat
{
    public interface IChatMessagesBus : IDisposable
    {
        public event Action<ChatMessage> OnMessageAdded;
        public event Action<string> MessageSent;
        public void Send(string message);
    }

    public static class ChatMessageBusExtensions
    {
        public static IChatMessagesBus WithSelfResend(this MultiplayerChatMessagesBus messagesBus, IWeb3IdentityCache web3IdentityCache, IProfileRepository profileRepository) =>
            new SelfResendChatMessageBus(messagesBus, web3IdentityCache, profileRepository);

        public static IChatMessagesBus WithDebugPanel(this IChatMessagesBus messagesBus, IDebugContainerBuilder debugContainerBuilder)
        {
            void CreateTestChatEntry()
            {
                messagesBus.Send(StringUtils.GenerateRandomString(UnityEngine.Random.Range(1, 250)));
            }

            debugContainerBuilder.AddWidget("Chat")!.AddControl(new DebugButtonDef("Create chat message", CreateTestChatEntry), null!);

            return messagesBus;
        }

        public static IChatMessagesBus WithCommands(this IChatMessagesBus messagesBus, IReadOnlyDictionary<Regex, Func<IChatCommand>> commandsFactory) =>
            new CommandsHandleChatMessageBus(messagesBus, commandsFactory);

        public static IChatMessagesBus WithIgnoreSymbols(this IChatMessagesBus messagesBus) =>
            new IgnoreWithSymbolsChatMessageBus(messagesBus);
    }
}
