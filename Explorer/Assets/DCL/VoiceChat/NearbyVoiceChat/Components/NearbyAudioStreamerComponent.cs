using DCL.VoiceChat.Nearby.Audio;

namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    ///     Per-avatar mirror of the registry's audio sids for the avatar's walletId.
    ///     <para>
    ///         <see cref="StreamSidsSnapshot"/> is a reference to a registry-owned copy-on-write <c>string[]</c>.
    ///         Reference identity is the version signal: a different reference  ↔ content changed.
    ///         <b>Never mutate.</b>
    ///         Never retain across frames longer than the COW guarantees in <see cref="NearbyAudioStreamsRegistry"/>.
    ///     </para>
    ///     <para>
    ///         Remote-only: the local participant (user) is never present in the registry's streaming snapshot.
    ///         LiveKit does not raise <c>TrackSubscribed</c> for locally-published tracks, so this component is never attached to the local player avatar.
    ///     </para>
    /// </summary>
    public struct NearbyAudioStreamerComponent
    {
        public string[] StreamSidsSnapshot;

        public NearbyAudioStreamerComponent(string[] streamSidsSnapshot)
        {
            StreamSidsSnapshot = streamSidsSnapshot;
        }
    }
}
