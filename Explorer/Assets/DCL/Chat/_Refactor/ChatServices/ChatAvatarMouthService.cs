using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Chat.History;
using DCL.Multiplayer.Profiles.Tables;
using System;

namespace DCL.Chat.ChatServices
{
    /// <summary>
    ///     Listens to incoming chat messages from <see cref="IChatHistory"/> and writes
    ///     <see cref="AvatarMouthInputComponent.PendingMessage"/> on the corresponding ECS entities
    ///     so that <c>AvatarFacialExpressionSystem</c> can drive mouth animation independently of
    ///     the nametag / chat-bubble display system.
    /// </summary>
    public class ChatAvatarMouthService : IDisposable
    {
        private readonly IChatHistory chatHistory;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly World world;
        private readonly Entity playerEntity;

        public ChatAvatarMouthService(
            IChatHistory chatHistory,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            World world,
            Entity playerEntity)
        {
            this.chatHistory = chatHistory;
            this.entityParticipantTable = entityParticipantTable;
            this.world = world;
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
                SetPendingMessage(playerEntity, addedMessage.Message);
            else if (entityParticipantTable.TryGet(addedMessage.SenderWalletAddress, out IReadOnlyEntityParticipantTable.Entry entry))
                SetPendingMessage(entry.Entity, addedMessage.Message);
        }

        private void SetPendingMessage(Entity entity, string message)
        {
            if (world.Has<AvatarMouthInputComponent>(entity))
            {
                ref var input = ref world.Get<AvatarMouthInputComponent>(entity);
                input.PendingMessage = message;
                input.MessageIsDirty = true;
            }
            else
            {
                world.Add(entity, new AvatarMouthInputComponent { PendingMessage = message, MessageIsDirty = true });
            }
        }
    }
}
