using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.Unity.ColorComponent;
using UnityEngine;
using Font = DCL.ECSComponents.Font;

namespace DCL.SDKComponents.SceneUI.Defaults
{
    public static class PBUiTextDefaults
    {
        public static Color GetColor(this PBUiText self) =>
            self.Color?.ToUnityColor() ?? ColorDefaults.COLOR_WHITE;

        public static TextAnchor GetTextAlign(this PBUiText self) =>
            (self.HasTextAlign ? self.TextAlign : TextAlignMode.TamMiddleCenter).ToUnityTextAlign();

        public static Font GetFont(this PBUiText self) =>
            self.HasFont ? self.Font : Font.FSansSerif;

        public static float GetFontSize(this PBUiText self) =>
            self.HasFontSize ? self.FontSize : 10;
    }
}
