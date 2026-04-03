using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Profiles.Announcements;
using DCL.Multiplayer.Profiles.Bunches;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DCL.Multiplayer.Profiles.RemoteAnnouncements
{
    public class PulseIncomingProfileAnnouncements : IRemoteAnnouncements
    {
        private readonly ConcurrentQueue<RemoteAnnouncement> queue = new ();
        private readonly List<RemoteAnnouncement> drainList = new ();

        public void Enqueue(string userId, int version) =>
            queue.Enqueue(new RemoteAnnouncement(version, userId, RoomSource.PULSE));

        public Bunch<RemoteAnnouncement> Bunch()
        {
            drainList.Clear();

            while (queue.TryDequeue(out RemoteAnnouncement item))
                drainList.Add(item);

            return new Bunch<RemoteAnnouncement>(drainList);
        }
    }
}
