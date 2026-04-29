namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    /// Marker placed on the avatar entity when LiveKit reports the avatar's walletId in
    /// <c>room.ActiveSpeakers</c> AND the avatar already carries
    /// <see cref="IsStreamingAudioTag"/>. Server-debounced — expect 100–1000 ms lag relative
    /// to actual VAD onset/offset.
    /// <para>Invariant: <c>IsActivelySpeakingTag ⊆ IsStreamingAudioTag</c>. Enforced by
    /// <see cref="DCL.VoiceChat.Nearby.Systems.NearbyVoiceMarkerSystem"/> at archetype level
    /// (the Add-speaking query filters on <see cref="IsStreamingAudioTag"/>) and via cascade
    /// removal when the streaming tag is removed.</para>
    /// <para>Remote-only — see <see cref="IsStreamingAudioTag"/>.</para>
    /// </summary>
    public struct IsActivelySpeakingTag { }
}
