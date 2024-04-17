#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;
using Toggle = UnityEngine.UIElements.Toggle;

namespace DCL.Input.Utils
{
    public class InteractionCache
    {
        private readonly Dictionary<GameObject, bool> interactionCache = new ();
        private readonly Dictionary<GameObject, PanelEventHandler> uiToolkitPanel = new ();

        public bool IsInteractable(GameObject gameObject, Vector2 pointerPosition)
        {
            if (uiToolkitPanel.TryGetValue(gameObject, out PanelEventHandler? panelEventHandler))
            {
                // we need to convert screen coord to panel coord, since uiElement panel anchor is top-left coordinate we flip the y axis
                Vector2 localCoord = pointerPosition;
                localCoord.y = Screen.height - pointerPosition.y;
                localCoord = RuntimePanelUtils.ScreenToPanel(panelEventHandler.panel, localCoord);

                List<VisualElement>? visualElements = ListPool<VisualElement>.Get();
                panelEventHandler.panel.PickAll(localCoord, visualElements);
                var canBeInteracted = false;

                for (var i = 0; i < visualElements.Count; i++)
                {
                    VisualElement? visualElement = visualElements[i];

                    canBeInteracted = visualElement is Button or Toggle;

                    if (canBeInteracted)
                        break;
                }

                ListPool<VisualElement>.Release(visualElements);
                return canBeInteracted;
            }

            if (interactionCache.TryGetValue(gameObject, out bool result))
                return result;

            // In theory Selectable should cover UnityEngine.UI.Toggle but it does not, weird
            Selectable? isCanvasButton = gameObject.GetComponent<Selectable>();

            if (isCanvasButton)
            {
                interactionCache.Add(gameObject, true);
                return true;
            }

            PanelEventHandler? eventHandler = gameObject.GetComponent<PanelEventHandler>();

            if (eventHandler)
                uiToolkitPanel.Add(gameObject, eventHandler);

            interactionCache.Add(gameObject, false);
            return false;
        }
    }
}
