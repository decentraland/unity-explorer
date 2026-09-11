using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKEntityTriggerArea
{
    public class SDKEntityTriggerArea : MonoBehaviour, IDisposable
    {
        [field: SerializeField] public BoxCollider BoxCollider { get; internal set; } = null!;
        [field: SerializeField] public SphereCollider SphereCollider { get; internal set; } = null!;

        private readonly HashSet<Collider> currentEntitiesInside = new ();
        private readonly HashSet<Collider> enteredEntitiesToBeProcessed = new ();
        private readonly HashSet<Collider> exitedEntitiesToBeProcessed = new ();

        public Transform? TargetTransform { get; private set; }

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

            enteredEntitiesToBeProcessed.Remove(other);

            // Untracked colliders (filtered on enter) have no ENTER to balance.
            if (!currentEntitiesInside.Remove(other)) return;

            exitedEntitiesToBeProcessed.Add(other);
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

        public bool IsEnterPending(Collider entityCollider) =>
            enteredEntitiesToBeProcessed.Contains(entityCollider);

        public void SetTargetTransform(Transform? targetTransform)
        {
            TargetTransform = targetTransform;

            if (targetTransform == null) return;

            // Evict colliders the filter will swallow callbacks for; they could never be removed otherwise.
            currentEntitiesInside.RemoveWhere(IsNotTargetEntity);
            enteredEntitiesToBeProcessed.RemoveWhere(IsNotTargetEntity);
            exitedEntitiesToBeProcessed.RemoveWhere(IsNotTargetEntity);
            return;

            bool IsNotTargetEntity(Collider entityCollider) =>
                entityCollider == null || entityCollider.transform != TargetTransform;
        }
    }
}
