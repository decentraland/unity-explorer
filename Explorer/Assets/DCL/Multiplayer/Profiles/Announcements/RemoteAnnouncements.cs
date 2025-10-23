using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Profiles.Announcements;
using DCL.Multiplayer.Profiles.Bunches;
using Decentraland.Kernel.Comms.Rfc4;
using System.Collections.Generic;

namespace DCL.Multiplayer.Profiles.RemoteAnnouncements
{
    public class RemoteAnnouncements
    {
        private readonly List<RemoteAnnouncement> list = new ();

        public RemoteAnnouncements(IMessagePipesHub messagePipesHub)
        {
            messagePipesHub.IslandPipe().Subscribe<AnnounceProfileVersion>(Packet.MessageOneofCase.ProfileVersion, OnMessageReceived);
            messagePipesHub.ScenePipe().Subscribe<AnnounceProfileVersion>(Packet.MessageOneofCase.ProfileVersion, OnMessageReceived);
        }

        private void OnMessageReceived(ReceivedMessage<AnnounceProfileVersion> obj)
        {
            using (obj)
                list.Add(new RemoteAnnouncement((int)obj.Payload.ProfileVersion, obj.FromWalletId, obj.FromRoom));
        }

        public Bunch<RemoteAnnouncement> Bunch() =>
            new (list);
    }
}
