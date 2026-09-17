using DCL.Input;
using DCL.Input.Component;
using DCL.SDKComponents.SceneUI.Classes;
using DCL.SDKComponents.SceneUI.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Components
{
    public class UIInputComponent
    {
        /// <summary>
        ///     Input maps blocked while the text field has keyboard focus.
        /// </summary>
        internal static readonly InputMapComponent.Kind[] BLOCKED_INPUT_KINDS =
        {
            InputMapComponent.Kind.Camera,
            InputMapComponent.Kind.Shortcuts,
            InputMapComponent.Kind.Player,
            InputMapComponent.Kind.InWorldCamera,
            InputMapComponent.Kind.Submit,
        };

        public readonly TextField TextField = new ();
        public readonly TextFieldPlaceholder Placeholder = new ();
        public TextElement TextElement { get; private set; }

        /// <summary>
        ///     True between the FocusIn that blocked the input maps and the FocusOut that released them.
        /// </summary>
        public bool IsFocused { get; internal set; }

        public bool IsOnValueChangedTriggered;
        public bool IsOnSubmitTriggered;

        internal EventCallback<ChangeEvent<string>> currentOnValueChanged = static _ => { };
        internal EventCallback<KeyDownEvent> currentOnSubmit = static _ => { };
        internal EventCallback<FocusInEvent> currentOnFocusIn = static _ => { };
        internal EventCallback<FocusOutEvent> currentOnFocusOut = static _ => { };

        private IInputBlock? focusInputBlock;

        public UIInputComponent()
        {
            TextElement = TextField.Q<TextElement>();
        }

        public void Initialize(
            IInputBlock inputBlock,
            string textFieldName,
            string text,
            string placeholderValue,
            Color placeholderColorValue)
        {
            focusInputBlock = inputBlock;

            TextField.name = textFieldName;
            TextField.AddToClassList("dcl-input");
            TextField.pickingMode = PickingMode.Position;
            TextField.SetValueWithoutNotify(text);

            Placeholder.Initialize(TextField, placeholderValue, placeholderColorValue);

            IsOnValueChangedTriggered = false;
            IsOnSubmitTriggered = false;
            IsFocused = false;
            this.RegisterInputCallbacks(inputBlock);
        }

        public void Dispose()
        {
            ReleaseInputFocus();
            this.UnregisterInputCallbacks();
            TextField.UnregisterHoverStyleCallbacks();
            Placeholder.Dispose();
        }

        /// <summary>
        ///     Blurs the field if it owns the panel focus. A field detached while focused is disposed without a blur
        ///     and UI Toolkit restores that focus when the element is attached again, so call this right after
        ///     attaching a recycled field: otherwise it starts focused without the FocusIn that blocks the input maps.
        /// </summary>
        public void BlurIfFocused()
        {
            Focusable? focused = TextField.focusController?.focusedElement;

            if (focused is VisualElement focusedElement && (focusedElement == TextField || TextField.Contains(focusedElement)))
                focusedElement.Blur();
        }

        /// <summary>
        ///     UI Toolkit never blurs an element that leaves the panel, so a field disposed while focused
        ///     would keep the input maps blocked forever. Must run before the callbacks are unregistered.
        /// </summary>
        private void ReleaseInputFocus()
        {
            // Blurring while still attached dispatches FocusOut synchronously: the registered callback lifts the block
            // and the panel drops the field from its focused list, so a recycled field does not come back focused.
            BlurIfFocused();

            // Nothing is dispatched when the field was detached before disposal, so lift the block directly.
            if (!IsFocused) return;

            IsFocused = false;
            focusInputBlock?.Enable(BLOCKED_INPUT_KINDS);
        }
    }
}
