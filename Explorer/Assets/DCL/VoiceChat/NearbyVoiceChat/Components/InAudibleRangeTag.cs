namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    /// Marker placed on the avatar entity when the avatar is a remote audio publisher
    /// (carries <see cref="StreamingAudioComponent"/>) and its distance to the local player is
    /// within the outer-boundary hysteresis band. Presence gates audio-source materialization
    /// in <see cref="DCL.VoiceChat.Nearby.Systems.NearbyAudioBindingSystem"/>.
    /// <para><see cref="IsSuspended"/> reflects the inner suspend hysteresis band (between
    /// suspend-out and outer-out): when true, audio-source mixing/spatialization is gated off
    /// without dropping the outer-band membership. The flag is enforced as a subset of
    /// <see cref="InAudibleRangeTag"/> by type — it cannot survive component removal.</para>
    /// <para>Invariant: <c>InAudibleRangeTag ⊆ StreamingAudioComponent</c>. Owned by
    /// <see cref="DCL.VoiceChat.Nearby.Systems.NearbyAudibleRangeSystem"/>.</para>
    /// </summary>
    public struct InAudibleRangeTag
    {
        public bool IsSuspended;
    }
}
