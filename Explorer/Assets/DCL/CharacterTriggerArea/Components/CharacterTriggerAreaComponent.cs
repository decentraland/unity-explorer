using DCL.ECSComponents;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace DCL.CharacterTriggerArea.Components
{
    public struct CharacterTriggerAreaComponent : IDirtyMarker
    {
        public Vector3 AreaSize;
        public CharacterTriggerArea MonoBehaviour;
        public bool TargetOnlyMainPlayer;

        private static readonly IReadOnlyCollection<Transform> empty = Array.Empty<Transform>();

        public IReadOnlyCollection<Transform> EnteredThisFrame => MonoBehaviour?.EnteredThisFrame ?? empty;
        public IReadOnlyCollection<Transform> ExitedThisFrame => MonoBehaviour?.ExitedThisFrame ?? empty;

        public CharacterTriggerAreaComponent(Vector3 areaSize, bool targetOnlyMainPlayer = false)
        {
            AreaSize = areaSize;
            TargetOnlyMainPlayer = targetOnlyMainPlayer;

            MonoBehaviour = null;

            IsDirty = true;
        }

        public bool IsDirty { get; set; }
    }
}
