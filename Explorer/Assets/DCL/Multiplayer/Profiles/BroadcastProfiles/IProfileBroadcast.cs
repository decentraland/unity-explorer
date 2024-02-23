namespace DCL.Multiplayer.Profiles.BroadcastProfiles
{
    public interface IProfileBroadcast
    {
        //TODO send profiles to new connected remote participants

        void NotifyRemotes();
    }
}
