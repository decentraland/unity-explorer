using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.CharacterTriggerArea
{
    public class CharacterTriggerArea : MonoBehaviour, IDisposable
    {
        [field: SerializeField] public BoxCollider BoxCollider { get; private set; }

        private readonly HashSet<Transform> currentAvatarsInside = new ();
        internal readonly HashSet<Transform> enteredThisFrame = new ();
        internal readonly HashSet<Transform> exitedThisFrame = new ();
        [field: NonSerialized] public Transform TargetTransform;

        public IReadOnlyCollection<Transform> EnteredThisFrame => enteredThisFrame;
        public IReadOnlyCollection<Transform> ExitedThisFrame => exitedThisFrame;
        public IReadOnlyCollection<Transform> CurrentAvatarsInside => currentAvatarsInside;

        public void OnTriggerEnter(Collider other)
        {
            if (TargetTransform != null && TargetTransform != other.transform) return;

            enteredThisFrame.Add(other.transform);
            currentAvatarsInside.Add(other.transform);
        }

        public void OnTriggerExit(Collider other)
        {
            if (TargetTransform != null && TargetTransform != other.transform) return;

            exitedThisFrame.Add(other.transform);
            currentAvatarsInside.Remove(other.transform);
        }

        public void Dispose()
        {
            BoxCollider.enabled = false;

            foreach (Transform avatarTransform in currentAvatarsInside) { exitedThisFrame.Add(avatarTransform); }

            currentAvatarsInside.Clear();
        }

        public void Clear()
        {
            enteredThisFrame.Clear();
            exitedThisFrame.Clear();
        }
    }
}
