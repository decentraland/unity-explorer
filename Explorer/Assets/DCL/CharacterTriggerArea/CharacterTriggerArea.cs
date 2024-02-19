using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.CharacterTriggerArea
{
    public class CharacterTriggerArea : MonoBehaviour
    {
        [field: SerializeField] public BoxCollider BoxCollider { get; private set; }

        private readonly HashSet<Collider> detectedColliders = new ();
        [field: NonSerialized] public Transform TargetTransform;

        public void OnTriggerEnter(Collider other)
        {
            if (TargetTransform != null && TargetTransform != other.transform) return;

            detectedColliders.Add(other);
            OnEnteredTrigger?.Invoke(other);
        }

        public void OnTriggerExit(Collider other)
        {
            if (TargetTransform != null && TargetTransform != other.transform) return;

            OnExitedTrigger?.Invoke(other);
            detectedColliders.Remove(other);
        }

        public event Action<Collider> OnEnteredTrigger;
        public event Action<Collider> OnExitedTrigger;

        public void ForceOnTriggerExit()
        {
            if (detectedColliders.Count == 0) return;

            foreach (Collider detectedCollider in detectedColliders) { OnExitedTrigger?.Invoke(detectedCollider); }

            detectedColliders.Clear();
        }

        public void ClearEvents()
        {
            OnEnteredTrigger = null;
            OnExitedTrigger = null;
        }
    }
}
