using DCL.Chat.Commands;
using DCL.UI.InputFieldFormatting;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     The pill of the go-to widget, styled by LobbyGoTo.uss. Return or the go button submits the trimmed text when
    ///     it is a destination the /goto chat command takes, and otherwise marks the pill invalid until the text changes.
    /// </summary>
    [UxmlElement]
    public partial class LobbyGoToElement : VisualElement
    {
        private const string USS_BLOCK = "lobby-goto";
        private const string USS_FOCUSED = USS_BLOCK + "--focused";
        private const string USS_WITH_TEXT = USS_BLOCK + "--with-text";
        private const string USS_INVALID = USS_BLOCK + "--invalid";
        private const string USS_ICON = USS_BLOCK + "__icon";
        private const string USS_COMMAND = USS_BLOCK + "__command";
        private const string USS_INPUT = USS_BLOCK + "__input";
        private const string USS_GO = USS_BLOCK + "__go";
        private const string USS_GO_ICON = USS_BLOCK + "__go-icon";
        private const string USS_ERROR = USS_BLOCK + "__error";
        private const string USS_ERROR_ICON = USS_BLOCK + "__error-icon";
        private const string USS_ERROR_LABEL = USS_BLOCK + "__error-label";

        private const string COMMAND = "/goto";
        private const string PLACEHOLDER = "e.g. 3,-5 or mana.dcl.eth";
        private const string ERROR = "Coordinates or World name doesn't exist";

        /// <summary>The destination as typed, trimmed, in a form the /goto chat command resolves.</summary>
        public Action<string>? Submitted;

        private readonly TextField input;

        /// <summary>What the field holds, untrimmed.</summary>
        public string Text
        {
            get => input.value;

            set
            {
                input.value = value;
                OnTextChanged();
            }
        }

        public LobbyGoToElement()
        {
            AddToClassList(USS_BLOCK);

            var icon = new VisualElement { name = "Icon", pickingMode = PickingMode.Ignore };
            icon.AddToClassList(USS_ICON);
            Add(icon);

            var command = new Label(COMMAND) { name = "Command", pickingMode = PickingMode.Ignore };
            command.AddToClassList(USS_COMMAND);
            Add(command);

            input = new TextField { name = "Input" };
            input.AddToClassList(USS_INPUT);
            input.textEdition.placeholder = PLACEHOLDER;
            input.textEdition.hidePlaceholderOnFocus = false;
            Add(input);

            // Not focusable so Tab stays on the field
            var go = new Button(Submit) { name = "Go", focusable = false };
            go.AddToClassList(USS_GO);
            var goIcon = new VisualElement { name = "GoIcon", pickingMode = PickingMode.Ignore };
            goIcon.AddToClassList(USS_GO_ICON);
            go.Add(goIcon);
            Add(go);

            var error = new VisualElement { name = "Error", pickingMode = PickingMode.Ignore };
            error.AddToClassList(USS_ERROR);
            var errorIcon = new VisualElement { name = "ErrorIcon", pickingMode = PickingMode.Ignore };
            errorIcon.AddToClassList(USS_ERROR_ICON);
            error.Add(errorIcon);
            var errorLabel = new Label(ERROR) { name = "ErrorLabel", pickingMode = PickingMode.Ignore };
            errorLabel.AddToClassList(USS_ERROR_LABEL);
            error.Add(errorLabel);
            Add(error);

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            // Trickle-down so the submit runs before the inner text element takes Return
            input.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            input.RegisterValueChangedCallback(OnValueChanged);
            input.RegisterCallback<FocusInEvent>(OnFocusIn);
            input.RegisterCallback<FocusOutEvent>(OnFocusOut);
        }

        /// <summary>Empties the field and drops its focus, so the placeholder shows and no key lands in it.</summary>
        public void Reset()
        {
            input.Blur();
            Text = string.Empty;
        }

        // The field takes its own presses; the rest of the pill hands them over
        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.target == this)
                input.Focus();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
                return;

            evt.StopPropagation();
            Submit();
        }

        private void Submit()
        {
            string destination = input.value.Trim();

            if (destination.Length == 0)
                return;

            if (!IsDestination(destination))
            {
                AddToClassList(USS_INVALID);
                return;
            }

            // Through the setter: the change event queues behind this key or click, which a subscriber may outlive
            Text = string.Empty;
            Submitted?.Invoke(destination);
        }

        private void OnValueChanged(ChangeEvent<string> _) =>
            OnTextChanged();

        private void OnTextChanged()
        {
            RemoveFromClassList(USS_INVALID);
            EnableInClassList(USS_WITH_TEXT, input.value.Trim().Length > 0);
        }

        private static bool IsDestination(string text)
        {
            GotoTarget target = ChatParamUtils.ParseGotoTarget(text);

            if (target.IsRandom || target.IsCrowd)
                return true;

            if (target.World is { } world && !DestinationRules.IsWorld(world) && !DestinationRules.IsWorldShortName(world))
                return false;

            if (target.Parcel is { } parcel && !DestinationRules.IsParcel(parcel.x, parcel.y))
                return false;

            return target.World != null || target.Parcel.HasValue;
        }

        private void OnFocusIn(FocusInEvent _) =>
            AddToClassList(USS_FOCUSED);

        private void OnFocusOut(FocusOutEvent _) =>
            RemoveFromClassList(USS_FOCUSED);
    }
}
