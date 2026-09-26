using UnityEngine;

namespace ECS.Unity.ColorComponent
{
    public static class ColorDefaults
    {
        public static readonly Color COLOR_WHITE = Color.white;

        public static readonly Color COLOR_BLACK = Color.black;

        public static readonly Color PLACEHOLDER_COLOR = new () { r = 0f, g = 0f, b = 0f, a = 0.5f };
    }
}
