namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    /// Marker placed on the avatar entity when LiveKit reports the avatar's walletId in
    /// <c>room.ActiveSpeakers</c> AND the avatar already carries
    /// <see cref="StreamingAudioComponent"/>. Server-debounced — expect 100–1000 ms lag relative
    /// to actual VAD onset/offset.
    /// <para>Invariant: <c>IsActivelySpeakingTag ⊆ StreamingAudioComponent</c>. Enforced by
    /// <see cref="DCL.VoiceChat.Nearby.Systems.NearbyLivekitBridgeSystem"/> at archetype level
    /// (the Add-speaking query filters on <see cref="StreamingAudioComponent"/>) and via cascade
    /// removal when the streaming component is removed.</para>
    /// <para>Remote-only — see <see cref="StreamingAudioComponent"/>.</para>
    /// </summary>
    public struct IsActivelySpeakingTag { }
}
