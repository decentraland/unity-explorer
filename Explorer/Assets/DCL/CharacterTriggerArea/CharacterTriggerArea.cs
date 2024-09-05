using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.CharacterTriggerArea
{
    public class CharacterTriggerArea : MonoBehaviour, IDisposable
    {
        [field: SerializeField] public BoxCollider BoxCollider { get; private set; }

        private readonly HashSet<Transform> currentAvatarsInside = new ();
        internal readonly HashSet<Transform> enteredAvatarsToBeProcessed = new ();
        internal readonly HashSet<Transform> exitedAvatarsToBeProcessed = new ();
        [NonSerialized] public Transform TargetTransform;

        public IReadOnlyCollection<Transform> EnteredAvatarsToBeProcessed => enteredAvatarsToBeProcessed;
        public IReadOnlyCollection<Transform> ExitedAvatarsToBeProcessed => exitedAvatarsToBeProcessed;
        public IReadOnlyCollection<Transform> CurrentAvatarsInside => currentAvatarsInside;

        public void OnTriggerEnter(Collider other)
        {
            if (TargetTransform != null && TargetTransform != other.transform) return;

            enteredAvatarsToBeProcessed.Add(other.transform);
            currentAvatarsInside.Add(other.transform);
            exitedAvatarsToBeProcessed.Remove(other.transform);
        }

        public void OnTriggerExit(Collider other)
        {
            if (TargetTransform != null && TargetTransform != other.transform) return;

            exitedAvatarsToBeProcessed.Add(other.transform);
            enteredAvatarsToBeProcessed.Remove(other.transform);
            currentAvatarsInside.Remove(other.transform);
        }

        public void Dispose()
        {
            BoxCollider.enabled = false;

            foreach (Transform avatarTransform in currentAvatarsInside)
                exitedAvatarsToBeProcessed.Add(avatarTransform);

            currentAvatarsInside.Clear();
        }

        public void Clear()
        {
            enteredAvatarsToBeProcessed.Clear();
            exitedAvatarsToBeProcessed.Clear();
        }

        public void ClearEnteredAvatarsToBeProcessed() =>
            enteredAvatarsToBeProcessed.Clear();

        public void ClearExitedAvatarsToBeProcessed() =>
            exitedAvatarsToBeProcessed.Clear();
    }
}
