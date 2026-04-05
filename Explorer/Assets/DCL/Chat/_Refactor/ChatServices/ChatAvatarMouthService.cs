using Arch.Core;
using DCL.AvatarRendering.AvatarShape;
using DCL.Chat.History;
using DCL.Multiplayer.Profiles.Tables;
using System;

namespace DCL.Chat.ChatServices
{
    /// <summary>
    ///     Listens to incoming chat messages from <see cref="IChatHistory"/> and enqueues
    ///     mouth-animation requests into <see cref="AvatarMouthInputQueue"/> so that
    ///     <c>AvatarFacialExpressionSystem</c> can apply them to ECS on the next frame.
    ///     Entity manipulation must happen inside ECS systems; this service only buffers.
    /// </summary>
    public class ChatAvatarMouthService : IDisposable
    {
        private readonly IChatHistory chatHistory;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly Entity playerEntity;
        private readonly AvatarMouthInputQueue mouthInputQueue;

        public ChatAvatarMouthService(
            IChatHistory chatHistory,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            AvatarMouthInputQueue mouthInputQueue,
            Entity playerEntity)
        {
            this.chatHistory = chatHistory;
            this.entityParticipantTable = entityParticipantTable;
            this.mouthInputQueue = mouthInputQueue;
            this.playerEntity = playerEntity;

            chatHistory.MessageAdded += OnMessageAdded;
        }

        public void Dispose()
        {
            chatHistory.MessageAdded -= OnMessageAdded;
        }

        private void OnMessageAdded(ChatChannel destinationChannel, ChatMessage addedMessage, int _)
        {
            if (addedMessage.IsSystemMessage)
                return;

            if (addedMessage.IsSentByOwnUser)
                mouthInputQueue.EnqueueMessage(playerEntity, addedMessage.Message);
            else if (entityParticipantTable.TryGet(addedMessage.SenderWalletAddress, out IReadOnlyEntityParticipantTable.Entry entry))
                mouthInputQueue.EnqueueMessage(entry.Entity, addedMessage.Message);
        }
    }
}
