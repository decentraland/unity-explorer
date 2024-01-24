using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.Unity.ColorComponent;
using UnityEngine;
using Font = DCL.ECSComponents.Font;

namespace DCL.SDKComponents.SceneUI.Defaults
{
    public static class PBUiDropdown_Defaults
    {
        public static Color GetColor(this PBUiDropdown self) =>
            self.Color?.ToUnityColor() ?? ColorDefaults.COLOR_BLACK;

        public static Font GetFont(this PBUiDropdown self) =>
            self.HasFont ? self.Font : Font.FSansSerif;

        public static float GetFontSize(this PBUiDropdown self) =>
            self.HasFontSize ? self.FontSize : 10;

        public static bool IsInteractive(this PBUiDropdown self) =>
            !self.Disabled;

        public static int GetSelectedIndex(this PBUiDropdown self) =>
            self.SelectedIndex <= -1 ? (self.AcceptEmpty ? -1 : 0) : self.SelectedIndex;

        public static TextAnchor GetTextAlign(this PBUiDropdown self) =>
            (self.HasTextAlign ? self.TextAlign : TextAlignMode.TamMiddleCenter).ToUnityTextAlign();
    }
}
