using DCL.Input;
using DCL.SDKComponents.SceneUI.Classes;
using DCL.SDKComponents.SceneUI.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Components
{
    public class UIInputComponent
    {
        public readonly TextField TextField = new ();
        public readonly TextFieldPlaceholder Placeholder = new ();
        public TextElement TextElement { get; private set; }
        public bool IsOnValueChangedTriggered;
        public bool IsOnSubmitTriggered;

        internal EventCallback<ChangeEvent<string>> currentOnValueChanged = static _ => { };
        internal EventCallback<KeyDownEvent> currentOnSubmit = static _ => { };
        internal EventCallback<FocusInEvent> currentOnFocusIn = static _ => { };
        internal EventCallback<FocusOutEvent> currentOnFocusOut = static _ => { };

        public void Initialize(
            IInputBlock inputBlock,
            string textFieldName,
            string text,
            string placeholderValue,
            Color placeholderColorValue)
        {
            TextField.name = textFieldName;
            TextField.AddToClassList("dcl-input");
            TextField.pickingMode = PickingMode.Position;
            TextField.SetValueWithoutNotify(text);

            TextElement = TextField.Q<TextElement>();

            Placeholder.Initialize(TextField, placeholderValue, placeholderColorValue);

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
