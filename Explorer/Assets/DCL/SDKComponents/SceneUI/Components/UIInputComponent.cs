using DCL.Input.UnityInputSystem.Blocks;
using DCL.SDKComponents.SceneUI.Classes;
using DCL.SDKComponents.SceneUI.Utils;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Components
{
    public class UIInputComponent
    {
        public readonly TextField TextField = new ();
        public readonly TextFieldPlaceholder Placeholder = new ();
        public bool IsOnValueChangedTriggered;
        public bool IsOnSubmitTriggered;

        internal EventCallback<ChangeEvent<string>> currentOnValueChanged;
        internal EventCallback<KeyDownEvent> currentOnSubmit;

        public void Initialize(IInputBlock inputBlock, string textFieldName, string styleClass)
        {
            TextField.name = textFieldName;
            TextField.AddToClassList(styleClass);
            TextField.pickingMode = PickingMode.Position;
            Placeholder.SetupTextField(TextField);

            IsOnValueChangedTriggered = false;
            IsOnSubmitTriggered = false;
            this.RegisterInputCallbacks(inputBlock);
        }

        public void Dispose()
        {
            this.UnregisterInputCallbacks();
            Placeholder.Dispose();
        }
    }
}
