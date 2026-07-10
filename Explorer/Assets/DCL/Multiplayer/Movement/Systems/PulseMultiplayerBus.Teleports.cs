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
            if (lastBroadcastRealm != null && lastBroadcastRealm != realmData.RealmName)
                PurgeDifferentRealmPeers();

            lastBroadcastRealm = realmData.RealmName;

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

            TeleportPerformed teleport = message.Message.Teleported;

            if (!peerIdCache.TryGetWallet(teleport.SubjectId, out Web3Address wallet))
            {
                ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Receiving teleport from unknown peer: {teleport.SubjectId}");
                return;
            }

            // An empty realm violates the server contract, so it's dropped rather than misread as a realm change
            if (teleport.Realm.Length == 0)
            {
                ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Dropping teleport for {teleport.SubjectId}: empty realm");
                return;
            }

            // A same-tick-range realm change is not re-announced with PlayerLeft, so the removal happens here
            if (teleport.Realm != realmData.RealmName)
            {
                peerIdCache.Remove(teleport.SubjectId);
                removeIntentions.Enqueue(wallet);
                PurgeQueues(teleport.SubjectId);
                return;
            }

            // Refreshing the realm is what keeps a co-teleporting peer (no PlayerJoined re-announcement) out of the purge
            peerIdCache.Set(wallet, teleport.SubjectId, teleport.Realm);

            NetworkMovementMessage movementMessage = ToNetworkMovementMessage(teleport.State, teleport.SubjectId, teleport.ServerTick, isInstant: true);
            TryUpdateLastMovementAndCompleteResync(teleport.ServerTick, teleport.SubjectId, teleport.Sequence, movementMessage);
            Inbox(movementMessage, wallet);
        }
    }
}
