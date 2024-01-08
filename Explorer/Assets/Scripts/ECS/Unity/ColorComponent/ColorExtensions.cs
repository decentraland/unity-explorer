using Decentraland.Common;
using UnityEngine;

namespace ECS.Unity.ColorComponent
{
    public static class ColorExtensions
    {
        public static Color ToUnityColor(this Color3? color)
        {
            if (color != null)
                return new (color.R, color.G, color.B);

            Debug.LogError("Invalid color provided, using WHITE instead");

            return Color.white;
        }

        public static Color ToUnityColor(this Color4? color)
        {
            if (color != null)
                return new (color.R, color.G, color.B, color.A);

            Debug.LogError("Invalid color provided, using WHITE instead");

            return Color.white;
        }

        public static Color3 ToColor3(this Color color) =>
            new () { B = color.b, G = color.g, R = color.r };

        public static Color4 ToColor4(this Color color) =>
            new () { B = color.b, G = color.g, R = color.r, A = color.a };
    }
}
