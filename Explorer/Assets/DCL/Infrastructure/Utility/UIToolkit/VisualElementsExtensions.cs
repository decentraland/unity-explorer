using System.Linq;
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
