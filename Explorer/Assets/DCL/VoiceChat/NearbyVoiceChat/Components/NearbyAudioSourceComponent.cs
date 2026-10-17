using Arch.Core;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using UnityEngine;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Lives on a dedicated audio-source entity. Bound 1:1 to a (participant, sid) pair.
    /// </summary>
    public struct NearbyAudioSourceComponent
    {
        public readonly StreamKey Key;
        public readonly Entity AvatarEntity;
        public readonly LivekitAudioSource LivekitAudioSource;

        public uint LastSeenMuteVersion;
        public bool LastAppliedMute;
        public bool LastInactive;
        public Vector3 LastWrittenPos;

        public NearbyAudioSourceComponent(StreamKey key, Entity avatarEntity, LivekitAudioSource livekitAudioSource)
        {
            Key = key;
            AvatarEntity = avatarEntity;
            LivekitAudioSource = livekitAudioSource;

            LastSeenMuteVersion = 0;
            LastAppliedMute = true;
            LastInactive = false;
            LastWrittenPos = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        }
    }
}
