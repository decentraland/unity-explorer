namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    ///     Per-avatar mirror of the registry's audio sids for the avatar's walletId.
    ///     Replaces the former <c>IsStreamingAudioTag</c> marker — the data that used to live
    ///     in the registry side-channel rides on the entity instead, so consumers (Binding /
    ///     Cleanup / range-marker filters) can read it through query parameters with zero
    ///     registry calls on the per-frame hot path.
    ///     <para>
    ///         <see cref="SidsSnapshot"/> is a reference to a registry-owned copy-on-write
    ///         <c>string[]</c>. Reference identity is the version signal: a different reference
    ///         ↔ content changed. <b>Never mutate.</b> Never retain across frames longer than
    ///         the COW guarantees in <see cref="DCL.VoiceChat.Nearby.Audio.NearbyAudioStreamRegistry"/>.
    ///     </para>
    ///     <para>
    ///         Remote-only: the local participant is never present in the registry's streaming
    ///         snapshot (LiveKit does not raise <c>TrackSubscribed</c> for locally-published
    ///         tracks), so this component is never attached to the local player avatar.
    ///     </para>
    /// </summary>
    public struct StreamingAudioComponent
    {
        public string[] SidsSnapshot;

        public StreamingAudioComponent(string[] sidsSnapshot)
        {
            SidsSnapshot = sidsSnapshot;
        }
    }
}
