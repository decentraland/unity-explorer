using DCL.ECSComponents;
using System;
using UnityEngine;

namespace DCL.MainPlayerTriggerArea
{
    public struct MainPlayerTriggerAreaComponent : IDirtyMarker
    {
        public Action OnEnteredTrigger;
        public Action OnExitedTrigger;
        public Vector3 areaSize;
        public MainPlayerTriggerArea MonoBehaviour;

        public MainPlayerTriggerAreaComponent(Vector3 areaSize, Action OnEnteredTrigger, Action OnExitedTrigger)
        {
            this.areaSize = areaSize;
            this.OnEnteredTrigger = OnEnteredTrigger;
            this.OnExitedTrigger = OnExitedTrigger;

            MonoBehaviour = null;

            IsDirty = true;
        }

        public bool IsDirty { get; set; }
    }
}
