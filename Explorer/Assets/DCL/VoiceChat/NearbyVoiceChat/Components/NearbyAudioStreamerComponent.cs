namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    ///     Per-avatar mirror of the registry's single active audio sid for the avatar's walletId.
    ///     <para>
    ///         <see cref="CurrentSid"/> is the resolver's pick (most-recent-frame winner). Never <c>null</c>
    ///         while the component is attached — <see cref="NearbyLivekitBridgeSystem"/> guarantees the invariant
    ///         by only attaching on a non-null resolver result and ref-mutating on flips.
    ///     </para>
    ///     <para>
    ///         Remote-only: the local participant (user) is never present in the registry's streaming snapshot.
    ///         LiveKit does not raise <c>TrackSubscribed</c> for locally-published tracks, so this component is never
    ///         attached to the local player avatar.
    ///     </para>
    /// </summary>
    public struct NearbyAudioStreamerComponent
    {
        public string CurrentSid;

        public NearbyAudioStreamerComponent(string currentSid)
        {
            CurrentSid = currentSid;
        }
    }
}
