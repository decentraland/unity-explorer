using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKEntityTriggerArea
{
    public class SDKEntityTriggerArea : MonoBehaviour, IDisposable
    {
        [field: SerializeField] public BoxCollider BoxCollider { get; private set; }
        [field: SerializeField] public SphereCollider SphereCollider { get; private set; }

        private readonly HashSet<Collider> currentEntitiesInside = new ();
        private readonly HashSet<Collider> enteredEntitiesToBeProcessed = new ();
        private readonly HashSet<Collider> exitedEntitiesToBeProcessed = new ();
        [NonSerialized] public Transform? TargetTransform;

        public IReadOnlyCollection<Collider> EnteredEntitiesToBeProcessed => enteredEntitiesToBeProcessed;
        public IReadOnlyCollection<Collider> ExitedEntitiesToBeProcessed => exitedEntitiesToBeProcessed;
        public IReadOnlyCollection<Collider> CurrentEntitiesInside => currentEntitiesInside;

        public void OnTriggerEnter(Collider other)
        {
            if (TargetTransform != null && TargetTransform != other.transform) return;

            enteredEntitiesToBeProcessed.Add(other);
            currentEntitiesInside.Add(other);
            exitedEntitiesToBeProcessed.Remove(other);
        }

        public void OnTriggerExit(Collider other)
        {
            if (TargetTransform != null && TargetTransform != other.transform) return;

            exitedEntitiesToBeProcessed.Add(other);
            enteredEntitiesToBeProcessed.Remove(other);
            currentEntitiesInside.Remove(other);
        }

        public void Dispose()
        {
            BoxCollider.enabled = false;
            SphereCollider.enabled = false;

            foreach (Collider entityCollider in currentEntitiesInside)
                exitedEntitiesToBeProcessed.Add(entityCollider);

            currentEntitiesInside.Clear();
        }

        public void Clear()
        {
            enteredEntitiesToBeProcessed.Clear();
            exitedEntitiesToBeProcessed.Clear();
        }

        public void ClearEnteredEntitiesToBeProcessed() =>
            enteredEntitiesToBeProcessed.Clear();

        public void ClearExitedEntitiesToBeProcessed() =>
            exitedEntitiesToBeProcessed.Clear();
    }
}
