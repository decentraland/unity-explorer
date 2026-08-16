using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKEntityTriggerArea
{
    public class SDKEntityTriggerArea : MonoBehaviour, IDisposable
    {
        [field: SerializeField] public BoxCollider BoxCollider { get; internal set; }
        [field: SerializeField] public SphereCollider SphereCollider { get; internal set; }

        private readonly HashSet<Collider> currentEntitiesInside = new ();
        private readonly HashSet<Collider> enteredEntitiesToBeProcessed = new ();
        private readonly HashSet<Collider> exitedEntitiesToBeProcessed = new ();
        private Predicate<Collider>? isNotTargetEntity;

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

            // Only a tracked presence can produce an exit event: a collider admitted by physics
            // while the target filter was bound was never tracked, so it has no ENTER to balance.
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

            // Invariant: the sets only hold colliders that pass the target filter. Binding a
            // filter re-applies it to colliders tracked while unfiltered — their callbacks are
            // swallowed from now on, so they could never be removed again. A destroyed
            // (fake-null) collider can no longer match the target either.
            isNotTargetEntity ??= entityCollider => entityCollider == null || entityCollider.transform != TargetTransform;
            currentEntitiesInside.RemoveWhere(isNotTargetEntity);
            enteredEntitiesToBeProcessed.RemoveWhere(isNotTargetEntity);
            exitedEntitiesToBeProcessed.RemoveWhere(isNotTargetEntity);
        }
    }
}
