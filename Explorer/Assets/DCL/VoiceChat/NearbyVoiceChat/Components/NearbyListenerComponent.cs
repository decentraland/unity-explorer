using UnityEngine;

namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    /// Singleton component on the camera entity carrying the listener-position data
    /// <see cref="ListenerTransform"/> is set once in Initialize from the app-lifetime camera transform;
    /// <see cref="PlayerHeadPosition"/> and <see cref="IsFirstPerson"/> are refreshed every tick.
    /// Read-only consumer is <see cref="Systems.NearbyAudioPositionSystem"/>.
    /// </summary>
    public struct NearbyListenerComponent
    {
        public Transform ListenerTransform;
        public Vector3 PlayerHeadPosition;
        public bool IsFirstPerson;
    }
}
