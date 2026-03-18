using Arch.Core;
using DCL.Diagnostics;
using DCL.Multiplayer.Profiles.Tables;
using System.Collections.Generic;

namespace DCL.Multiplayer.Movement
{
    public class MovementInbox
    {
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly World globalWorld;
        private readonly Dictionary<string, NetworkMovementMessage> pendingMessages = new ();

        public MovementInbox(IReadOnlyEntityParticipantTable entityParticipantTable, World globalWorld)
        {
            this.entityParticipantTable = entityParticipantTable;
            this.globalWorld = globalWorld;
        }

        public void TryEnqueue(NetworkMovementMessage fullMovementMessage, string @for)
        {
            ReportHub.Log(ReportCategory.MULTIPLAYER_MOVEMENT, $"Movement from {@for} - {fullMovementMessage}");

            if (!entityParticipantTable.TryGet(@for, out IReadOnlyEntityParticipantTable.Entry entry))
            {
                pendingMessages[@for] = fullMovementMessage;
                return;
            }

            EnqueueToEntity(entry.Entity, fullMovementMessage);
        }

        /// <summary>
        ///     Flushes any pending movement message that was received but could not be processed due to missing participant
        /// </summary>
        public void TryFlushPending(string walletId)
        {
            if (!pendingMessages.Remove(walletId, out NetworkMovementMessage pending))
                return;

            if (!entityParticipantTable.TryGet(walletId, out IReadOnlyEntityParticipantTable.Entry entry))
                return;

            EnqueueToEntity(entry.Entity, pending);
        }

        public void RemovePending(string walletId)
        {
            pendingMessages.Remove(walletId);
        }

        private void EnqueueToEntity(Entity entity, NetworkMovementMessage message)
        {
            if (globalWorld.TryGet(entity, out RemotePlayerMovementComponent remotePlayerMovementComponent))
                remotePlayerMovementComponent.Enqueue(message);
        }
    }
}
