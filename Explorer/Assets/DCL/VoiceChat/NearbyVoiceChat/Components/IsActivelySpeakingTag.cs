namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    /// Marker placed on the avatar entity when LiveKit reports the avatar's walletId in  <c>room.ActiveSpeakers</c>
    /// AND the avatar already carries  <see cref="NearbyAudioStreamerComponent"/>.
    /// Server-debounced — expect 100–1000 ms lag relative to actual VAD onset/offset.
    /// <para>Invariant: <c>IsActivelySpeakingTag ⊆ StreamingAudioComponent</c>.</para>
    /// <para>Remote-only — see <see cref="NearbyAudioStreamerComponent"/>.</para>
    /// </summary>
    public struct IsActivelySpeakingTag { }
}
