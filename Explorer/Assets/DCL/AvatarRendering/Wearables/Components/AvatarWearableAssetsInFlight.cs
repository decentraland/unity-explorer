namespace DCL.AvatarRendering.Wearables.Components
{
    /// <summary>
    ///     Marks a wearable resolution (an avatar's <see cref="Intentions.GetWearablesByPointersIntention" /> promise
    ///     entity) that has been admitted to the asset-loading phase, so its per-wearable asset promises may be created.
    ///     <see cref="Systems.ResolveWearablePromisesSystem" /> admits candidates only while the shared download budget
    ///     has free slots, so avatars stagger in nearest-first instead of flooding the queue and all finishing together.
    /// </summary>
    public struct AvatarWearableAssetsInFlight { }
}
