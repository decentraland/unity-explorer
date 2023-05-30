using Decentraland.Common;
using UnityEngine;

namespace ECS.Unity.ColorComponent
{
    public static class ColorExtensions
    {
        public static Color ToUnityColor(this Color3 color) =>
            new (color.R, color.G, color.B);

        public static Color ToUnityColor(this Color4 color) =>
            new (color.R, color.G, color.B, color.A);
    }
}
