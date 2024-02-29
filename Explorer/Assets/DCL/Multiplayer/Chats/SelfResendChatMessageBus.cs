using DCL.Chat;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using System;

namespace DCL.Multiplayer.Chats
{
    public class SelfResendChatMessageBus : IChatMessagesBus
    {
        private readonly IChatMessagesBus origin;
        private readonly IWeb3IdentityCache web3IdentityCache;

        public event Action<ChatMessage>? OnMessageAdded;

        public SelfResendChatMessageBus(IChatMessagesBus origin, IWeb3IdentityCache web3IdentityCache)
        {
            this.origin = origin;
            this.web3IdentityCache = web3IdentityCache;
            this.origin.OnMessageAdded += OriginOnOnMessageAdded;
        }

        ~SelfResendChatMessageBus()
        {
            this.origin.OnMessageAdded -= OriginOnOnMessageAdded;
        }

        private void OriginOnOnMessageAdded(ChatMessage obj)
        {
            OnMessageAdded?.Invoke(obj);
        }

        public void Send(string message)
        {
            origin.Send(message);
            var identity = web3IdentityCache.Identity;

            if (identity == null)
            {
                ReportHub.LogWarning(ReportCategory.ARCHIPELAGO_REQUEST, "SelfResendChatMessageBus.Send: Identity is null, can't send message");
                return;
            }

            OnMessageAdded?.Invoke(
                new ChatMessage(
                    message,
                    identity.Address,
                    identity.Address,
                    true
                )
            );
        }
    }
}
