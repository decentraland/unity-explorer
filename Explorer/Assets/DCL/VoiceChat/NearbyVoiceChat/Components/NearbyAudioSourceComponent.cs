using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using UnityEngine;

namespace DCL.VoiceChat
{
    public struct NearbyAudioSourceComponent
    {
        public readonly StreamKey Key;
        public readonly LivekitAudioSource LivekitAudioSource;

        public uint LastSeenMuteVersion;
        public bool LastAppliedMute;
        public bool LastInactive;
        public Vector3 LastWrittenPos;

        public NearbyAudioSourceComponent(StreamKey key, LivekitAudioSource livekitAudioSource)
        {
            Key = key;
            LivekitAudioSource = livekitAudioSource;

            LastSeenMuteVersion = 0;
            LastAppliedMute = true;
            LastInactive = false;
            LastWrittenPos = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        }
    }
}
