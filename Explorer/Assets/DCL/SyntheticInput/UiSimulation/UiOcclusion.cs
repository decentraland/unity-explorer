using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace DCL.SyntheticInput.UiSimulation
{
    /// <summary>
    ///     Reasoning about a prepared uGUI raycast list: the occlusion verdict for a semantic action (the top hit
    ///     must be the target, sit inside it, or resolve its click to it — otherwise something covers the target
    ///     and the action must fail instead of clicking through the cover) and the classification of a hit that
    ///     is really a UI Toolkit panel. Pure logic, so it is testable without a live EventSystem.
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

        /// <summary>
        ///     The UI Toolkit panel a raycast hit stands for, if the hit came from a panel rather than from a
        ///     Graphic: <see cref="PanelRaycaster" /> reports the panel <em>host</em> GameObject, so the hit's name
        ///     describes Unity plumbing ("EventSystem/DCLScenePanelSettings") and never the element the panel
        ///     picked. The raycaster itself carries the panel, which is what lets a caller describe the cover in
        ///     the panel's own terms. Matched on the concrete raycaster because the interface that would express
        ///     it (IRuntimePanelComponent) is internal to UI Toolkit.
        /// </summary>
        public static bool TryGetHostedPanel(in RaycastResult hit, out IPanel? panel)
        {
            panel = hit.module is PanelRaycaster panelRaycaster ? panelRaycaster.panel : null;
            return panel != null;
        }
    }
}
