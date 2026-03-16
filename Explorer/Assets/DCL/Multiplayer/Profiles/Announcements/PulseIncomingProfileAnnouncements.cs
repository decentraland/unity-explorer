using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Profiles.Announcements;
using DCL.Multiplayer.Profiles.Bunches;
using System.Collections.Generic;

namespace DCL.Multiplayer.Profiles.RemoteAnnouncements
{
    public class PulseIncomingProfileAnnouncements : IRemoteAnnouncements
    {
        private readonly List<RemoteAnnouncement> list = new ();

        public void Enqueue(string userId, int version) =>
            list.Add(new RemoteAnnouncement(version, userId, RoomSource.PULSE));

        public Bunch<RemoteAnnouncement> Bunch() =>
            new (list);
    }
}
