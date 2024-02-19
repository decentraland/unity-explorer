using DCL.ECSComponents;
using System;
using UnityEngine;

namespace DCL.CharacterTriggerArea.Components
{
    public struct CharacterTriggerAreaComponent : IDirtyMarker
    {
        public Action<Collider> OnEnteredTrigger;
        public Action<Collider> OnExitedTrigger;
        public Vector3 areaSize;
        public CharacterTriggerArea MonoBehaviour;
        public bool targetOnlyMainPlayer;

        public CharacterTriggerAreaComponent(Vector3 areaSize, Action<Collider> OnEnteredTrigger, Action<Collider> OnExitedTrigger, bool targetOnlyMainPlayer = false)
        {
            this.areaSize = areaSize;
            this.OnEnteredTrigger = OnEnteredTrigger;
            this.OnExitedTrigger = OnExitedTrigger;
            this.targetOnlyMainPlayer = targetOnlyMainPlayer;

            MonoBehaviour = null;

            IsDirty = true;
        }

        public bool IsDirty { get; set; }
    }
}
