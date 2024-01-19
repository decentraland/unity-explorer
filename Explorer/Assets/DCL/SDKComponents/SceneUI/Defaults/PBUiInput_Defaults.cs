using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.Unity.ColorComponent;
using UnityEngine;
using Font = DCL.ECSComponents.Font;

namespace DCL.SDKComponents.SceneUI.Defaults
{
    public static class PBUiInput_Defaults
    {
        public static Color GetColor(this PBUiInput self) =>
            self.Color?.ToUnityColor() ?? ColorDefaults.COLOR_BLACK;

        public static Color GetPlaceholderColor(this PBUiInput self) =>
            self.PlaceholderColor?.ToUnityColor() ?? ColorDefaults.PLACEHOLDER_COLOR;

        public static bool IsInteractive(this PBUiInput self) =>
            !self.Disabled;

        public static TextAnchor GetTextAlign(this PBUiInput self) =>
            (self.HasTextAlign ? self.TextAlign : TextAlignMode.TamMiddleCenter).ToUnityTextAlign();

        public static Font GetFont(this PBUiInput self) =>
            self.HasFont ? self.Font : Font.FSansSerif;

        public static float GetFontSize(this PBUiInput self) =>
            self.HasFontSize ? self.FontSize : 10;
    }
}
