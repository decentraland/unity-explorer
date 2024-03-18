using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Profiles.Bunches;
using Decentraland.Kernel.Comms.Rfc4;
using System.Collections.Generic;
using Utility.Multithreading;

namespace DCL.Multiplayer.Profiles.RemoteAnnouncements
{
    public class ThreadSafeRemoteAnnouncements : IRemoteAnnouncements
    {
        private readonly HashSet<RemoteAnnouncement> list = new ();
        private readonly MutexSync mutex = new ();

        public ThreadSafeRemoteAnnouncements(IMessagePipesHub messagePipesHub)
        {
            messagePipesHub.IslandPipe().Subscribe<AnnounceProfileVersion>(OnMessageReceived);
            messagePipesHub.ScenePipe().Subscribe<AnnounceProfileVersion>(OnMessageReceived);
        }

        private void OnMessageReceived(ReceivedMessage<AnnounceProfileVersion> obj)
        {
            using (obj)
                ThreadSafeAdd(new RemoteAnnouncement((int)obj.Payload.ProfileVersion, obj.FromWalletId));
        }

        public OwnedBunch<RemoteAnnouncement> Bunch() =>
            new (mutex, list);

        private void ThreadSafeAdd(RemoteAnnouncement remoteAnnouncement)
        {
            using MutexSync.Scope _ = mutex.GetScope();
            list.Add(remoteAnnouncement);
        }
    }
}
