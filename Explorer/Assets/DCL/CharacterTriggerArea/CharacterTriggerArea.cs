using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.CharacterTriggerArea
{
    public class CharacterTriggerArea : MonoBehaviour, IDisposable
    {
        [field: SerializeField] public BoxCollider BoxCollider { get; private set; }

        // [field: NonSerialized]
        public HashSet<Transform> EnteredThisFrame;

        // [field: NonSerialized]
        public HashSet<Transform> ExitedThisFrame;
        [field: NonSerialized] public Transform TargetTransform;

        // TODO: does this fuck up systems reading of the hashsets???
        // private void LateUpdate()
        // {
        //     EnteredThisFrame.Clear();
        //     ExitedThisFrame.Clear();
        // }

        public void OnTriggerEnter(Collider other)
        {
            if (TargetTransform != null && TargetTransform != other.transform) return;

            EnteredThisFrame.Add(other.transform);
        }

        public void OnTriggerExit(Collider other)
        {
            if (TargetTransform != null && TargetTransform != other.transform) return;

            ExitedThisFrame.Add(other.transform);
        }

        public void Dispose()
        {
            BoxCollider.enabled = false;
            EnteredThisFrame.Clear();
            ExitedThisFrame.Clear();
        }
    }
}
