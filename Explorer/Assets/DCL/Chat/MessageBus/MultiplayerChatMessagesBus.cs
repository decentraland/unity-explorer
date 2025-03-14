using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Chat.MessageBus.Deduplication;
using DCL.Friends.UserBlocking;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Utilities;
using LiveKit.Proto;
using System;
using System.Text.RegularExpressions;
using System.Threading;

namespace DCL.Chat.MessageBus
{
    public class MultiplayerChatMessagesBus : IChatMessagesBus
    {
        private static readonly Regex USERNAME_REGEX = new (@"(?<=^|\s)@([A-Za-z0-9]{3,15}(?:#[A-Za-z0-9]{4})?)(?=\s|!|\?|\.|,|$)", RegexOptions.Compiled);

        private readonly IMessagePipesHub messagePipesHub;
        private readonly IProfileRepository profileRepository;
        private readonly IMessageDeduplication<double> messageDeduplication;
        private readonly CancellationTokenSource cancellationTokenSource = new ();
        private readonly ISelfProfile selfProfile;
        private readonly ObjectProxy<IUserBlockingCache> userBlockingCacheProxy;

        public event Action<ChatChannel.ChannelId, ChatMessage>? MessageAdded;

        public MultiplayerChatMessagesBus(IMessagePipesHub messagePipesHub,
            IProfileRepository profileRepository,
            ISelfProfile selfProfile,
            IMessageDeduplication<double> messageDeduplication,
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy)
        {
            this.messagePipesHub = messagePipesHub;
            this.profileRepository = profileRepository;
            this.messageDeduplication = messageDeduplication;
            this.selfProfile = selfProfile;
            this.userBlockingCacheProxy = userBlockingCacheProxy;

            messagePipesHub.IslandPipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Chat>(Decentraland.Kernel.Comms.Rfc4.Packet.MessageOneofCase.Chat, OnMessageReceived);
            messagePipesHub.ScenePipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Chat>(Decentraland.Kernel.Comms.Rfc4.Packet.MessageOneofCase.Chat, OnMessageReceived);
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        private void OnMessageReceived(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Chat> receivedMessage)
        {
            OnChatAsync(receivedMessage).Forget();
        }

        private async UniTaskVoid OnChatAsync(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Chat> receivedMessage)
        {
            using (receivedMessage)
            {
                if (messageDeduplication.TryPass(receivedMessage.FromWalletId, receivedMessage.Payload.Timestamp) == false
                    || IsUserBlockedAndMessagesHidden(receivedMessage.FromWalletId))
                    return;

                Profile profile = await profileRepository.GetAsync(receivedMessage.FromWalletId, cancellationTokenSource.Token);

                ChatChannel.ChannelId parsedChannelId = ChatChannel.NEARBY_CHANNEL;
                string chatMessage = receivedMessage.Payload.Message;

                Profile ownProfile = await selfProfile.ProfileAsync(cancellationTokenSource.Token);

                var isMention = false;

                if (ownProfile != null)
                    isMention = IsMention(chatMessage, ownProfile.MentionName);

                MessageAdded?.Invoke(
                    parsedChannelId,
                    new ChatMessage(
                        chatMessage,
                        profile?.ValidatedName ?? string.Empty,
                        receivedMessage.FromWalletId,
                        false,
                        profile?.WalletId ?? null,
                        isMention
                    )
                );
            }
        }

        private bool IsUserBlockedAndMessagesHidden(string walletAddress) =>
            userBlockingCacheProxy.Configured && userBlockingCacheProxy.Object!.HideChatMessages && userBlockingCacheProxy.Object!.UserIsBlocked(walletAddress);

        private bool IsMention(string chatMessage, string userName)
        {
            foreach (Match match in USERNAME_REGEX.Matches(chatMessage))
            {
                if (match.Value == userName)
                    return true;
            }
            return false;
        }

        public void Send(ChatChannel.ChannelId channelId, string message, string origin)
        {
            if (cancellationTokenSource.IsCancellationRequested)
                throw new Exception("ChatMessagesBus is disposed");

            double timestamp = DateTime.UtcNow.TimeOfDay.TotalSeconds;
            SendTo(channelId, message, timestamp, messagePipesHub.IslandPipe());
            SendTo(channelId, message, timestamp, messagePipesHub.ScenePipe());
        }

        private void SendTo(ChatChannel.ChannelId channelId, string message, double timestamp, IMessagePipe messagePipe)
        {
            MessageWrap<Decentraland.Kernel.Comms.Rfc4.Chat> chat = messagePipe.NewMessage<Decentraland.Kernel.Comms.Rfc4.Chat>();
            chat.Payload.Message = message;
            chat.Payload.Timestamp = timestamp;
            chat.SendAndDisposeAsync(cancellationTokenSource.Token, DataPacketKind.KindReliable).Forget();
        }
    }
}
