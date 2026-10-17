using DCL.ECSComponents;
using Decentraland.Common;
using ECS.Unity.ColorComponent;
using UnityEngine;

namespace DCL.SDKComponents.SceneUI.Defaults
{
    public static class PBUiBackgroundDefaults
    {
        private static readonly BorderRect DEFAULT_SLICES = new ()
            { Left = 1 / 3f, Bottom = 1 / 3f, Right = 1 / 3f, Top = 1 / 3f };

        public static Color GetColor(this PBUiBackground self) =>
            self.Color?.ToUnityColor() ?? ColorDefaults.COLOR_WHITE;

        public static Vector4 GetBorder(this PBUiBackground self)
        {
            var rect = self.TextureSlices ?? DEFAULT_SLICES;
            return rect == null ? Vector4.zero : new Vector4(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }
    }
}
