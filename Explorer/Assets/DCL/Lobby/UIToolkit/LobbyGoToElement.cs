using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     The pill of the go-to widget, styled by LobbyGoTo.uss: a location icon, the /goto command and the field the
    ///     destination is typed in. A press anywhere on the pill focuses the field; Return submits what it holds, trimmed,
    ///     and empties it, while an empty field submits nothing.
    /// </summary>
    [UxmlElement]
    public partial class LobbyGoToElement : VisualElement
    {
        private const string USS_BLOCK = "lobby-goto";
        private const string USS_FOCUSED = USS_BLOCK + "--focused";
        private const string USS_ICON = USS_BLOCK + "__icon";
        private const string USS_COMMAND = USS_BLOCK + "__command";
        private const string USS_INPUT = USS_BLOCK + "__input";

        private const string COMMAND = "/goto";
        private const string PLACEHOLDER = "e.g. 3,-5 or mana.dcl.eth";

        /// <summary>The destination as typed, trimmed and never empty.</summary>
        public Action<string>? Submitted;

        private readonly TextField input;

        /// <summary>What the field holds, untrimmed.</summary>
        public string Text
        {
            get => input.value;
            set => input.value = value;
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

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            // Trickle-down so the submit runs before the inner text element takes Return
            input.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            input.RegisterCallback<FocusInEvent>(OnFocusIn);
            input.RegisterCallback<FocusOutEvent>(OnFocusOut);
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
            string destination = input.value.Trim();

            if (destination.Length == 0)
                return;

            input.value = string.Empty;
            Submitted?.Invoke(destination);
        }

        private void OnFocusIn(FocusInEvent _) =>
            AddToClassList(USS_FOCUSED);

        private void OnFocusOut(FocusOutEvent _) =>
            RemoveFromClassList(USS_FOCUSED);
    }
}
