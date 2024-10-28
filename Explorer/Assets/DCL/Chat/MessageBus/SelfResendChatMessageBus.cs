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

        public event Action<ChatMessage>? MessageAdded;

        public SelfResendChatMessageBus(MultiplayerChatMessagesBus origin, IWeb3IdentityCache web3IdentityCache, IProfileRepository profileRepository)
        {
            this.origin = origin;
            this.web3IdentityCache = web3IdentityCache;
            this.profileRepository = profileRepository;
            this.origin.MessageAdded += OriginOnOnMessageAdded;
        }

        ~SelfResendChatMessageBus()
        {
            this.origin.MessageAdded -= OriginOnOnMessageAdded;
        }

        public void Dispose()
        {
            origin.Dispose();
        }

        private void OriginOnOnMessageAdded(ChatMessage obj)
        {
            MessageAdded?.Invoke(obj);
        }

        public void Send(string message, string origin)
        {
            this.origin.Send(message, origin);
            SendSelfAsync(message).Forget();
        }

        private async UniTaskVoid SendSelfAsync(string message)
        {
            var identity = web3IdentityCache.Identity;

            if (identity == null)
            {
                ReportHub.LogWarning(ReportCategory.COMMS_SCENE_HANDLER, "SelfResendChatMessageBus.Send: Identity is null, can't send message");
                return;
            }

            Profile? profile = await profileRepository.GetAsync(identity.Address, CancellationToken.None);

            MessageAdded?.Invoke(
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
