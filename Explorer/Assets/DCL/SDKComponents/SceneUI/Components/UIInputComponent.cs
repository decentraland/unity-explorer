using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Classes;
using DCL.SDKComponents.SceneUI.Utils;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Components
{
    public class UIInputComponent: IPoolableComponentProvider<UIInputComponent>
    {
        public readonly TextField TextField = new ();
        public readonly TextFieldPlaceholder Placeholder = new ();
        public bool IsOnValueChangedTriggered;
        public bool IsOnSubmitTriggered;

        UIInputComponent IPoolableComponentProvider<UIInputComponent>.PoolableComponent => this;
        Type IPoolableComponentProvider<UIInputComponent>.PoolableComponentType => typeof(UIInputComponent);

        internal EventCallback<ChangeEvent<string>> currentOnValueChanged;
        internal EventCallback<KeyDownEvent> currentOnSubmit;

        public void Initialize(string textFieldName, string styleClass)
        {
            TextField.name = textFieldName;
            TextField.AddToClassList(styleClass);
            TextField.pickingMode = PickingMode.Position;
            Placeholder.SetupTextField(TextField);

            IsOnValueChangedTriggered = false;
            IsOnSubmitTriggered = false;
            this.RegisterInputCallbacks(
                evt =>
                {
                    evt.StopPropagation();
                    IsOnValueChangedTriggered = true;
                },
                evt =>
                {
                    if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
                        return;

                    evt.StopPropagation();
                    IsOnSubmitTriggered = true;
                });
        }

        public void Dispose()
        {
            this.UnregisterInputCallbacks();
            Placeholder.Dispose();
        }
    }
}
