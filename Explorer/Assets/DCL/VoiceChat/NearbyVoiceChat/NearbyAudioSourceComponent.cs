using Arch.Core;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using UnityEngine;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Lives on a dedicated audio-source entity. Bound 1:1 to a (participant, sid) pair.
    /// <see cref="DCL.VoiceChat.Nearby.Systems.NearbyAudioPositionSystem"/> reads <see cref="AvatarEntity"/> per frame to fetch
    /// the head transform; nothing else owns the avatar entity reference.
    ///
    /// Diff state (<see cref="LastSeenMuteVersion"/>, <see cref="LastAppliedMute"/>, <see cref="LastWrittenPos"/>)
    /// is initialized so the first tick after binding pessimistically recomputes everything:
    /// <c>cache.Version</c> starts at 1 (so 0 always mismatches), <see cref="LastAppliedMute"/> matches the
    /// binding-time <c>mute=true</c> start state, and <see cref="LastWrittenPos"/> is +Infinity so the first
    /// real <c>sourcePos</c> always exceeds the position-write epsilon.
    /// </summary>
    public struct NearbyAudioSourceComponent
    {
        public readonly StreamKey Key;
        public readonly Entity AvatarEntity;
        public readonly LivekitAudioSource LivekitAudioSource;

        public uint LastSeenMuteVersion;
        public bool LastAppliedMute;
        public Vector3 LastWrittenPos;

        public NearbyAudioSourceComponent(StreamKey key, Entity avatarEntity, LivekitAudioSource livekitAudioSource)
        {
            Key = key;
            AvatarEntity = avatarEntity;
            LivekitAudioSource = livekitAudioSource;

            LastSeenMuteVersion = 0;
            LastAppliedMute = true;
            LastWrittenPos = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        }
    }
}
