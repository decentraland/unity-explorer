using LiveKit.Rooms.Streaming.Audio;
using UnityEngine;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Per-avatar state for voice-driven lip sync animation.
    /// Added by <see cref="ProximityAudioPositionSystem"/> when a remote participant
    /// has both a proximity audio source and a visible mouth renderer.
    /// </summary>
    public struct ProximityLipSyncComponent
    {
        public string ParticipantIdentity;
        public Renderer MouthRenderer;
        public LivekitAudioSource LivekitSource;
        public int CurrentPoseIndex;
        public float PoseHoldTimer;
        public float SmoothedAmplitude;
    }
}
