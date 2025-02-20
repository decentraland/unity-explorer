using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Chat.MessageBus.Deduplication;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Profiles;
using DCL.Profiles.Self;
using LiveKit.Proto;
using System;
using System.Text.RegularExpressions;
using System.Threading;

namespace DCL.Chat.MessageBus
{
    public class MultiplayerChatMessagesBus : IChatMessagesBus
    {
        private static readonly Regex USERNAME_REGEX = new (@"(?<=^|\s)@([A-Za-z0-9]{3,15}(?:#[A-Za-z0-9]{4})?)(?=\s|$)", RegexOptions.Compiled);

        private readonly IMessagePipesHub messagePipesHub;
        private readonly IProfileRepository profileRepository;
        private readonly IMessageDeduplication<double> messageDeduplication;
        private readonly CancellationTokenSource cancellationTokenSource = new ();
        private readonly ISelfProfile selfProfile;

        public event Action<ChatChannel.ChannelId, ChatMessage>? MessageAdded;

        public MultiplayerChatMessagesBus(IMessagePipesHub messagePipesHub, IProfileRepository profileRepository, ISelfProfile selfProfile, IMessageDeduplication<double> messageDeduplication)
        {
            this.messagePipesHub = messagePipesHub;
            this.profileRepository = profileRepository;
            this.messageDeduplication = messageDeduplication;
            this.selfProfile = selfProfile;

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
                if (messageDeduplication.TryPass(receivedMessage.FromWalletId, receivedMessage.Payload.Timestamp) == false)
                    return;

                Profile? profile = await profileRepository.GetAsync(receivedMessage.FromWalletId, cancellationTokenSource.Token);

                ChatChannel.ChannelId parsedChannelId = ParseChatChannelIdFromPayloadMessage(receivedMessage.Payload.Message);
                string chatMessage = ParseChatMessageFromPayloadMessage(receivedMessage.Payload.Message);

                Profile? ownProfile = await selfProfile.ProfileAsync(cancellationTokenSource.Token);

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

        private ChatChannel.ChannelId ParseChatChannelIdFromPayloadMessage(string payloadMessage)
        {
            // TODO: Remove this line once this code is merged to dev
            if (false)
            {
                string channelId = payloadMessage.Substring(1, payloadMessage.IndexOf('>'));
                ChatChannel.ChannelId.GetTypeAndNameFromId(channelId, out ChatChannel.ChatChannelType parsedChannelType, out string channelIdName);
                return new ChatChannel.ChannelId(parsedChannelType, channelIdName);
            }
            else

                // TODO: Remove this line once this code is merged to dev
                return ChatChannel.NEARBY_CHANNEL;
        }

        private bool IsMention(string chatMessage, string userName)
        {
            foreach (Match match in USERNAME_REGEX.Matches(chatMessage))
            {
                if (match.Value == userName)
                    return true;
            }

            return false;
        }

        private string ParseChatMessageFromPayloadMessage(string payloadMessage) => payloadMessage;
            // payloadMessage.Substring(payloadMessage.IndexOf('>') + 1); TODO: Use this line when private conversations are implemented

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
            chat.Payload.Message = BuildChatChannelMessage(channelId, message);
            chat.Payload.Timestamp = timestamp;
            chat.SendAndDisposeAsync(cancellationTokenSource.Token, DataPacketKind.KindReliable).Forget();
        }

        private string BuildChatChannelMessage(ChatChannel.ChannelId channelId, string message)
        {
            return message;

            // TODO: Remove this line once this code is merged to dev
            channelId = ChatChannel.NEARBY_CHANNEL;
            return $"<{channelId.Id}>{message}"; // TODO: Use this when private conversations are implemented
        }
    }
}
