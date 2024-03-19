using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Profiles.Bunches;
using Decentraland.Kernel.Comms.Rfc4;
using System.Collections.Generic;

namespace DCL.Multiplayer.Profiles.RemoteAnnouncements
{
    public class RemoteAnnouncements : IRemoteAnnouncements
    {
        private readonly List<RemoteAnnouncement> list = new ();

        public RemoteAnnouncements(IMessagePipesHub messagePipesHub)
        {
            messagePipesHub.IslandPipe().Subscribe<AnnounceProfileVersion>(OnMessageReceived);
            messagePipesHub.ScenePipe().Subscribe<AnnounceProfileVersion>(OnMessageReceived);
        }

        private void OnMessageReceived(ReceivedMessage<AnnounceProfileVersion> obj)
        {
            using (obj)
                list.Add(new RemoteAnnouncement((int)obj.Payload.ProfileVersion, obj.FromWalletId));
        }

        public Bunch<RemoteAnnouncement> Bunch() =>
            new (list);
    }
}
