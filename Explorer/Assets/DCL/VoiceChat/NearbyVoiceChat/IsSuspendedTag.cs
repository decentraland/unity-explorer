namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    /// Marker placed on the avatar entity when the avatar is in the outer band of the audible
    /// range (between the suspend-out and outer-out thresholds). All audio sources whose
    /// <c>AvatarEntity</c> carries this tag have their <c>LivekitAudioSource</c> and underlying
    /// <see cref="UnityEngine.AudioSource"/> disabled — no audio-thread mixing, no
    /// spatial-panning, no transform updates.
    /// <para>Invariant: <c>IsSuspendedTag ⊆ InAudibleRangeTag ⊆ IsStreamingAudioTag</c>.
    /// Owned by <see cref="DCL.VoiceChat.Nearby.Systems.NearbyAudibleRangeMarkerSystem"/>.</para>
    /// </summary>
    public struct IsSuspendedTag { }
}
