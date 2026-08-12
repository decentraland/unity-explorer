using DCL.Multiplayer.Profiles.Bunches;

namespace DCL.Multiplayer.Profiles.RemoveIntentions
{
    public interface IRemoveIntentions
    {
        /// <summary>
        ///     Racy, lock-free peek that returns <c>true</c> when at least one intention
        ///     is queued. Does not acquire the backing <c>MutexSync</c>. Mirrors
        ///     <c>RemoteProfiles.NewBunchAvailable()</c>.
        ///     <para>
        ///     May observe a just-published intention one frame late; the item stays queued and
        ///     is picked up on the next frame (bounded one-frame staleness — never dropped).
        ///     </para>
        /// </summary>
        bool NewBunchAvailable();

        OwnedBunch<RemoveIntention> Bunch();
    }
}
