using DCL.ECSComponents;
using System;

namespace DCL.MainPlayerTriggerArea
{
    public class MainPlayerTriggerAreaComponent : IDirtyMarker
    {
        public Action OnEnteredTrigger;
        public Action OnExitedTrigger;
        public bool IsDirty { get; set; } = true;
    }
}
