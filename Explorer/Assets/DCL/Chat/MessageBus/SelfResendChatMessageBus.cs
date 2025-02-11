using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Web3.Identities;
using System;
using System.Text;
using System.Threading;

namespace DCL.Chat.MessageBus
{
    public class SelfResendChatMessageBus : IChatMessagesBus
    {
        private const string LINK_PROFILE_OPENING = "<link=profile>";
        private const string LINK_PROFILE_CLOSING = "</link>";
        private const string MARK_OPENING = "<mark=#438FFF40>";
        private const string MARK_CLOSING = "</mark>";

        private readonly MultiplayerChatMessagesBus origin;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IProfileRepository profileRepository;
        private readonly StringBuilder sb = new ();

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

        private async UniTaskVoid SendSelfAsync(ChatChannel.ChannelId channelId, string chatMessage)
        {
            IWeb3Identity? identity = web3IdentityCache.Identity;

            if (identity == null)
            {
                ReportHub.LogWarning(ReportCategory.COMMS_SCENE_HANDLER, "SelfResendChatMessageBus.Send: Identity is null, can't send message");
                return;
            }

            Profile? ownProfile = await profileRepository.GetAsync(identity.Address, CancellationToken.None);

            var isMention = false;

            if (ownProfile != null)
                isMention = TryChangeUserMentionStyle(ref chatMessage, ownProfile.MentionName);

            MessageAdded?.Invoke(
                channelId,
                new ChatMessage(
                    chatMessage,
                    ownProfile?.ValidatedName ?? string.Empty,
                    identity.Address,
                    true,
                    ownProfile?.WalletId ?? null,
                    isMention
                )
            );
        }

        private bool TryChangeUserMentionStyle(ref string chatMessage, string userName)
        {
            bool contains = chatMessage.Contains(userName, StringComparison.Ordinal);

            if (!contains) return false;

            chatMessage = chatMessage.Replace($"{LINK_PROFILE_OPENING}{userName}{LINK_PROFILE_CLOSING}", $"{MARK_OPENING}{userName}{MARK_CLOSING}");

            return true;
        }
    }
}
