using DCL.Multiplayer.Profiles.Bunches;

namespace DCL.Multiplayer.Profiles.RemoveIntentions
{
    public interface IRemoveIntentions
    {
        /// <summary>
        ///     Racy, lock-free peek of the pending-intention count. Lets the per-frame drain
        ///     skip constructing an <see cref="OwnedBunch{T}"/> (which acquires+releases the
        ///     backing <c>MutexSync</c>) when there is nothing to remove. Mirrors
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
