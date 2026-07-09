using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Web3;
using Decentraland.Pulse;
using Pulse.Transport;
using UnityEngine;
using Utility;

namespace DCL.Multiplayer.Movement
{
    public partial class PulseMultiplayerBus
    {
        public void BroadcastTeleport(Vector3 worldPosition)
        {
            // RealmData is guaranteed to hold the destination realm by the time the teleport is broadcast
            PurgeDifferentRealmPeers();

            var outgoing = OutgoingMessage.Create(PacketMode.RELIABLE, ClientMessage.MessageOneofCase.Teleport);

            Vector2Int parcelIndex = worldPosition.ToParcel();

            var relativePosition = new Vector3(
                worldPosition.x - (parcelIndex.x * ParcelMathHelper.PARCEL_SIZE),
                worldPosition.y,
                worldPosition.z - (parcelIndex.y * ParcelMathHelper.PARCEL_SIZE)
            );

            TeleportRequest teleport = outgoing.Message.Teleport;
            teleport.ParcelIndex = parcelEncoder.Encode(parcelIndex);
            teleport.PositionXQuantized = relativePosition.x;
            teleport.PositionYQuantized = relativePosition.y;
            teleport.PositionZQuantized = relativePosition.z;
            teleport.Realm = realmData.RealmName;

            pulseService.Send(outgoing);
        }

        private void HandleTeleport(IncomingMessage message)
        {
            if (isDisposed)
            {
                ReportHub.LogError(ReportCategory.MULTIPLAYER, "Receiving teleport while disposed");
                return;
            }

            TryDrainRoutingPurge();

            TeleportPerformed teleport = message.Message.Teleported;

            if (!peerIdCache.TryGetWalletInRealm(teleport.SubjectId, realmData.RealmName, out Web3Address wallet))
            {
                // Expected for peers filtered out by realm, so not an error
                ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Receiving teleport from unknown peer: {teleport.SubjectId}");
                return;
            }

            NetworkMovementMessage movementMessage = ToNetworkMovementMessage(teleport.State, teleport.SubjectId, teleport.ServerTick, isInstant: true);
            TryUpdateLastMovementAndCompleteResync(teleport.ServerTick, teleport.SubjectId, teleport.Sequence, movementMessage);
            Inbox(movementMessage, wallet);
        }
    }
}
