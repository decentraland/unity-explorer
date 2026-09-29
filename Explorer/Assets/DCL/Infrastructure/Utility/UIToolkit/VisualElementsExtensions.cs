using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Utility.UIToolkit
{
    public static class VisualElementsExtensions
    {
        public static void SetDisplayed(this VisualElement ve, bool displayed) =>
            ve.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;

        public static bool IsDisplayed(this VisualElement ve) =>
            ve.style.display == DisplayStyle.Flex;

        public static void SetVisible(this VisualElement ve, bool visible) =>
            ve.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;

        public static T InstantiateForElement<T>(this VisualTreeAsset asset) where T: VisualElement =>
            asset.Instantiate().Q<T>();

        /// <summary>
        ///     Centre of the element in Unity screen pixels (bottom-left origin), where uGUI overlays such as the context menus are
        ///     positioned. RuntimePanelUtils offers no panel-to-screen conversion, so the inverse is recovered from two probe conversions.
        ///     The element must be attached to a panel.
        /// </summary>
        public static Vector2 ScreenCenter(this VisualElement element)
        {
            IPanel panel = element.panel;
            Vector2 origin = RuntimePanelUtils.ScreenToPanel(panel, Vector2.zero);
            Vector2 perPixel = RuntimePanelUtils.ScreenToPanel(panel, Vector2.one) - origin;
            Vector2 center = element.worldBound.center;

            float x = Mathf.Approximately(perPixel.x, 0f) ? 0f : (center.x - origin.x) / perPixel.x;
            float y = Mathf.Approximately(perPixel.y, 0f) ? 0f : (center.y - origin.y) / perPixel.y;

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
