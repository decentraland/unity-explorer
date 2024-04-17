using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Web3.Identities;
using System;
using System.Threading;

namespace DCL.Chat.MessageBus
{
    public class SelfResendChatMessageBus : IChatMessagesBus
    {
        private readonly MultiplayerChatMessagesBus origin;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IProfileRepository profileRepository;

        public event Action<ChatMessage>? OnMessageAdded;

        public SelfResendChatMessageBus(MultiplayerChatMessagesBus origin, IWeb3IdentityCache web3IdentityCache, IProfileRepository profileRepository)
        {
            this.origin = origin;
            this.web3IdentityCache = web3IdentityCache;
            this.profileRepository = profileRepository;
            this.origin.OnMessageAdded += OriginOnOnMessageAdded;
        }

        ~SelfResendChatMessageBus()
        {
            this.origin.OnMessageAdded -= OriginOnOnMessageAdded;
        }

        public void Dispose()
        {
            origin.Dispose();
        }

        private void OriginOnOnMessageAdded(ChatMessage obj)
        {
            OnMessageAdded?.Invoke(obj);
        }

        public void Send(string message)
        {
            origin.Send(message);
            SendSelfAsync(message).Forget();
        }

        private async UniTaskVoid SendSelfAsync(string message)
        {
            var identity = web3IdentityCache.Identity;

            if (identity == null)
            {
                ReportHub.LogWarning(ReportCategory.ARCHIPELAGO_REQUEST, "SelfResendChatMessageBus.Send: Identity is null, can't send message");
                return;
            }

            var profile = await profileRepository.GetAsync(identity.Address, 0, CancellationToken.None);

            OnMessageAdded?.Invoke(
                new ChatMessage(
                    message,
                    profile?.DisplayName ?? string.Empty,
                    identity.Address,
                    true,
                    true
                )
            );
        }
    }
}
