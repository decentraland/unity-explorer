using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Profiles.RemoveIntentions;
using System.Collections.Generic;
using Utility.Multithreading;

namespace DCL.Multiplayer.Profiles.Announcements
{
    public class PulseIncomingProfileAnnouncements : IRemoteAnnouncements
    {
        private readonly DCLConcurrentQueue<RemoteAnnouncement> queue = new ();

        // RemoveWallets runs on the main thread only, so single-threaded reuse is safe
        private readonly List<RemoteAnnouncement> scrubBuffer = new ();

        public void Enqueue(string userId, int version) =>
            queue.Enqueue(new RemoteAnnouncement(version, userId, RoomSource.PULSE));

        public void Fill(List<RemoteAnnouncement> announcements)
        {
            while (queue.TryDequeue(out RemoteAnnouncement item))
                announcements.Add(item);
        }

        public void Remove(IReadOnlyCollection<RemoveIntention> removeIntentions) { }

        /// <summary>
        ///     Safe against concurrent enqueues: a racing item is either examined here or appended after the survivors.
        /// </summary>
        public void RemoveWallets(HashSet<string> wallets)
        {
            while (queue.TryDequeue(out RemoteAnnouncement item))
            {
                if (!wallets.Contains(item.WalletId))
                    scrubBuffer.Add(item);
            }

            foreach (RemoteAnnouncement item in scrubBuffer)
                queue.Enqueue(item);

            scrubBuffer.Clear();
        }

        public void Clear() =>
            queue.Clear();
    }
}
