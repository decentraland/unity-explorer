using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Classes
{
    public class DCLInputText
    {
        public readonly TextField TextField = new ();
        public readonly TextFieldPlaceholder Placeholder = new ();

        private EventCallback<ChangeEvent<string>> currentOnValueChanged;
        private EventCallback<KeyDownEvent> currentOnSubmit;

        public bool HasAnyOnChangeCallback => currentOnValueChanged != null;
        public bool HasAnyOnSubmitCallback => currentOnSubmit != null;

        public void Initialize(string textFieldName, string styleClass)
        {
            TextField.name = textFieldName;
            TextField.AddToClassList(styleClass);
            TextField.pickingMode = PickingMode.Position;
            Placeholder.SetupTextField(TextField);
        }

        public void RegisterOnChangeCallback(EventCallback<ChangeEvent<string>> newOnChangeCallback)
        {
            if (HasAnyOnChangeCallback)
                TextField.UnregisterCallback(currentOnValueChanged);

            TextField.RegisterCallback(newOnChangeCallback);
            currentOnValueChanged = newOnChangeCallback;
        }

        public void RegisterOnKeyDownCallback(EventCallback<KeyDownEvent> newOnSubmitCallback)
        {
            if (HasAnyOnSubmitCallback)
                TextField.UnregisterCallback(currentOnSubmit);

            TextField.RegisterCallback(newOnSubmitCallback);
            currentOnSubmit = newOnSubmitCallback;
        }

        public void UnregisterAllCallbacks()
        {
            if (HasAnyOnChangeCallback)
                TextField.UnregisterCallback(currentOnValueChanged);

            if (HasAnyOnSubmitCallback)
                TextField.UnregisterCallback(currentOnSubmit);
        }

        public void Dispose()
        {
            UnregisterAllCallbacks();
            Placeholder.Dispose();
        }
    }
}
