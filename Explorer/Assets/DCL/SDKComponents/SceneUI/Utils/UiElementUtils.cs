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
        // Ratio (~0.22) matches ScrollRectExtensions (0.45/2 = 0.225) to keep scroll feel
        // consistent across UGUI and UI Toolkit. Absolute values differ because
        // ScrollRect.scrollSensitivity is a delta multiplier while ScrollView.mouseWheelScrollSize is pixels per tick.
        private const float MACOS_MOUSE_WHEEL_SCROLL_SIZE = 4f;
        private const float DEFAULT_MOUSE_WHEEL_SCROLL_SIZE = 18f;

        private static readonly float scrollViewMouseWheelScrollSize =
            Application.platform is RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer
                ? MACOS_MOUSE_WHEEL_SCROLL_SIZE
                : DEFAULT_MOUSE_WHEEL_SCROLL_SIZE;

        public static void SetupTransformVisualElement(VisualElement transformVisualElement, ref PBUiTransform model)
        {
            transformVisualElement.style.display = GetDisplay(model.Display);
            transformVisualElement.style.overflow = GetOverflow(model.Overflow);

            // Pointer blocking
            transformVisualElement.pickingMode = model.PointerFilter == PointerFilterMode.PfmBlock ? PickingMode.Position : PickingMode.Ignore;

            // Flex
            transformVisualElement.style.flexDirection = GetFlexDirection(model.FlexDirection);
            if (model.FlexBasisUnit != YGUnit.YguUndefined)
                transformVisualElement.style.flexBasis = model.FlexBasisUnit == YGUnit.YguAuto ? new StyleLength(StyleKeyword.Auto) : new Length(model.FlexBasis, GetUnit(model.FlexBasisUnit));

            transformVisualElement.style.flexGrow = model.FlexGrow;
            transformVisualElement.style.flexShrink = model.GetFlexShrink();
            transformVisualElement.style.flexWrap = GetWrap(model.GetFlexWrap());

            // Align
            transformVisualElement.style.alignContent = GetAlign(model.GetAlignContent());
            transformVisualElement.style.alignItems = GetAlign(model.GetAlignItems());
            transformVisualElement.style.alignSelf = GetAlign(model.AlignSelf);
            transformVisualElement.style.justifyContent = GetJustify(model.JustifyContent);

            // Border
            var defaultStyleFloat = new StyleFloat(StyleKeyword.Undefined);
            transformVisualElement.style.borderBottomColor = model.GetBorderBottomColor();
            transformVisualElement.style.borderTopColor = model.GetBorderTopColor();
            transformVisualElement.style.borderLeftColor = model.GetBorderLeftColor();
            transformVisualElement.style.borderRightColor = model.GetBorderRightColor();
            transformVisualElement.style.borderBottomWidth = model.HasBorderBottomWidth ? model.BorderBottomWidth : defaultStyleFloat;
            transformVisualElement.style.borderTopWidth = model.HasBorderTopWidth ? model.BorderTopWidth : defaultStyleFloat;
            transformVisualElement.style.borderLeftWidth = model.HasBorderLeftWidth ? model.BorderLeftWidth : defaultStyleFloat;
            transformVisualElement.style.borderRightWidth = model.HasBorderRightWidth ? model.BorderRightWidth : defaultStyleFloat;
            transformVisualElement.style.borderBottomLeftRadius = GetBorderRadius(
                model.HasBorderBottomLeftRadius,
                model.HasBorderBottomLeftRadiusUnit,
                model.BorderBottomLeftRadius,
                model.BorderBottomLeftRadiusUnit
            );
            transformVisualElement.style.borderBottomRightRadius = GetBorderRadius(
                model.HasBorderBottomRightRadius,
                model.HasBorderBottomRightRadiusUnit,
                model.BorderBottomRightRadius,
                model.BorderBottomRightRadiusUnit
            );
            transformVisualElement.style.borderTopLeftRadius = GetBorderRadius(
                model.HasBorderTopLeftRadius,
                model.HasBorderTopLeftRadiusUnit,
                model.BorderTopLeftRadius,
                model.BorderTopLeftRadiusUnit
            );
            transformVisualElement.style.borderTopRightRadius = GetBorderRadius(
                model.HasBorderTopRightRadius,
                model.HasBorderTopRightRadiusUnit,
                model.BorderTopRightRadius,
                model.BorderTopRightRadiusUnit
            );

            // Layout size
            if (model.HeightUnit != YGUnit.YguUndefined)
                transformVisualElement.style.height = model.HeightUnit == YGUnit.YguAuto ? new StyleLength(StyleKeyword.Auto) : new Length(model.Height, GetUnit(model.HeightUnit));
            else
                transformVisualElement.style.height = StyleKeyword.Null;

            if (model.WidthUnit != YGUnit.YguUndefined)
                transformVisualElement.style.width = model.WidthUnit == YGUnit.YguAuto ? new StyleLength(StyleKeyword.Auto) : new Length(model.Width, GetUnit(model.WidthUnit));
            else
                transformVisualElement.style.width = StyleKeyword.Null;

            if (model.MaxWidthUnit != YGUnit.YguUndefined)
                transformVisualElement.style.maxWidth = model.MaxWidthUnit == YGUnit.YguAuto ? new StyleLength(StyleKeyword.Auto) : new Length(model.MaxWidth, GetUnit(model.MaxWidthUnit));
            else
                transformVisualElement.style.maxWidth = StyleKeyword.Null;

            if (model.MaxHeightUnit != YGUnit.YguUndefined)
                transformVisualElement.style.maxHeight = model.MaxHeightUnit == YGUnit.YguAuto ? new StyleLength(StyleKeyword.Auto) : new Length(model.MaxHeight, GetUnit(model.MaxHeightUnit));
            else
                transformVisualElement.style.maxHeight = StyleKeyword.Null;

            if (model.MinHeightUnit != YGUnit.YguUndefined)
                transformVisualElement.style.minHeight = new Length(model.MinHeight, GetUnit(model.MinHeightUnit));
            else
                transformVisualElement.style.minHeight = StyleKeyword.Null;

            if (model.MinWidthUnit != YGUnit.YguUndefined)
                transformVisualElement.style.minWidth = new Length(model.MinWidth, GetUnit(model.MinWidthUnit));
            else
                transformVisualElement.style.minWidth = StyleKeyword.Null;

            // Paddings
            if (model.PaddingBottomUnit != YGUnit.YguUndefined)
                transformVisualElement.style.paddingBottom = new Length(model.PaddingBottom, GetUnit(model.PaddingBottomUnit));
            else
                transformVisualElement.style.paddingBottom = StyleKeyword.Null;

            if (model.PaddingLeftUnit != YGUnit.YguUndefined)
                transformVisualElement.style.paddingLeft = new Length(model.PaddingLeft, GetUnit(model.PaddingLeftUnit));
            else
                transformVisualElement.style.paddingLeft = StyleKeyword.Null;

            if (model.PaddingRightUnit != YGUnit.YguUndefined)
                transformVisualElement.style.paddingRight = new Length(model.PaddingRight, GetUnit(model.PaddingRightUnit));
            else
                transformVisualElement.style.paddingRight = StyleKeyword.Null;

            if (model.PaddingTopUnit != YGUnit.YguUndefined)
                transformVisualElement.style.paddingTop = new Length(model.PaddingTop, GetUnit(model.PaddingTopUnit));
            else
                transformVisualElement.style.paddingTop = StyleKeyword.Null;

            // Margins
            if (model.MarginLeftUnit != YGUnit.YguUndefined)
                transformVisualElement.style.marginLeft = new Length(model.MarginLeft, GetUnit(model.MarginLeftUnit));
            else
                transformVisualElement.style.marginLeft = StyleKeyword.Null;

            if (model.MarginRightUnit != YGUnit.YguUndefined)
                transformVisualElement.style.marginRight = new Length(model.MarginRight, GetUnit(model.MarginRightUnit));
            else
                transformVisualElement.style.marginRight = StyleKeyword.Null;

            if (model.MarginBottomUnit != YGUnit.YguUndefined)
                transformVisualElement.style.marginBottom = new Length(model.MarginBottom, GetUnit(model.MarginBottomUnit));
            else
                transformVisualElement.style.marginBottom = StyleKeyword.Null;

            if (model.MarginTopUnit != YGUnit.YguUndefined)
                transformVisualElement.style.marginTop = new Length(model.MarginTop, GetUnit(model.MarginTopUnit));
            else
                transformVisualElement.style.marginTop = StyleKeyword.Null;

            // Position
            transformVisualElement.style.position = GetPosition(model.PositionType);

            if (model.PositionTopUnit != YGUnit.YguUndefined)
                transformVisualElement.style.top = new Length(model.PositionTop, GetUnit(model.PositionTopUnit));
            else
                transformVisualElement.style.top = StyleKeyword.Null;

            if (model.PositionBottomUnit != YGUnit.YguUndefined)
                transformVisualElement.style.bottom = new Length(model.PositionBottom, GetUnit(model.PositionBottomUnit));
            else
                transformVisualElement.style.bottom = StyleKeyword.Null;

            if (model.PositionRightUnit != YGUnit.YguUndefined)
                transformVisualElement.style.right = new Length(model.PositionRight, GetUnit(model.PositionRightUnit));
            else
                transformVisualElement.style.right = StyleKeyword.Null;

            if (model.PositionLeftUnit != YGUnit.YguUndefined)
                transformVisualElement.style.left = new Length(model.PositionLeft, GetUnit(model.PositionLeftUnit));
            else
                transformVisualElement.style.left = StyleKeyword.Null;

            transformVisualElement.style.opacity = model.HasOpacity ? model.Opacity : 1;
        }

        /// <summary>
        /// Ensures the transform has an inner ScrollView when overflow is Scroll, or removes it otherwise.
        /// When enabling scroll, moves all current Transform children into the ScrollView's contentContainer.
        /// When disabling, moves them back and disposes the ScrollView.
        /// </summary>
        public static void EnsureScrollMode(UITransformComponent component, in PBUiTransform model)
        {
            bool wantScroll = model.Overflow == YGOverflow.YgoScroll;
            VisualElement transform = component.Transform;

            if (wantScroll)
            {
                if (component.InnerScrollView == null)
                {
                    var scrollView = new ScrollView
                    {
                        horizontalScrollerVisibility = ScrollerVisibility.AlwaysVisible,
                        verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible,
                        mouseWheelScrollSize = scrollViewMouseWheelScrollSize
                    };
                    scrollView.style.flexGrow = 1;
                    scrollView.style.width = new Length(100, LengthUnit.Percent);
                    scrollView.style.height = new Length(100, LengthUnit.Percent);

                    while (transform.childCount > 0)
                    {
                        var child = transform[0];
                        child.RemoveFromHierarchy();
                        scrollView.contentContainer.Add(child);
                    }

                    transform.Add(scrollView);
                    component.InnerScrollView = scrollView;
                }

                // PBUiTransform properties that have to be propagated into the ContentContainer
                var contentStyle = component.InnerScrollView.contentContainer.style;
                contentStyle.flexDirection = GetFlexDirection(model.FlexDirection);
                contentStyle.justifyContent = GetJustify(model.JustifyContent);
                contentStyle.alignItems = GetAlign(model.GetAlignItems());
                contentStyle.alignContent = GetAlign(model.GetAlignContent());
                contentStyle.flexWrap = GetWrap(model.GetFlexWrap());
            }
            else if (component.InnerScrollView != null)
            {
                var scrollView = component.InnerScrollView;
                var content = scrollView.contentContainer;
                while (content.childCount > 0)
                {
                    var child = content[0];
                    child.RemoveFromHierarchy();
                    transform.Add(child);
                }
                scrollView.RemoveFromHierarchy();
                component.InnerScrollView = null;
            }
        }

        public static void SetupLabel(ref Label labelToSetup, ref PBUiText model, ref UITransformComponent uiTransformComponent, in StyleFontDefinition[] styleFontDefinitions)
        {
            labelToSetup.style.position = new StyleEnum<Position>(Position.Absolute);
            if (uiTransformComponent.Transform.style.width.keyword == StyleKeyword.Auto || uiTransformComponent.Transform.style.height.keyword == StyleKeyword.Auto)
                labelToSetup.style.position = new StyleEnum<Position>(Position.Relative);

            labelToSetup.text = model.Value;
            labelToSetup.style.color = model.GetColor();
            labelToSetup.style.fontSize = model.GetFontSize();
            labelToSetup.style.unityTextAlign = model.GetTextAlign();

            int font = (int)model.GetFont();
            if (font < styleFontDefinitions.Length)
                labelToSetup.style.unityFontDefinition = styleFontDefinitions[font];

            if (model.HasTextWrap)
                labelToSetup.style.whiteSpace = model.TextWrap == TextWrap.TwWrap ? WhiteSpace.Normal : WhiteSpace.NoWrap;
            else
                labelToSetup.style.whiteSpace = WhiteSpace.NoWrap;
        }

        public static void SetupFromSdkModel(this DCLImage imageToSetup, ref PBUiBackground model, Texture? texture = null)
        {
            imageToSetup.Color = model.GetColor();
            imageToSetup.Slices = model.GetBorder();
            imageToSetup.UVs = model.Uvs.ToDCLUVs();
            imageToSetup.ScaleMode = model.TextureMode.ToDCLImageScaleMode();
            imageToSetup.Texture = texture;
        }

        public static void SetupUIInputComponent(ref UIInputComponent inputToSetup, in PBUiInput model, in StyleFontDefinition[] styleFontDefinitions)
        {
            bool isReadonly = !model.IsInteractive();

            inputToSetup.Placeholder.PlaceholderText = model.Placeholder;
            inputToSetup.Placeholder.PlaceholderColor = model.GetPlaceholderColor();
            inputToSetup.Placeholder.NormalColor = model.GetColor();
            inputToSetup.Placeholder.IsReadonly = isReadonly;

            inputToSetup.TextField.isReadOnly = isReadonly;
            inputToSetup.TextField.style.fontSize = model.GetFontSize();

            int font = (int)model.GetFont();
            if (font < styleFontDefinitions.Length)
                inputToSetup.TextField.style.unityFontDefinition = styleFontDefinitions[font];

            inputToSetup.TextField.SetValueWithoutNotify(model.HasValue ? model.Value : string.Empty);
            inputToSetup.Placeholder.Refresh();

            inputToSetup.TextField.pickingMode = model.Disabled ? PickingMode.Ignore : PickingMode.Position;
            inputToSetup.TextField.SetEnabled(!model.Disabled);
            inputToSetup.TextElement.style.unityTextAlign = model.GetTextAlign();
        }

        public static void SetupUIDropdownComponent(ref UIDropdownComponent dropdownToSetup, in PBUiDropdown model, in StyleFontDefinition[] styleFontDefinitions)
        {
            var dropdownField = dropdownToSetup.DropdownField;
            dropdownField.style.fontSize = model.GetFontSize();
            dropdownField.style.color = model.GetColor();

            int font = (int)model.GetFont();
            if (font < styleFontDefinitions.Length)
                dropdownField.style.unityFontDefinition = styleFontDefinitions[font];

            dropdownField.choices.Clear();
            dropdownField.choices.AddRange(model.Options);

            int selectedIndex = model.GetSelectedIndex();
            if (selectedIndex != dropdownToSetup.LastIndexSetByScene)
            {
                var newValue = dropdownField.choices.ElementAtOrDefault(selectedIndex) ?? model.EmptyLabel;

                // Below '-1' is checked, because '-1' is used for the case of 'accept Empty value'
                if (dropdownToSetup.LastIndexSetByScene < -1)
                    dropdownField.SetValueWithoutNotify(newValue);
                else
                    dropdownField.value = newValue;

                dropdownToSetup.LastIndexSetByScene = selectedIndex;
            }

            dropdownToSetup.TextElement.style.unityTextAlign = model.GetTextAlign();

            var arrowIcon = dropdownField.Q(null, "unity-base-popup-field__arrow");
            arrowIcon.AddToClassList("sprite-common__icon-arrow-down");

            dropdownField.pickingMode = model.Disabled ? PickingMode.Ignore : PickingMode.Position;
            dropdownField.SetEnabled(!model.Disabled);
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
                case YGOverflow.YgoScroll:
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

        private static StyleLength GetBorderRadius(bool hasBorderRadius, bool hasBorderRadiusUnit, float borderRadius, YGUnit borderRadiusUnit)
        {
            if (!hasBorderRadius)
                return StyleKeyword.Undefined;

            LengthUnit unit = hasBorderRadiusUnit && borderRadiusUnit == YGUnit.YguPercent ?
                LengthUnit.Percent : LengthUnit.Pixel;

            return new Length(borderRadius, unit);
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

        public static void ConfigureHoverStylesBehaviour(World world, Entity entity, in UITransformComponent uiTransformComponent, VisualElement hoverEventTarget, float borderDarkenFactor, float backgroundDarkenFactor)
        {
            var transform = uiTransformComponent.Transform;
            var borderTopColor = transform.resolvedStyle.borderTopColor;
            var borderRightColor = transform.resolvedStyle.borderRightColor;
            var borderBottomColor = transform.resolvedStyle.borderBottomColor;
            var borderLeftColor = transform.resolvedStyle.borderLeftColor;

            var data = new Extensions.HoverStyleBehaviourData(
                hoverEventTarget,
                transform,
                world,
                entity,
                borderDarkenFactor,
                backgroundDarkenFactor,
                borderTopColor,
                borderRightColor,
                borderBottomColor,
                borderLeftColor);

            hoverEventTarget.RegisterHoverStyleCallbacks(data);
        }

        private const float UI_TRANSFORM_DEFAULT_RADIUS = 10f;
        private const float UI_TRANSFORM_DEFAULT_BORDER_WIDTH = 1f;
        /// <summary>
        /// Applies default UI transform styles for interactive UI elements (dropdowns, inputs).
        /// Sets overflow to hidden, and applies default border radius, border width, and border color
        /// when these properties are not explicitly defined in the PBUiTransform component.
        /// </summary>
        public static void ApplyDefaultUiTransformValues(in PBUiTransform pbUiTransform, VisualElement uiTransform)
        {
            uiTransform.style.overflow = new StyleEnum<Overflow>(Overflow.Hidden);

            if (pbUiTransform is
                {
                    HasBorderBottomLeftRadius: false,
                    HasBorderBottomRightRadius: false,
                    HasBorderTopLeftRadius: false,
                    HasBorderTopRightRadius: false
                })
            {
                uiTransform.style.borderBottomLeftRadius = new StyleLength(UI_TRANSFORM_DEFAULT_RADIUS);
                uiTransform.style.borderBottomRightRadius = new StyleLength(UI_TRANSFORM_DEFAULT_RADIUS);
                uiTransform.style.borderTopLeftRadius = new StyleLength(UI_TRANSFORM_DEFAULT_RADIUS);
                uiTransform.style.borderTopRightRadius = new StyleLength(UI_TRANSFORM_DEFAULT_RADIUS);
            }

            if (pbUiTransform is
                {
                    HasBorderTopWidth: false,
                    HasBorderRightWidth: false,
                    HasBorderBottomWidth: false,
                    HasBorderLeftWidth: false
                })
            {
                uiTransform.style.borderTopWidth = new StyleFloat(UI_TRANSFORM_DEFAULT_BORDER_WIDTH);
                uiTransform.style.borderRightWidth = new StyleFloat(UI_TRANSFORM_DEFAULT_BORDER_WIDTH);
                uiTransform.style.borderBottomWidth = new StyleFloat(UI_TRANSFORM_DEFAULT_BORDER_WIDTH);
                uiTransform.style.borderLeftWidth = new StyleFloat(UI_TRANSFORM_DEFAULT_BORDER_WIDTH);
            }

            if (pbUiTransform is
                {
                    BorderTopColor: null,
                    BorderRightColor: null,
                    BorderBottomColor: null,
                    BorderLeftColor: null
                })
            {
                uiTransform.style.borderTopColor = new StyleColor(Color.gray);
                uiTransform.style.borderRightColor = new StyleColor(Color.gray);
                uiTransform.style.borderBottomColor = new StyleColor(Color.gray);
                uiTransform.style.borderLeftColor = new StyleColor(Color.gray);
            }
        }

        /// <summary>
        /// Applies a default background color to UI elements that don't have an explicit PBUiBackground component.
        /// This ensures interactive elements like dropdowns and inputs have a visible background by default.
        /// </summary>
        public static void ApplyDefaultUiBackgroundValues(World world, Entity entity, VisualElement uiTransform)
        {
            if (world.Has<PBUiBackground>(entity)) return;

            uiTransform.style.backgroundColor = new StyleColor(Color.white);
        }

        /// <summary>
        /// Clears the default interactive styles (overflow, background, default borders) applied by
        /// <see cref="ApplyDefaultUiTransformValues"/> and <see cref="ApplyDefaultUiBackgroundValues"/>.
        /// Called when returning the UITransform to the pool so values do not leak to the next use.
        /// </summary>
        public static void ClearDefaultInteractiveStyles(VisualElement uiTransform)
        {
            uiTransform.style.overflow = new StyleEnum<Overflow>(StyleKeyword.Null);
            uiTransform.style.backgroundColor = new StyleColor(StyleKeyword.Null);
            uiTransform.style.borderBottomLeftRadius = new StyleLength(StyleKeyword.Undefined);
            uiTransform.style.borderBottomRightRadius = new StyleLength(StyleKeyword.Undefined);
            uiTransform.style.borderTopLeftRadius = new StyleLength(StyleKeyword.Undefined);
            uiTransform.style.borderTopRightRadius = new StyleLength(StyleKeyword.Undefined);
            uiTransform.style.borderTopWidth = new StyleFloat(StyleKeyword.Undefined);
            uiTransform.style.borderRightWidth = new StyleFloat(StyleKeyword.Undefined);
            uiTransform.style.borderBottomWidth = new StyleFloat(StyleKeyword.Undefined);
            uiTransform.style.borderLeftWidth = new StyleFloat(StyleKeyword.Undefined);
            uiTransform.style.borderTopColor = new StyleColor(StyleKeyword.Undefined);
            uiTransform.style.borderRightColor = new StyleColor(StyleKeyword.Undefined);
            uiTransform.style.borderBottomColor = new StyleColor(StyleKeyword.Undefined);
            uiTransform.style.borderLeftColor = new StyleColor(StyleKeyword.Undefined);
        }
    }
}
