using UnityEngine.UIElements;

namespace Utils
{
    public static class UIExtensions
    {
        public static void RotateBy(this VisualElement element, float angle)
        {
            element.style.rotate = new StyleRotate(new Rotate(new Angle(element.style.rotate.value.angle.value + angle)));
        }

        public static void SetDisplay(this VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public static void SetVisibility(this VisualElement element, bool visible)
        {
            element.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;
        }
    }
}