using Arch.Core;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Lives on a dedicated audio-source entity. Bound 1:1 to a (participant, sid) pair.
    /// <see cref="NearbyAudioPositionSystem"/> reads <see cref="AvatarEntity"/> per frame to fetch
    /// the head transform; nothing else owns the avatar entity reference.
    /// </summary>
    public struct NearbyAudioSourceComponent
    {
        public readonly StreamKey Key;
        public readonly Entity AvatarEntity;
        public readonly LivekitAudioSource LivekitAudioSource;

        public NearbyAudioSourceComponent(StreamKey key, Entity avatarEntity, LivekitAudioSource livekitAudioSource)
        {
            Key = key;
            AvatarEntity = avatarEntity;
            LivekitAudioSource = livekitAudioSource;
        }
    }
}
