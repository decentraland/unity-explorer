using UnityEngine;

namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    ///     Shared listener-position state for the Nearby audio chain.
    /// </summary>
    public class NearbyListenerState
    {
        public Transform ListenerTransform { get; private set; } = null!;

        public Vector3 PlayerHeadPosition;
        public bool IsFirstPerson;

        public void BindListener(Transform listenerTransform) =>
            ListenerTransform = listenerTransform;
    }
}
