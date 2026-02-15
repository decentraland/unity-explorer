using Arch.Core;
using DCL.ECSComponents;
using DCL.Input;
using DCL.Input.Component;
using DCL.SDKComponents.SceneUI.Classes;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Defaults;
using Google.Protobuf.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Utils
{
    public static class Extensions
    {
        // UiDropdown, UiInput and UiButton detect the hover event, but their UiTransform has the styles effect.
        // A struct and static callbacks are used to avoid allocations.
        public readonly struct HoverStyleBehaviourData
        {
            public readonly VisualElement HoverEventTarget;
            public readonly VisualElement UiTransform;
            public readonly World World;
            public readonly Entity Entity;
            public readonly float BorderDarkenFactor;
            public readonly float BackgroundDarkenFactor;

            // Store original border colors from VisualElement to lerp back to on hover leave
            public readonly Color OriginalBorderTopColor;
            public readonly Color OriginalBorderRightColor;
            public readonly Color OriginalBorderBottomColor;
            public readonly Color OriginalBorderLeftColor;

            public HoverStyleBehaviourData(
                VisualElement hoverEventTarget,
                VisualElement uiTransform,
                World world,
                Entity entity,
                float borderDarkenFactor,
                float backgroundDarkenFactor,
                Color originalBorderTopColor,
                Color originalBorderRightColor,
                Color originalBorderBottomColor,
                Color originalBorderLeftColor)
            {
                HoverEventTarget = hoverEventTarget;
                UiTransform = uiTransform;
                World = world;
                Entity = entity;
                BorderDarkenFactor = borderDarkenFactor;
                BackgroundDarkenFactor = backgroundDarkenFactor;
                OriginalBorderTopColor = originalBorderTopColor;
                OriginalBorderRightColor = originalBorderRightColor;
                OriginalBorderBottomColor = originalBorderBottomColor;
                OriginalBorderLeftColor = originalBorderLeftColor;
            }
        }

        internal static readonly EventCallback<PointerEnterEvent, HoverStyleBehaviourData> HOVER_ENTER_CALLBACK = OnHoverEnter;
        internal static readonly EventCallback<PointerLeaveEvent, HoverStyleBehaviourData> HOVER_LEAVE_CALLBACK = OnHoverLeave;

        public static void RegisterHoverStyleCallbacks(this VisualElement hoverEventTarget, HoverStyleBehaviourData data)
        {
            hoverEventTarget.UnregisterHoverStyleCallbacks();
            hoverEventTarget.RegisterCallback(HOVER_ENTER_CALLBACK, data);
            hoverEventTarget.RegisterCallback(HOVER_LEAVE_CALLBACK, data);
        }

        public static void UnregisterHoverStyleCallbacks(this VisualElement hoverEventTarget)
        {
            hoverEventTarget.UnregisterCallback(HOVER_ENTER_CALLBACK);
            hoverEventTarget.UnregisterCallback(HOVER_LEAVE_CALLBACK);
        }

        private static void OnHoverEnter(PointerEnterEvent evt, HoverStyleBehaviourData behaviourData)
        {
            if (behaviourData.HoverEventTarget.hasDisabledPseudoState)
                return;

            if (behaviourData.BorderDarkenFactor > 0)
            {
                behaviourData.UiTransform.style.borderTopColor = Color.Lerp(behaviourData.OriginalBorderTopColor, Color.black, behaviourData.BorderDarkenFactor);
                behaviourData.UiTransform.style.borderRightColor = Color.Lerp(behaviourData.OriginalBorderRightColor, Color.black, behaviourData.BorderDarkenFactor);
                behaviourData.UiTransform.style.borderBottomColor = Color.Lerp(behaviourData.OriginalBorderBottomColor, Color.black, behaviourData.BorderDarkenFactor);
                behaviourData.UiTransform.style.borderLeftColor = Color.Lerp(behaviourData.OriginalBorderLeftColor, Color.black, behaviourData.BorderDarkenFactor);
            }

            if (behaviourData.BackgroundDarkenFactor > 0)
            {
                // Get the background color from PBUiBackground if it exists, otherwise use default one
                Color backgroundColor = behaviourData.World.TryGet(behaviourData.Entity, out PBUiBackground? pbUiBackground)
                    ? pbUiBackground!.GetColor()
                    : Color.white;

                behaviourData.UiTransform.style.backgroundColor = Color.Lerp(backgroundColor, Color.black, behaviourData.BackgroundDarkenFactor);
            }
        }

        private static void OnHoverLeave(PointerLeaveEvent evt, HoverStyleBehaviourData behaviourData)
        {
            if (evt.target != behaviourData.HoverEventTarget)
                return; // detected on child

            if (behaviourData.BorderDarkenFactor > 0)
            {
                behaviourData.UiTransform.style.borderTopColor = behaviourData.OriginalBorderTopColor;
                behaviourData.UiTransform.style.borderRightColor = behaviourData.OriginalBorderRightColor;
                behaviourData.UiTransform.style.borderBottomColor = behaviourData.OriginalBorderBottomColor;
                behaviourData.UiTransform.style.borderLeftColor = behaviourData.OriginalBorderLeftColor;
            }

            if (behaviourData.BackgroundDarkenFactor > 0)
            {
                // Restore to the current PBUiBackground color if it exists, otherwise use default one
                Color originalBackgroundColor = behaviourData.World.TryGet(behaviourData.Entity, out PBUiBackground? pbUiBackground)
                    ? pbUiBackground!.GetColor()
                    : Color.white;

                behaviourData.UiTransform.style.backgroundColor = originalBackgroundColor;
            }
        }

        public static TextAnchor ToUnityTextAlign(this TextAlignMode align)
        {
            switch (align)
            {
                case TextAlignMode.TamTopCenter:
                    return TextAnchor.UpperCenter;
                case TextAlignMode.TamTopLeft:
                    return TextAnchor.UpperLeft;
                case TextAlignMode.TamTopRight:
                    return TextAnchor.UpperRight;

                case TextAlignMode.TamBottomCenter:
                    return TextAnchor.LowerCenter;
                case TextAlignMode.TamBottomLeft:
                    return TextAnchor.LowerLeft;
                case TextAlignMode.TamBottomRight:
                    return TextAnchor.LowerRight;

                case TextAlignMode.TamMiddleCenter:
                    return TextAnchor.MiddleCenter;
                case TextAlignMode.TamMiddleLeft:
                    return TextAnchor.MiddleLeft;
                case TextAlignMode.TamMiddleRight:
                    return TextAnchor.MiddleRight;

                default:
                    return TextAnchor.MiddleCenter;
            }
        }

        public static DCLUVs ToDCLUVs(this RepeatedField<float>? uvs) =>
            uvs is not { Count: 8 }
                ? DCLUVs.Default
                : new DCLUVs(
                    new Vector2(uvs[0], uvs[1]),
                    new Vector2(uvs[2], uvs[3]),
                    new Vector2(uvs[4], uvs[5]),
                    new Vector2(uvs[6], uvs[7]));

        public static DCLImageScaleMode ToDCLImageScaleMode(this BackgroundTextureMode textureMode)
        {
            return textureMode switch
                   {
                       BackgroundTextureMode.Center => DCLImageScaleMode.Center,
                       BackgroundTextureMode.Stretch => DCLImageScaleMode.Stretch,
                       BackgroundTextureMode.NineSlices => DCLImageScaleMode.NineSlices,
                       _ => DCLImageScaleMode.Stretch
                   };
        }

        public static void RegisterPointerCallbacks(this UITransformComponent uiTransformComponent)
        {
            EventCallback<PointerDownEvent> newOnPointerDownCallback = _ => uiTransformComponent.PointerEventTriggered = PointerEventType.PetDown;
            EventCallback<PointerUpEvent> newOnPointerUpCallback = _ => uiTransformComponent.PointerEventTriggered = PointerEventType.PetUp;
            EventCallback<PointerEnterEvent> newOnPointerEnterCallback = _ => uiTransformComponent.PointerEventTriggered = PointerEventType.PetHoverEnter;
            EventCallback<PointerLeaveEvent> newOnPointerLeaveCallback = _ => uiTransformComponent.PointerEventTriggered = PointerEventType.PetHoverLeave;

            uiTransformComponent.UnregisterPointerCallbacks();
            uiTransformComponent.Transform.RegisterCallback(newOnPointerDownCallback);
            uiTransformComponent.currentOnPointerDownCallback = newOnPointerDownCallback;
            uiTransformComponent.Transform.RegisterCallback(newOnPointerUpCallback);
            uiTransformComponent.currentOnPointerUpCallback = newOnPointerUpCallback;
            uiTransformComponent.Transform.RegisterCallback(newOnPointerEnterCallback);
            uiTransformComponent.currentOnPointerEnterCallback = newOnPointerEnterCallback;
            uiTransformComponent.Transform.RegisterCallback(newOnPointerLeaveCallback);
            uiTransformComponent.currentOnPointerLeaveCallback = newOnPointerLeaveCallback;
        }

        public static void UnregisterPointerCallbacks(this UITransformComponent uiTransformComponent)
        {
            if(uiTransformComponent.currentOnPointerDownCallback!=null)
                uiTransformComponent.Transform.UnregisterCallback(uiTransformComponent.currentOnPointerDownCallback);

            if(uiTransformComponent.currentOnPointerUpCallback!=null)
                uiTransformComponent.Transform.UnregisterCallback(uiTransformComponent.currentOnPointerUpCallback);

            if(uiTransformComponent.currentOnPointerEnterCallback!=null)
                uiTransformComponent.Transform.UnregisterCallback(uiTransformComponent.currentOnPointerEnterCallback);

            if(uiTransformComponent.currentOnPointerLeaveCallback!=null)
                uiTransformComponent.Transform.UnregisterCallback(uiTransformComponent.currentOnPointerLeaveCallback);
        }

        public static void RegisterInputCallbacks(this UIInputComponent uiInputComponent, IInputBlock inputBlock)
        {
            EventCallback<ChangeEvent<string>> newOnChangeCallback = evt =>
            {
                evt.StopPropagation();
                uiInputComponent.IsOnValueChangedTriggered = true;
            };

            EventCallback<KeyDownEvent> newOnSubmitCallback = evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
                    return;

                evt.StopPropagation();
                uiInputComponent.IsOnSubmitTriggered = true;
            };

            EventCallback<FocusInEvent> newOnFocusInCallback = evt =>
            {
                evt.StopPropagation();
                inputBlock.Disable(InputMapComponent.Kind.CAMERA , InputMapComponent.Kind.SHORTCUTS , InputMapComponent.Kind.PLAYER, InputMapComponent.Kind.IN_WORLD_CAMERA);
            };

            EventCallback<FocusOutEvent> newOnFocusOutCallback = evt =>
            {
                evt.StopPropagation();
                inputBlock.Enable(InputMapComponent.Kind.CAMERA , InputMapComponent.Kind.SHORTCUTS , InputMapComponent.Kind.PLAYER, InputMapComponent.Kind.IN_WORLD_CAMERA);
            };

            uiInputComponent.UnregisterInputCallbacks();
            uiInputComponent.TextField.RegisterCallback(newOnChangeCallback);
            uiInputComponent.currentOnValueChanged = newOnChangeCallback;
            uiInputComponent.TextField.RegisterCallback(newOnSubmitCallback);
            uiInputComponent.currentOnSubmit = newOnSubmitCallback;
            uiInputComponent.TextField.RegisterCallback(newOnFocusInCallback);
            uiInputComponent.currentOnFocusIn = newOnFocusInCallback;
            uiInputComponent.TextField.RegisterCallback(newOnFocusOutCallback);
            uiInputComponent.currentOnFocusOut = newOnFocusOutCallback;
        }

        public static void UnregisterInputCallbacks(this UIInputComponent uiInputComponent)
        {
            uiInputComponent.TextField.UnregisterCallback(uiInputComponent.currentOnValueChanged);
            uiInputComponent.TextField.UnregisterCallback(uiInputComponent.currentOnSubmit);
            uiInputComponent.TextField.UnregisterCallback(uiInputComponent.currentOnFocusIn);
            uiInputComponent.TextField.UnregisterCallback(uiInputComponent.currentOnFocusOut);
        }

        // Using static callbacks to avoid closure allocations
        private static readonly EventCallback<ChangeEvent<string>, UIDropdownComponent> DROPDOWN_VALUE_CHANGED_CALLBACK = OnDropdownValueChanged;
        private static readonly EventCallback<PointerDownEvent, UIDropdownComponent> DROPDOWN_POINTER_DOWN_CALLBACK = OnDropdownPointerDown;
        internal static readonly System.Action<VisualElement, float> OPACITY_ANIMATION_CALLBACK = static (element, value) => element.style.opacity = value;

        public static void RegisterDropdownCallbacks(this UIDropdownComponent uiDropdownComponent)
        {
            uiDropdownComponent.UnregisterDropdownCallbacks();
            uiDropdownComponent.DropdownField.RegisterCallback(DROPDOWN_VALUE_CHANGED_CALLBACK, uiDropdownComponent);

            // Enforce an opacity transition since Unity instantiates the popup on demand,
            // and we cannot animate a property on it from the uss stylesheet.
            // The animation must be scheduled (deferred) because Unity creates the popup element
            // on demand, so it doesn't exist in the visual tree at PointerDown time.
            uiDropdownComponent.cachedScheduledAction ??= uiDropdownComponent.AnimateDropdownOpacity;
            uiDropdownComponent.DropdownField.RegisterCallback(DROPDOWN_POINTER_DOWN_CALLBACK, uiDropdownComponent);
        }

        public static void UnregisterDropdownCallbacks(this UIDropdownComponent uiDropdownComponent)
        {
            uiDropdownComponent.DropdownField.UnregisterCallback(DROPDOWN_VALUE_CHANGED_CALLBACK);
            uiDropdownComponent.DropdownField.UnregisterCallback(DROPDOWN_POINTER_DOWN_CALLBACK);
        }

        private static void OnDropdownValueChanged(ChangeEvent<string> evt, UIDropdownComponent component)
        {
            evt.StopPropagation();
            component.IsOnValueChangedTriggered = true;
        }

        private static void OnDropdownPointerDown(PointerDownEvent _, UIDropdownComponent component)
        {
            component.DropdownField.schedule.Execute(component.cachedScheduledAction);
        }
    }
}
