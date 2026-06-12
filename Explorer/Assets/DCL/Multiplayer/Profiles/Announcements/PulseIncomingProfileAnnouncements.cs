using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Profiles.RemoveIntentions;
using System.Collections.Generic;
using Utility.Multithreading;

namespace DCL.Multiplayer.Profiles.Announcements
{
    public class PulseIncomingProfileAnnouncements : IRemoteAnnouncements
    {
        private readonly DCLConcurrentQueue<RemoteAnnouncement> queue = new ();

        public void Enqueue(string userId, int version) =>
            queue.Enqueue(new RemoteAnnouncement(version, userId, RoomSource.PULSE));

        public void Fill(List<RemoteAnnouncement> announcements)
        {
            while (queue.TryDequeue(out RemoteAnnouncement item))
                announcements.Add(item);
        }

        public void Remove(IReadOnlyCollection<RemoveIntention> removeIntentions) { }
    }
}
