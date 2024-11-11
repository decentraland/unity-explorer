using UnityEngine;

namespace DCL.InWorldCamera.CameraReel.Components
{
    public static class RectTransformUtils
    {
        public static Rect GetWorldRect(this RectTransform rectTransform)
        {
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            Vector2 min = corners[0];
            Vector2 max = corners[2];
            Vector2 size = max - min;
            return new Rect(min, size);
        }
    }
}
