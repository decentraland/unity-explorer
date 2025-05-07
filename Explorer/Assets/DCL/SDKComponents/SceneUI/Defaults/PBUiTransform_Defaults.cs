using CRDT;
using DCL.ECSComponents;
using ECS.Unity.ColorComponent;
using UnityEngine;

namespace DCL.SDKComponents.SceneUI.Defaults
{
    public static class PBUiTransform_Defaults
    {
        public static CRDTEntity GetRightOfEntity(this PBUiTransform self) =>
            self.RightOf;

        public static float GetFlexShrink(this PBUiTransform self) =>
            self.HasFlexShrink ? self.FlexShrink : 1;

        public static YGAlign GetAlignItems(this PBUiTransform self) =>
            self.HasAlignItems ? self.AlignItems : YGAlign.YgaStretch;

        public static YGAlign GetAlignContent(this PBUiTransform self) =>
            self.HasAlignContent ? self.AlignContent : YGAlign.YgaStretch;

        public static YGWrap GetFlexWrap(this PBUiTransform self) =>
            self.HasFlexWrap ? self.FlexWrap : YGWrap.YgwNoWrap;

        public static Color GetBorderBottomColor(this PBUiTransform self) =>
            self.BorderBottomColor?.ToUnityColor() ?? Color.clear;

        public static Color GetBorderTopColor(this PBUiTransform self) =>
            self.BorderTopColor?.ToUnityColor() ?? Color.clear;

        public static Color GetBorderLeftColor(this PBUiTransform self) =>
            self.BorderLeftColor?.ToUnityColor() ?? Color.clear;

        public static Color GetBorderRightColor(this PBUiTransform self) =>
            self.BorderRightColor?.ToUnityColor() ?? Color.clear;
    }
}
