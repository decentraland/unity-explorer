using UnityEngine;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Per-avatar state for voice-driven lip sync animation.
    /// Added by <see cref="ProximityAudioPositionSystem"/> to entities that have
    /// a proximity audio source and a visible mouth renderer.
    /// </summary>
    public struct ProximityLipSyncComponent
    {
        public Renderer MouthRenderer;
        public int CurrentPoseIndex;
        public float PoseHoldTimer;
    }
}
