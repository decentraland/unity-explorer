using DCL.ECSComponents;
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
        public readonly HashSet<Transform> EnteredThisFrame;
        public readonly HashSet<Transform> ExitedThisFrame;

        public CharacterTriggerAreaComponent(Vector3 areaSize, bool targetOnlyMainPlayer = false)
        {
            AreaSize = areaSize;
            TargetOnlyMainPlayer = targetOnlyMainPlayer;

            MonoBehaviour = null;
            EnteredThisFrame = new HashSet<Transform>();
            ExitedThisFrame = new HashSet<Transform>();

            IsDirty = true;
        }

        public bool IsDirty { get; set; }
    }
}
