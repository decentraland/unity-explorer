using DCL.ECSComponents;
using DCL.Input;
using DCL.Input.Component;
using DCL.SDKComponents.SceneUI.Classes;
using DCL.SDKComponents.SceneUI.Components;
using Google.Protobuf.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Utils
{
    public static class Extensions
    {
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

            uiTransformComponent.UnregisterPointerCallbacks();
            uiTransformComponent.Transform.RegisterCallback(newOnPointerDownCallback);
            uiTransformComponent.currentOnPointerDownCallback = newOnPointerDownCallback;
            uiTransformComponent.Transform.RegisterCallback(newOnPointerUpCallback);
            uiTransformComponent.currentOnPointerUpCallback = newOnPointerUpCallback;
        }

        public static void UnregisterPointerCallbacks(this UITransformComponent uiTransformComponent)
        {
            uiTransformComponent.Transform.UnregisterCallback(uiTransformComponent.currentOnPointerDownCallback);
            uiTransformComponent.Transform.UnregisterCallback(uiTransformComponent.currentOnPointerUpCallback);
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
                inputBlock.Disable(InputMapComponent.Kind.CAMERA , InputMapComponent.Kind.SHORTCUTS , InputMapComponent.Kind.PLAYER);
            };

            EventCallback<FocusOutEvent> newOnFocusOutCallback = evt =>
            {
                evt.StopPropagation();
                inputBlock.Enable(InputMapComponent.Kind.CAMERA , InputMapComponent.Kind.SHORTCUTS , InputMapComponent.Kind.PLAYER);
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

        public static void RegisterDropdownCallbacks(this UIDropdownComponent uiDropdownComponent)
        {
            EventCallback<ChangeEvent<string>> newOnChangeCallback = evt =>
            {
                evt.StopPropagation();
                uiDropdownComponent.IsOnValueChangedTriggered = true;
            };

            uiDropdownComponent.UnregisterDropdownCallbacks();
            uiDropdownComponent.DropdownField.RegisterCallback(newOnChangeCallback);
            uiDropdownComponent.currentOnValueChanged = newOnChangeCallback;
        }

        public static void UnregisterDropdownCallbacks(this UIDropdownComponent uiInputComponent)
        {
            uiInputComponent.DropdownField.UnregisterCallback(uiInputComponent.currentOnValueChanged);
        }
    }
}
