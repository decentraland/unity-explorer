using DCL.ECSComponents;
using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace DCL.CharacterTriggerArea.Components
{
    public struct CharacterTriggerAreaComponent : IDirtyMarker
    {
        public Action<Collider> OnEnteredTrigger;
        public Action<Collider> OnExitedTrigger;
        public Vector3 AreaSize;
        public CharacterTriggerArea MonoBehaviour;
        public bool TargetOnlyMainPlayer;

        public CharacterTriggerAreaComponent(Vector3 areaSize, Action<Collider> onEnteredTrigger, Action<Collider> onExitedTrigger, bool targetOnlyMainPlayer = false)
        {
            AreaSize = areaSize;
            OnEnteredTrigger = onEnteredTrigger;
            OnExitedTrigger = onExitedTrigger;
            TargetOnlyMainPlayer = targetOnlyMainPlayer;

            MonoBehaviour = null;

            IsDirty = true;
        }

        public bool IsDirty { get; set; }
    }
}
