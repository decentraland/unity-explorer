using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace DCL.SyntheticInput.UiSimulation
{
    /// <summary>Pure checks over a prepared uGUI raycast list, testable without a live EventSystem.</summary>
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

        /// <summary>
        ///     Matched on the concrete <see cref="PanelRaycaster" /> because IRuntimePanelComponent is internal to
        ///     UI Toolkit.
        /// </summary>
        public static bool TryGetHostedPanel(in RaycastResult hit, out IPanel? panel)
        {
            panel = hit.module is PanelRaycaster panelRaycaster ? panelRaycaster.panel : null;
            return panel != null;
        }
    }
}
