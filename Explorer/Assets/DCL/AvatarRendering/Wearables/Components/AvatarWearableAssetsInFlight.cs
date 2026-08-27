namespace DCL.AvatarRendering.Wearables.Components
{
    /// <summary>
    ///     Marks a wearable resolution (an avatar's <see cref="Intentions.GetWearablesByPointersIntention" /> promise
    ///     entity) that has been admitted to the asset-loading phase, so its per-wearable asset promises may be created.
    ///     <see cref="Systems.ResolveWearablePromisesSystem" /> caps how many resolutions carry this marker at once,
    ///     so avatars finish loading one after another instead of interleaving their downloads.
    /// </summary>
    public struct AvatarWearableAssetsInFlight
    {
        /// <summary>
        ///     Seconds since admission. A resolution stuck beyond a threshold stops counting against the cap so it
        ///     cannot starve the queue.
        /// </summary>
        public float Age;
    }
}
