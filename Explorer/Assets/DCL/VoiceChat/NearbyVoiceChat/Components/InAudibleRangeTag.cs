namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    /// Marker placed on the avatar entity when the avatar is a remote audio publisher
    /// (carries <see cref="StreamingAudioComponent"/>) and its distance to the local player is
    /// within the outer-boundary hysteresis band. Presence gates audio-source materialization
    /// in <see cref="DCL.VoiceChat.Nearby.Systems.NearbyAudioBindingSystem"/>.
    /// <para>Invariant: <c>InAudibleRangeTag ⊆ StreamingAudioComponent</c>. Owned by
    /// <see cref="DCL.VoiceChat.Nearby.Systems.NearbyAudibleRangeMarkerSystem"/>.</para>
    /// </summary>
    public struct InAudibleRangeTag { }
}
