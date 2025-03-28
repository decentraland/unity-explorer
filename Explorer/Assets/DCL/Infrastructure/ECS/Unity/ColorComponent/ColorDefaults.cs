using UnityEngine;

namespace ECS.Unity.ColorComponent
{
    public static class ColorDefaults
    {
        public static readonly Color COLOR_WHITE = Color.white;

        public static readonly Color COLOR_BLACK = Color.black;

        public static readonly Color PLACEHOLDER_COLOR = new () { r = 0.3f, g = 0.3f, b = 0.3f, a = 1.0f };
    }
}
