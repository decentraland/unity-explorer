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

        public event Action<ChatChannel.ChannelId, ChatMessage>? MessageAdded;

        public SelfResendChatMessageBus(MultiplayerChatMessagesBus origin, IWeb3IdentityCache web3IdentityCache, IProfileRepository profileRepository)
        {
            this.origin = origin;
            this.web3IdentityCache = web3IdentityCache;
            this.profileRepository = profileRepository;
            this.origin.MessageAdded += OriginOnOnMessageAdded;
        }

        ~SelfResendChatMessageBus()
        {
            origin.MessageAdded -= OriginOnOnMessageAdded;
        }

        public void Dispose()
        {
            origin.Dispose();
        }

        private void OriginOnOnMessageAdded(ChatChannel.ChannelId channelId, ChatMessage message)
        {
            MessageAdded?.Invoke(channelId, message);
        }

        public void Send(ChatChannel.ChannelId channelId, string message, string origin)
        {
            this.origin.Send(channelId, message, origin);
            SendSelfAsync(channelId, message).Forget();
        }

        private async UniTaskVoid SendSelfAsync(ChatChannel.ChannelId channelId, string message)
        {
            IWeb3Identity? identity = web3IdentityCache.Identity;

            if (identity == null)
            {
                ReportHub.LogWarning(ReportCategory.COMMS_SCENE_HANDLER, "SelfResendChatMessageBus.Send: Identity is null, can't send message");
                return;
            }

            Profile? profile = await profileRepository.GetAsync(identity.Address, CancellationToken.None);

            var isMention = false;

            if (profile != null)
            {
                string displayName = profile.MentionName;
                isMention = CheckMentionOnChatMessage(message, displayName);

                if (isMention)
                    message = AddStyleToUserMention(message, displayName);
            }

            MessageAdded?.Invoke(
                channelId,
                new ChatMessage(
                    message,
                    profile?.ValidatedName ?? string.Empty,
                    identity.Address,
                    true,
                    profile?.WalletId ?? null,
                    isMention: isMention
                )
            );
        }

        private bool CheckMentionOnChatMessage(string chatMessage, string displayName) =>
            chatMessage.Contains(displayName);


        //TODO FRAN: Make these values constants
        private string AddStyleToUserMention(string chatMessage, string userName) =>
            chatMessage.Replace($"<link=profile>{userName}</link>",$"<mark=#438FFF40>{userName}</mark>");

    }
}
