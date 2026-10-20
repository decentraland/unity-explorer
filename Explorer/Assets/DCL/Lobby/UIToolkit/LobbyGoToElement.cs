using DCL.Chat.Commands;
using DCL.CommunicationData.URLHelpers;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     The pill of the go-to widget, styled by LobbyGoTo.uss: a location icon, the /goto command and the field the
    ///     destination is typed in, and a go button shown only while the field holds a destination: a parcel or a world
    ///     named with its domain, in any form the /goto chat command takes. A press anywhere on the pill focuses the field;
    ///     Return or the go button submits the destination, trimmed, and empties the field, while anything else submits
    ///     nothing.
    /// </summary>
    [UxmlElement]
    public partial class LobbyGoToElement : VisualElement
    {
        private const string USS_BLOCK = "lobby-goto";
        private const string USS_FOCUSED = USS_BLOCK + "--focused";
        private const string USS_ICON = USS_BLOCK + "__icon";
        private const string USS_COMMAND = USS_BLOCK + "__command";
        private const string USS_INPUT = USS_BLOCK + "__input";
        private const string USS_GO = USS_BLOCK + "__go";
        private const string USS_GO_ICON = USS_BLOCK + "__go-icon";
        private const string USS_WITH_DESTINATION = USS_BLOCK + "--with-destination";

        private const string COMMAND = "/goto";
        private const string PLACEHOLDER = "e.g. 3,-5 or mana.dcl.eth";

        /// <summary>The destination as typed, trimmed; always a parcel or a world named with its domain.</summary>
        public Action<string>? Submitted;

        private readonly TextField input;

        /// <summary>What the field holds, untrimmed.</summary>
        public string Text
        {
            get => input.value;

            set
            {
                input.value = value;
                UpdateDestination();
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

            if (!IsDestination(destination))
                return;

            input.value = string.Empty;
            Submitted?.Invoke(destination);
        }

        private void OnValueChanged(ChangeEvent<string> _) =>
            UpdateDestination();

        private void UpdateDestination() =>
            EnableInClassList(USS_WITH_DESTINATION, IsDestination(input.value.Trim()));

        // What the /goto chat command resolves without guessing: a parcel, or a world named with its ENS domain, each
        // with the command's optional parcel and spawn point
        private static bool IsDestination(string text)
        {
            GotoTarget target = ChatParamUtils.ParseGotoTarget(text);
            return target.World?.IsEns() ?? target.Parcel.HasValue;
        }

        private void OnFocusIn(FocusInEvent _) =>
            AddToClassList(USS_FOCUSED);

        private void OnFocusOut(FocusOutEvent _) =>
            RemoveFromClassList(USS_FOCUSED);
    }
}
