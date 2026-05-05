using UnityEngine;

namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    ///     Shared listener-position state for the Nearby audio chain.
    /// </summary>
    public class NearbyListenerState
    {
        public Transform ListenerTransform { get; private set; } = null!;
        private Transform playerHeadTransform = null!;

        public Vector3 PlayerHeadPosition => playerHeadTransform.position;

        public void BindListener(Transform playerHead, Transform listener)
        {
            ListenerTransform = listener;
            this.playerHeadTransform = playerHead;
        }
    }
}
