using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.CharacterTriggerArea
{
    public class CharacterTriggerArea : MonoBehaviour, IDisposable
    {
        // TODO: CREATE 1 DICTIONARY OF TRANSFORM, ENUM (ENTERED/STAYED/EXITED) ???

        [field: SerializeField] public BoxCollider BoxCollider { get; private set; }

        private readonly HashSet<Transform> currentAvatarsInside = new ();
        public HashSet<Transform> EnteredThisFrame;
        public HashSet<Transform> ExitedThisFrame;
        [field: NonSerialized] public Transform TargetTransform;

        public void OnTriggerEnter(Collider other)
        {
            if (TargetTransform != null && TargetTransform != other.transform) return;

            EnteredThisFrame.Add(other.transform);
            currentAvatarsInside.Add(other.transform);
        }

        public void OnTriggerExit(Collider other)
        {
            if (TargetTransform != null && TargetTransform != other.transform) return;

            ExitedThisFrame.Add(other.transform);
            currentAvatarsInside.Remove(other.transform);
        }

        public void Dispose()
        {
            BoxCollider.enabled = false;

            foreach (Transform avatarTransform in currentAvatarsInside) { ExitedThisFrame.Add(avatarTransform); }

            currentAvatarsInside.Clear();
        }
    }
}
