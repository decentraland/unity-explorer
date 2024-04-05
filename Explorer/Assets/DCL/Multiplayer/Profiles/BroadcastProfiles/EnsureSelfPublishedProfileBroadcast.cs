namespace DCL.Multiplayer.Profiles.BroadcastProfiles
{
    public class EnsureSelfPublishedProfileBroadcast : IProfileBroadcast
    {
        private readonly IProfileBroadcast origin;

        public EnsureSelfPublishedProfileBroadcast(IProfileBroadcast origin)
        {
            this.origin = origin;
        }

        public void NotifyRemotes()
        {
            //TODO
            origin.NotifyRemotes();
        }

        public void Dispose()
        {
            origin.Dispose();
        }
    }
}
