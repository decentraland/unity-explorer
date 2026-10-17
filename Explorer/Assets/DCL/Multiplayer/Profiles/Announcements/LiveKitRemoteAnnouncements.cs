using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Profiles.BroadcastProfiles;
using DCL.Multiplayer.Profiles.RemoveIntentions;
using Decentraland.Kernel.Comms.Rfc4;
using System.Collections.Generic;

namespace DCL.Multiplayer.Profiles.Announcements
{
    public class LiveKitRemoteAnnouncements : IRemoteAnnouncements
    {
        private readonly List<RemoteAnnouncement> list = new ();

        private readonly LiveKitMessagesBroadcaster broadcaster;

        public LiveKitRemoteAnnouncements(IMessagePipesHub messagePipesHub, LiveKitMessagesBroadcaster broadcaster)
        {
            this.broadcaster = broadcaster;

            messagePipesHub.IslandPipe().Subscribe<AnnounceProfileVersion>(Packet.MessageOneofCase.ProfileVersion, OnMessageReceived);
            messagePipesHub.ScenePipe().Subscribe<AnnounceProfileVersion>(Packet.MessageOneofCase.ProfileVersion, OnMessageReceived);
        }

        private void OnMessageReceived(ReceivedMessage<AnnounceProfileVersion> obj)
        {
            using (obj)
            {
                list.Add(new RemoteAnnouncement((int)obj.Payload.ProfileVersion, obj.FromWalletId, obj.FromRoom));
                broadcaster.Add(obj.FromWalletId, obj.FromRoom);
            }
        }

        public void Fill(List<RemoteAnnouncement> announcements)
        {
            announcements.AddRange(list);
            list.Clear();
        }

        public void Remove(IReadOnlyCollection<RemoveIntention> removeIntentions)
        {
            foreach (RemoveIntention removeIntention in removeIntentions)
                broadcaster.Remove(removeIntention.WalletId, removeIntention.FromRoom);
        }
    }
}
