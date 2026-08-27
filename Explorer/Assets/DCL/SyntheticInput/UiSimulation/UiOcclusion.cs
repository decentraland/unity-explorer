using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.SyntheticInput.UiSimulation
{
    /// <summary>
    ///     The occlusion verdict for a semantic uGUI action: the raycast's top hit must be the target, sit inside
    ///     it, or resolve its click to it — otherwise something covers the target and the action must fail
    ///     instead of clicking through the cover. Pure logic over a prepared raycast list, so it is testable
    ///     without a live EventSystem.
    /// </summary>
    public static class UiOcclusion
    {
        public static bool IsTopHitFor(GameObject target, List<RaycastResult> raycastResults, out GameObject? blocker)
        {
            blocker = null;

            if (raycastResults.Count == 0)
                return false;

            GameObject topHit = raycastResults[0].gameObject;

            if (topHit == target || topHit.transform.IsChildOf(target.transform))
                return true;

            if (ExecuteEvents.GetEventHandler<IPointerClickHandler>(topHit) == target)
                return true;

            blocker = topHit;
            return false;
        }
    }
}
