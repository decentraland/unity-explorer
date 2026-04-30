namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    /// Per-avatar snapshot of the LiveKit audio sids currently subscribed for that avatar's
    /// walletId. Replaces the zero-field <c>IsStreamingAudioTag</c>: the same archetype-membership
    /// contract (avatar entity is a remote audio publisher), now with the sid set carried as data.
    /// <para>
    /// <see cref="SidsSnapshot"/> is a reference to a registry-owned copy-on-write <c>string[]</c>.
    /// Reference identity is the version signal: a different reference ↔ content changed.
    /// Never mutate the array. Never retain across frames longer than the COW guarantees
    /// (the registry replaces the reference on every sid add/remove).
    /// </para>
    /// <para>Invariant: <c>IsActivelySpeakingTag ⊆ StreamingAudioComponent</c>,
    /// <c>InAudibleRangeTag ⊆ StreamingAudioComponent</c>,
    /// <c>IsSuspendedTag ⊆ InAudibleRangeTag ⊆ StreamingAudioComponent</c>.</para>
    /// <para>Remote-only: the local participant is never present in the registry's streaming
    /// snapshot (LiveKit does not raise <c>TrackSubscribed</c> for locally-published tracks),
    /// so this component is never applied to the local player avatar.</para>
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
