using UnityEngine.UIElements;

namespace Utility.UIToolkit
{
    public static class VisualElementsExtensions
    {
        public static void SetDisplayed(this VisualElement ve, bool displayed) =>
            ve.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;

        public static T InstantiateForElement<T>(this VisualTreeAsset asset) where T: VisualElement =>
            asset.Instantiate().Q<T>();
    }
}
