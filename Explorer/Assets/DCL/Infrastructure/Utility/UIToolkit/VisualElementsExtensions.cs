using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Utility.UIToolkit
{
    public static class VisualElementsExtensions
    {
        /// <summary>
        ///     USS class of a clickable element: over it the cursor turns into the interaction one, as over a Button.
        /// </summary>
        public const string INTERACTABLE_CLASS = "dcl-interactable";

        public static void SetDisplayed(this VisualElement ve, bool displayed) =>
            ve.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;

        public static bool IsDisplayed(this VisualElement ve) =>
            ve.style.display == DisplayStyle.Flex;

        public static void SetVisible(this VisualElement ve, bool visible) =>
            ve.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;

        public static T InstantiateForElement<T>(this VisualTreeAsset asset) where T: VisualElement =>
            asset.Instantiate().Q<T>();

        /// <summary>Centre of the element in Unity screen pixels (bottom-left origin). The element must be in a panel.</summary>
        public static Vector2 ScreenCenter(this VisualElement element) =>
            element.ScreenPosition(element.worldBound.center);

        /// <summary>
        ///     A panel-space position in Unity screen pixels (bottom-left origin). RuntimePanelUtils only converts
        ///     screen to panel, so the inverse comes from two probe conversions. The element must be in a panel.
        /// </summary>
        public static Vector2 ScreenPosition(this VisualElement element, Vector2 panelPosition)
        {
            IPanel panel = element.panel;
            Vector2 origin = RuntimePanelUtils.ScreenToPanel(panel, Vector2.zero);
            Vector2 perPixel = RuntimePanelUtils.ScreenToPanel(panel, Vector2.one) - origin;

            float x = Mathf.Approximately(perPixel.x, 0f) ? 0f : (panelPosition.x - origin.x) / perPixel.x;
            float y = Mathf.Approximately(perPixel.y, 0f) ? 0f : (panelPosition.y - origin.y) / perPixel.y;

            return new Vector2(x, Screen.height - y);
        }

        /// <summary>
        /// Removes all modifiers (any classes that contain --) from the element.
        /// </summary>
        public static void RemoveModifiers(this VisualElement element)
        {
            var classes = element.GetClasses().ToList();

            foreach (string @class in classes)
                if (@class.Contains("--"))
                    element.RemoveFromClassList(@class);
        }

        /// <summary>
        /// Removes all sprite classes (those starting with sprite-)
        /// </summary>
        public static void RemoveSprites(this VisualElement element)
        {
            var classes = element.GetClasses().ToList();

            foreach (string @class in classes)
                if (@class.StartsWith("sprite-"))
                    element.RemoveFromClassList(@class);
        }
    }
}
