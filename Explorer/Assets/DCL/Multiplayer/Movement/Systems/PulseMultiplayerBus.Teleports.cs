using CrdtEcsBridge.Components.Conversion;
using DCL.Diagnostics;
using DCL.Multiplayer.Movement;
using Decentraland.Pulse;
using Pulse.Transport;
using UnityEngine;
using Utility;

namespace DCL.Multiplayer.Connections.Pulse
{
    public partial class PulseMultiplayerBus
    {
        public void BroadcastTeleport(Vector3 worldPosition)
        {
            var outgoing = OutgoingMessage.Create(PacketMode.RELIABLE, ClientMessage.MessageOneofCase.Teleport);

            Vector2Int parcelIndex = worldPosition.ToParcel();

            var relativePosition = new Vector3(
                worldPosition.x - (parcelIndex.x * ParcelMathHelper.PARCEL_SIZE),
                worldPosition.y,
                worldPosition.z - (parcelIndex.y * ParcelMathHelper.PARCEL_SIZE)
            );

            TeleportRequest teleport = outgoing.Message.Teleport;
            teleport.ParcelIndex = parcelEncoder.Encode(parcelIndex);
            teleport.Position = relativePosition.ToProtoVector();

            pulseService.Send(outgoing);
        }

        private void HandleTeleport(IncomingMessage message)
        {
            if (isDisposed)
            {
                ReportHub.LogError(ReportCategory.MULTIPLAYER, "Receiving teleport while disposed");
                return;
            }

            TeleportPerformed teleport = message.Message.Teleported;

            if (!peerIdCache.TryGetWallet(teleport.SubjectId, out string wallet))
            {
                ReportHub.LogError(ReportCategory.MULTIPLAYER, $"Receiving teleport from unknown peer: {teleport.SubjectId}");
                return;
            }

            NetworkMovementMessage movementMessage = ToNetworkMovementMessage(teleport.State, teleport.ServerTick, isInstant: true);
            TryUpdateLastMovementAndCompleteResync(teleport.ServerTick, teleport.SubjectId, teleport.Sequence, movementMessage);
            Inbox(movementMessage, wallet);
        }
    }
}
