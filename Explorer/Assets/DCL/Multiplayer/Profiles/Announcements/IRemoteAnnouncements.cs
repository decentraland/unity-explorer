using DCL.Multiplayer.Profiles.RemoveIntentions;
using System.Collections.Generic;

namespace DCL.Multiplayer.Profiles.Announcements
{
    public interface IRemoteAnnouncements
    {
        /// <summary>
        ///     Fill the list with all required announcements
        /// </summary>
        public void Fill(List<RemoteAnnouncement> announcements);

        public void Remove(IReadOnlyCollection<RemoveIntention> removeIntentions);

        public void Clear();
    }
}
