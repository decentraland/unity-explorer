using DCL.ECSComponents;
using ECS.Unity.ColorComponent;
using UnityEngine;
using Font = DCL.ECSComponents.Font;

namespace DCL.SDKComponents.SceneUI.Defaults
{
    public static class PBUiInput_Defaults
    {
        private static readonly Color PLACEHOLDER_COLOR = new () { r = 0.3f, g = 0.3f, b = 0.3f, a = 1.0f };

        public static Color GetColor(this PBUiInput self) =>
            self.Color?.ToUnityColor() ?? ColorDefaults.COLOR_BLACK;

        public static Color GetPlaceholderColor(this PBUiInput self) =>
            self.PlaceholderColor?.ToUnityColor() ?? PLACEHOLDER_COLOR;

        public static bool IsInteractive(this PBUiInput self) =>
            !self.Disabled;

        public static TextAlignMode GetTextAlign(this PBUiInput self) =>
            self.HasTextAlign ? self.TextAlign : TextAlignMode.TamMiddleCenter;

        public static Font GetFont(this PBUiInput self) =>
            self.HasFont ? self.Font : Font.FSansSerif;

        public static float GetFontSize(this PBUiInput self) =>
            self.HasFontSize ? self.FontSize : 10;
    }
}
