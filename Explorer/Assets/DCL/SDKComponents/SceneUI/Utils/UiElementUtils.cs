using Arch.Core;
using CRDT;
using UnityEngine.UIElements;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Classes;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Defaults;
using System.Linq;
using UnityEngine;

namespace DCL.SDKComponents.SceneUI.Utils
{
    public static class UiElementUtils
    {
        public static void SetupVisualElement(ref VisualElement visualElementToSetup, ref PBUiTransform model)
        {
            visualElementToSetup.style.display = GetDisplay(model.Display);
            visualElementToSetup.style.overflow = GetOverflow(model.Overflow);

            // Pointer blocking
            visualElementToSetup.pickingMode = model.PointerFilter == PointerFilterMode.PfmBlock ? PickingMode.Position : PickingMode.Ignore;

            // Flex
            visualElementToSetup.style.flexDirection = GetFlexDirection(model.FlexDirection);
            if (model.FlexBasisUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.flexBasis = model.FlexBasisUnit == YGUnit.YguAuto ? new StyleLength(StyleKeyword.Auto) : new Length(model.FlexBasis, GetUnit(model.FlexBasisUnit));

            visualElementToSetup.style.flexGrow = model.FlexGrow;
            visualElementToSetup.style.flexShrink = model.GetFlexShrink();
            visualElementToSetup.style.flexWrap = GetWrap(model.GetFlexWrap());

            // Align
            visualElementToSetup.style.alignContent = GetAlign(model.GetAlignContent());
            visualElementToSetup.style.alignItems = GetAlign(model.GetAlignItems());
            visualElementToSetup.style.alignSelf = GetAlign(model.AlignSelf);
            visualElementToSetup.style.justifyContent = GetJustify(model.JustifyContent);

            // Layout size
            if (model.HeightUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.height = model.HeightUnit == YGUnit.YguAuto ? new StyleLength(StyleKeyword.Auto) : new Length(model.Height, GetUnit(model.HeightUnit));
            else
                visualElementToSetup.style.height = StyleKeyword.Null;

            if (model.WidthUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.width = model.WidthUnit == YGUnit.YguAuto ? new StyleLength(StyleKeyword.Auto) : new Length(model.Width, GetUnit(model.WidthUnit));
            else
                visualElementToSetup.style.width = StyleKeyword.Null;

            if (model.MaxWidthUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.maxWidth = model.MaxWidthUnit == YGUnit.YguAuto ? new StyleLength(StyleKeyword.Auto) : new Length(model.MaxWidth, GetUnit(model.MaxWidthUnit));
            else
                visualElementToSetup.style.maxWidth = StyleKeyword.Null;

            if (model.MaxHeightUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.maxHeight = model.MaxHeightUnit == YGUnit.YguAuto ? new StyleLength(StyleKeyword.Auto) : new Length(model.MaxHeight, GetUnit(model.MaxHeightUnit));
            else
                visualElementToSetup.style.maxHeight = StyleKeyword.Null;

            if (model.MinHeightUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.minHeight = new Length(model.MinHeight, GetUnit(model.MinHeightUnit));
            else
                visualElementToSetup.style.minHeight = StyleKeyword.Null;

            if (model.MinWidthUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.minWidth = new Length(model.MinWidth, GetUnit(model.MinWidthUnit));
            else
                visualElementToSetup.style.minWidth = StyleKeyword.Null;

            // Paddings
            if (model.PaddingBottomUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.paddingBottom = new Length(model.PaddingBottom, GetUnit(model.PaddingBottomUnit));
            else
                visualElementToSetup.style.paddingBottom = StyleKeyword.Null;

            if (model.PaddingLeftUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.paddingLeft = new Length(model.PaddingLeft, GetUnit(model.PaddingLeftUnit));
            else
                visualElementToSetup.style.paddingLeft = StyleKeyword.Null;

            if (model.PaddingRightUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.paddingRight = new Length(model.PaddingRight, GetUnit(model.PaddingRightUnit));
            else
                visualElementToSetup.style.paddingRight = StyleKeyword.Null;

            if (model.PaddingTopUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.paddingTop = new Length(model.PaddingTop, GetUnit(model.PaddingTopUnit));
            else
                visualElementToSetup.style.paddingTop = StyleKeyword.Null;

            // Margins
            if (model.MarginLeftUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.marginLeft = new Length(model.MarginLeft, GetUnit(model.MarginLeftUnit));
            else
                visualElementToSetup.style.marginLeft = StyleKeyword.Null;

            if (model.MarginRightUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.marginRight = new Length(model.MarginRight, GetUnit(model.MarginRightUnit));
            else
                visualElementToSetup.style.marginRight = StyleKeyword.Null;

            if (model.MarginBottomUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.marginBottom = new Length(model.MarginBottom, GetUnit(model.MarginBottomUnit));
            else
                visualElementToSetup.style.marginBottom = StyleKeyword.Null;

            if (model.MarginTopUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.marginTop = new Length(model.MarginTop, GetUnit(model.MarginTopUnit));
            else
                visualElementToSetup.style.marginTop = StyleKeyword.Null;

            // Position
            visualElementToSetup.style.position = GetPosition(model.PositionType);

            if (model.PositionTopUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.top = new Length(model.PositionTop, GetUnit(model.PositionTopUnit));
            else
                visualElementToSetup.style.top = StyleKeyword.Null;

            if (model.PositionBottomUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.bottom = new Length(model.PositionBottom, GetUnit(model.PositionBottomUnit));
            else
                visualElementToSetup.style.bottom = StyleKeyword.Null;

            if (model.PositionRightUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.right = new Length(model.PositionRight, GetUnit(model.PositionRightUnit));
            else
                visualElementToSetup.style.right = StyleKeyword.Null;

            if (model.PositionLeftUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.left = new Length(model.PositionLeft, GetUnit(model.PositionLeftUnit));
            else
                visualElementToSetup.style.left = StyleKeyword.Null;

            visualElementToSetup.style.backgroundImage = new StyleBackground(StyleKeyword.Null);
            visualElementToSetup.style.backgroundColor = new StyleColor(StyleKeyword.None);
        }

        public static void SetupLabel(ref Label labelToSetup, ref PBUiText model, ref UITransformComponent uiTransformComponent)
        {
            labelToSetup.style.position = new StyleEnum<Position>(Position.Absolute);
            if (uiTransformComponent.Transform.style.width.keyword == StyleKeyword.Auto || uiTransformComponent.Transform.style.height.keyword == StyleKeyword.Auto)
                labelToSetup.style.position = new StyleEnum<Position>(Position.Relative);

            labelToSetup.text = model.Value;
            labelToSetup.style.color = model.GetColor();
            labelToSetup.style.fontSize = model.GetFontSize();
            labelToSetup.style.unityTextAlign = model.GetTextAlign();
            //labelToSetup.style.unityFont = model.GetFont();
        }

        public static void SetupDCLImage(ref DCLImage imageToSetup, ref PBUiBackground model, Texture2D texture = null)
        {
            imageToSetup.Color = model.GetColor();
            imageToSetup.Slices = model.GetBorder();
            imageToSetup.UVs = model.Uvs.ToDCLUVs();
            imageToSetup.ScaleMode = model.TextureMode.ToDCLImageScaleMode();
            imageToSetup.Texture = texture;
        }

        public static void SetupUIInputComponent(ref UIInputComponent inputToSetup, ref PBUiInput model)
        {
            bool isReadonly = !model.IsInteractive();
            inputToSetup.Placeholder.SetPlaceholder(model.Placeholder);
            inputToSetup.Placeholder.SetPlaceholderColor(model.GetPlaceholderColor());
            inputToSetup.Placeholder.SetNormalColor(model.GetColor());
            inputToSetup.Placeholder.SetReadOnly(isReadonly);
            inputToSetup.TextField.isReadOnly = isReadonly;
            inputToSetup.TextField.style.fontSize = model.GetFontSize();
            inputToSetup.TextField.style.unityTextAlign = model.GetTextAlign();
            inputToSetup.TextField.SetValueWithoutNotify(model.HasValue ? model.Value : string.Empty);
            inputToSetup.Placeholder.Refresh();
        }

        public static void SetupUIDropdownComponent(ref UIDropdownComponent dropdownToSetup, ref PBUiDropdown model)
        {
            dropdownToSetup.DropdownField.style.fontSize = model.GetFontSize();
            dropdownToSetup.DropdownField.style.color = model.GetColor();
            dropdownToSetup.DropdownField.choices.Clear();
            dropdownToSetup.DropdownField.choices.AddRange(model.Options);
            dropdownToSetup.DropdownField.SetValueWithoutNotify(dropdownToSetup.DropdownField.choices.ElementAtOrDefault(model.GetSelectedIndex()) ?? model.EmptyLabel);
            dropdownToSetup.DropdownField.EnableInClassList("dcl-dropdown-readonly", model.Disabled);
            dropdownToSetup.DropdownField.pickingMode = model.Disabled ? PickingMode.Ignore : PickingMode.Position;
            dropdownToSetup.TextElement.style.unityTextAlign = model.GetTextAlign();
        }

        public static void SetElementDefaultStyle(IStyle elementStyle)
        {
            elementStyle.right = 0;
            elementStyle.left = 0;
            elementStyle.top = 0;
            elementStyle.bottom = 0;
            elementStyle.width = new StyleLength(StyleKeyword.Auto);
            elementStyle.height = new StyleLength(StyleKeyword.Auto);
            elementStyle.position = new StyleEnum<Position>(Position.Absolute);
            elementStyle.justifyContent = new StyleEnum<Justify>(Justify.Center);
            elementStyle.alignItems = new StyleEnum<Align>(Align.Center);
            elementStyle.whiteSpace = new StyleEnum<WhiteSpace>(WhiteSpace.Normal);
        }

        public static void ReleaseUIElement(VisualElement visualElement) =>
            visualElement.RemoveFromHierarchy();

        public static void ReleaseUITransformComponent(UITransformComponent transform)
        {
            transform.Dispose();
        }

        public static void ReleaseDCLImage(DCLImage image) =>
            image.Dispose();

        public static void ReleaseUIInputComponent(UIInputComponent input)
        {
            input.Dispose();
            ReleaseUIElement(input.TextField);
        }

        public static void ReleaseUIDropdownComponent(UIDropdownComponent dropdown)
        {
            dropdown.Dispose();
            ReleaseUIElement(dropdown.DropdownField);
        }

        private static LengthUnit GetUnit(YGUnit unit)
        {
            switch (unit)
            {
                case YGUnit.YguPercent:
                    return LengthUnit.Percent;
                default:
                    return LengthUnit.Pixel;
            }
        }

        private static StyleEnum<Overflow> GetOverflow(YGOverflow overflow)
        {
            switch (overflow)
            {
                case YGOverflow.YgoHidden:
                    return Overflow.Hidden;
                default:
                    return Overflow.Visible;
            }
        }

        public static StyleEnum<DisplayStyle> GetDisplay(YGDisplay display)
        {
            switch (display)
            {
                case YGDisplay.YgdNone:
                    return DisplayStyle.None;
                default:
                    return DisplayStyle.Flex;
            }
        }

        private static StyleEnum<Justify> GetJustify(YGJustify justify)
        {
            switch (justify)
            {
                case YGJustify.YgjFlexStart:
                    return Justify.FlexStart;
                case YGJustify.YgjCenter:
                    return Justify.Center;
                case YGJustify.YgjFlexEnd:
                    return Justify.FlexEnd;
                case YGJustify.YgjSpaceBetween:
                    return Justify.SpaceBetween;
                case YGJustify.YgjSpaceAround:
                    return Justify.SpaceAround;
                default:
                    return Justify.FlexStart;
            }
        }

        private static StyleEnum<Wrap> GetWrap(YGWrap wrap)
        {
            switch (wrap)
            {
                case YGWrap.YgwNoWrap:
                    return Wrap.NoWrap;
                case YGWrap.YgwWrap:
                    return Wrap.Wrap;
                case YGWrap.YgwWrapReverse:
                    return Wrap.WrapReverse;
                default:
                    return Wrap.Wrap;
            }
        }

        private static StyleEnum<FlexDirection> GetFlexDirection(YGFlexDirection direction)
        {
            switch (direction)
            {
                case YGFlexDirection.YgfdColumn:
                    return FlexDirection.Column;
                case YGFlexDirection.YgfdColumnReverse:
                    return FlexDirection.ColumnReverse;
                case YGFlexDirection.YgfdRow:
                    return FlexDirection.Row;
                case YGFlexDirection.YgfdRowReverse:
                    return FlexDirection.RowReverse;
                default:
                    return FlexDirection.Row;
            }
        }

        private static StyleEnum<Position> GetPosition(YGPositionType positionType)
        {
            switch (positionType)
            {
                case YGPositionType.YgptAbsolute:
                    return Position.Absolute;
                default:
                    return Position.Relative;
            }
        }

        private static StyleEnum<Align> GetAlign(YGAlign align)
        {
            switch (align)
            {
                case YGAlign.YgaAuto:
                    return Align.Auto;
                case YGAlign.YgaFlexStart:
                    return Align.FlexStart;
                case YGAlign.YgaCenter:
                    return Align.Center;
                case YGAlign.YgaFlexEnd:
                    return Align.FlexEnd;
                case YGAlign.YgaStretch:
                    return Align.Stretch;
                default:
                    return Align.Auto;
            }
        }

        public static string BuildElementName(string prefix, in CRDTEntity entity)
        {
#if UNITY_EDITOR
            return $"{prefix} ({entity})";
#else
            return prefix;
#endif
        }

        public static string BuildElementName(string prefix, in Entity entity)
        {
#if UNITY_EDITOR
            return $"{prefix} (Entity {entity.Id})";
#else
            return prefix;
#endif
        }
    }
}
