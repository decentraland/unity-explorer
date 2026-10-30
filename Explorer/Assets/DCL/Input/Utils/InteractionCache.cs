using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Utility.UIToolkit;
using Button = UnityEngine.UIElements.Button;
using Toggle = UnityEngine.UIElements.Toggle;

namespace DCL.Input.Utils
{
    /// <summary>
    ///     Whether the UI under the pointer would take a click: only the topmost hit and its parents are asked, as a click
    ///     reaches nothing behind them.
    /// </summary>
    public class InteractionCache
    {
        /// <summary>A Selectable never moves to another game object, so each one's own is cached, null included.</summary>
        private readonly Dictionary<GameObject, Selectable?> selectables = new ();
        private readonly Dictionary<GameObject, PanelEventHandler> uiToolkitPanel = new ();

        public bool IsInteractable(GameObject gameObject, Vector2 pointerPosition)
        {
            if (uiToolkitPanel.TryGetValue(gameObject, out PanelEventHandler? panelEventHandler))
                return IsInteractable(panelEventHandler, pointerPosition);

            if (!selectables.ContainsKey(gameObject) && gameObject.TryGetComponent(out PanelEventHandler eventHandler))
            {
                uiToolkitPanel.Add(gameObject, eventHandler);
                return IsInteractable(eventHandler, pointerPosition);
            }

            for (Transform? at = gameObject.transform; at != null; at = at.parent)
            {
                Selectable? selectable = SelectableOf(at.gameObject);

                if (selectable == null || !selectable.isActiveAndEnabled)
                    continue;

                return selectable.IsInteractable();
            }

            return false;
        }

        private static bool IsInteractable(PanelEventHandler panelEventHandler, Vector2 pointerPosition)
        {
            // The panel space is anchored top-left, so the y axis is flipped.
            Vector2 localCoord = pointerPosition;
            localCoord.y = Screen.height - pointerPosition.y;
            localCoord = RuntimePanelUtils.ScreenToPanel(panelEventHandler.panel, localCoord);

            for (VisualElement? at = panelEventHandler.panel.Pick(localCoord); at != null; at = at.parent)
                if (at.enabledInHierarchy && (at is Button or Toggle || at.ClassListContains(VisualElementsExtensions.INTERACTABLE_CLASS)))
                    return true;

            return false;
        }

        private Selectable? SelectableOf(GameObject gameObject)
        {
            if (selectables.TryGetValue(gameObject, out Selectable? selectable))
                return selectable;

            gameObject.TryGetComponent(out selectable);
            selectables.Add(gameObject, selectable);
            return selectable;
        }
    }
}
