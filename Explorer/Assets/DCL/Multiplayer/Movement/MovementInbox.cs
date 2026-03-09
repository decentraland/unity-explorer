using Arch.Core;
using DCL.Diagnostics;
using DCL.Multiplayer.Profiles.Tables;

namespace DCL.Multiplayer.Movement
{
    public class MovementInbox
    {
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly World globalWorld;

        public MovementInbox(IReadOnlyEntityParticipantTable entityParticipantTable, World globalWorld)
        {
            this.entityParticipantTable = entityParticipantTable;
            this.globalWorld = globalWorld;
        }

        public void TryEnqueue(NetworkMovementMessage fullMovementMessage, string @for)
        {
            TryEnqueue(@for, fullMovementMessage);
            ReportHub.Log(ReportCategory.MULTIPLAYER_MOVEMENT, $"Movement from {@for} - {fullMovementMessage}");
        }

        private void TryEnqueue(string walletId, NetworkMovementMessage fullMovementMessage)
        {
            if (!entityParticipantTable.TryGet(walletId, out IReadOnlyEntityParticipantTable.Entry entry))
            {
                ReportHub.LogWarning(ReportCategory.MULTIPLAYER_MOVEMENT, $"Entity for wallet {walletId} not found");
                return;
            }

            Entity entity = entry.Entity;

            if (globalWorld.TryGet(entity, out RemotePlayerMovementComponent remotePlayerMovementComponent))
                remotePlayerMovementComponent.Enqueue(fullMovementMessage);
        }
    }
}
