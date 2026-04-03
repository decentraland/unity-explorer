using Arch.Core;
using DCL.Diagnostics;
using DCL.Multiplayer.Profiles.Tables;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DCL.Multiplayer.Movement
{
    public class MovementInbox
    {
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly World globalWorld;
        private readonly ConcurrentQueue<(string wallet, NetworkMovementMessage message)> incomingQueue = new ();
        private readonly Dictionary<string, NetworkMovementMessage> pendingMessages = new ();

        public MovementInbox(IReadOnlyEntityParticipantTable entityParticipantTable, World globalWorld)
        {
            this.entityParticipantTable = entityParticipantTable;
            this.globalWorld = globalWorld;
        }

        /// <summary>
        ///     Enqueues a movement message for later processing. Thread-safe — called from the background drain thread.
        /// </summary>
        public void Enqueue(NetworkMovementMessage fullMovementMessage, string @for)
        {
            ReportHub.Log(ReportCategory.MULTIPLAYER_MOVEMENT, $"Movement from {@for} - {fullMovementMessage}");

            incomingQueue.Enqueue((@for, fullMovementMessage));
        }

        /// <summary>
        ///     Drains all queued movement messages and dispatches them to entities.
        ///     Must be called from the main thread each frame.
        /// </summary>
        public void DrainToEntities()
        {
            while (incomingQueue.TryDequeue(out (string wallet, NetworkMovementMessage message) item))
            {
                if (!entityParticipantTable.TryGet(item.wallet, out IReadOnlyEntityParticipantTable.Entry entry))
                {
                    pendingMessages[item.wallet] = item.message;
                    continue;
                }

                EnqueueToEntity(entry.Entity, item.message);
            }
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
