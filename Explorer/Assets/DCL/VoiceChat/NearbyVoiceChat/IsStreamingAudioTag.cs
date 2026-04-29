namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    /// Marker placed on the avatar entity when LiveKit reports an active audio subscription
    /// for that avatar's walletId (i.e. <c>NearbyAudioStreamRegistry.GetAudioSids</c> returns
    /// non-null). Reflects the "we are receiving audio packets" lifecycle, not raw "track
    /// published".
    /// <para>Remote-only: the local participant is never present in the registry's streaming
    /// snapshot (LiveKit does not raise <c>TrackSubscribed</c> for locally-published tracks),
    /// so this tag is never applied to the local player avatar.</para>
    /// </summary>
    public struct IsStreamingAudioTag { }
}
