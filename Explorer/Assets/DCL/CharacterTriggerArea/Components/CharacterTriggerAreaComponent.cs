using DCL.ECSComponents;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.CharacterTriggerArea.Components
{
    public struct CharacterTriggerAreaComponent : IDirtyMarker
    {
        private static readonly IReadOnlyCollection<Transform> EMPTY_COLLECTION = Array.Empty<Transform>();
        public Vector3 AreaSize;
        public CharacterTriggerArea MonoBehaviour;
        public bool TargetOnlyMainPlayer;

        public IReadOnlyCollection<Transform> EnteredThisFrame => MonoBehaviour?.EnteredThisFrame ?? EMPTY_COLLECTION;
        public IReadOnlyCollection<Transform> ExitedThisFrame => MonoBehaviour?.ExitedThisFrame ?? EMPTY_COLLECTION;
        public IReadOnlyCollection<Transform> CurrentAvatarsInside => MonoBehaviour?.CurrentAvatarsInside ?? EMPTY_COLLECTION;

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
