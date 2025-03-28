#nullable enable
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
        private static readonly Regex USERNAME_REGEX = new (@"(?<=^|\s)@([A-Za-z0-9]{3,15}(?:#[A-Za-z0-9]{4})?)(?=\s|!|\?|\.|,|$)", RegexOptions.Compiled);

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
            messagePipesHub.ChatPipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Chat>(Decentraland.Kernel.Comms.Rfc4.Packet.MessageOneofCase.Chat, OnPrivateMessageReceived);
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        private void OnPrivateMessageReceived(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Chat> receivedMessage)
        {
            OnChatAsync(receivedMessage, true).Forget();
        }

        private void OnMessageReceived(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Chat> receivedMessage)
        {
            OnChatAsync(receivedMessage).Forget();
        }

        private async UniTaskVoid OnChatAsync(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Chat> receivedMessage, bool isPrivate = false)
        {
            using (receivedMessage)
            {
                if (messageDeduplication.TryPass(receivedMessage.FromWalletId, receivedMessage.Payload.Timestamp) == false)
                    return;

                Profile? profile = await profileRepository.GetAsync(receivedMessage.FromWalletId, cancellationTokenSource.Token);

                ChatChannel.ChannelId parsedChannelId = isPrivate? new ChatChannel.ChannelId(receivedMessage.FromWalletId) : ChatChannel.NEARBY_CHANNEL_ID;
                string chatMessage = receivedMessage.Payload.Message;

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
                        parsedChannelId,
                        isPrivate,
                        isMention,
                        false,
                        false
                    )
                );
            }
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

        public void Send(ChatChannel channel, string message, string origin)
        {
            if (cancellationTokenSource.IsCancellationRequested)
                throw new Exception("ChatMessagesBus is disposed");

            double timestamp = DateTime.UtcNow.TimeOfDay.TotalSeconds;
            switch (channel.ChannelType)
            {
                case ChatChannel.ChatChannelType.Nearby:
                    SendTo(message, timestamp, messagePipesHub.IslandPipe());
                    SendTo(message, timestamp, messagePipesHub.ScenePipe());
                    break;
                case ChatChannel.ChatChannelType.User:
                    SendTo(message, timestamp, messagePipesHub.ChatPipe(), channel.Id.Id);
                    break;
                default:
                    break;
            }

        }

        private void SendTo(string message, double timestamp, IMessagePipe messagePipe, string? recipient = null)
        {
            MessageWrap<Decentraland.Kernel.Comms.Rfc4.Chat> chat = messagePipe.NewMessage<Decentraland.Kernel.Comms.Rfc4.Chat>();
            if (recipient != null)
                chat.AddSpecialRecipient(recipient);
            chat.Payload.Message = message;
            chat.Payload.Timestamp = timestamp;
            chat.SendAndDisposeAsync(cancellationTokenSource.Token, DataPacketKind.KindReliable).Forget();
        }
    }
}
