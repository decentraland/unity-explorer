using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace DCL.UI.CustomInputField
{
    /// <summary>
    /// This custom class overrides some methods from the TMP_InputField, allowing us to capture certain input events to run custom code
    /// also helps by making checks when text is inserted and replaced, to make sure the input field limits aren't exceeded.
    /// </summary>
    public class CustomInputField : TMP_InputField
    {
        private bool isControlPressed;
        private readonly StringBuilder stringBuilder = new ();

        public event Action<PointerEventData.InputButton>? Clicked;
        public event Action? PasteShortcutPerformed;

        public bool UpAndDownArrowsEnabled { get; set; }

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                OnSelect(null);
                int insertionIndex = TMP_TextUtilities.GetCursorIndexFromPosition(m_TextComponent, eventData.position, eventData.pressEventCamera);
                caretPosition = insertionIndex;
            }
            else
                base.OnPointerClick(eventData);

            Clicked?.Invoke(eventData.button);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            isControlPressed = false;
            base.OnDeselect(eventData);
        }

        private readonly Event processingEvent = new ();

        public override void OnUpdateSelected(BaseEventData eventData)
        {
            if (!isFocused)
                return;

            if (TryHandleSpecialKeys())
            {
                UpdateLabel();
                eventData.Use();
                return;
            }

            base.OnUpdateSelected(eventData);
        }

        private bool TryHandleSpecialKeys()
        {
            //This whole logic is so we can capture Ctrl+V and up and down events before they are sent to the input field
            //Otherwise the input field inserts each character pasted one by one, and sending on Input changed events
            //For each character which is not desirable, slows down the game quite a bit and also overflows our sounds manager
            //trying to play 200 sounds simultaneously. I dont like accessing keyboard directly, so this is prone to be
            //refactored when we move this functionality to other input fields.
            if (Keyboard.current.leftCommandKey.wasPressedThisFrame || Keyboard.current.leftCtrlKey.wasPressedThisFrame)
                isControlPressed = true;

            if (Keyboard.current.tabKey.wasPressedThisFrame)
                return true;

            if (isControlPressed && Keyboard.current.vKey.wasPressedThisFrame)
            {
                PasteShortcutPerformed?.Invoke();
                return true;
            }

            if (!UpAndDownArrowsEnabled &&
                (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame))
                return true;

            if (Keyboard.current.leftCommandKey.wasReleasedThisFrame || Keyboard.current.leftCtrlKey.wasReleasedThisFrame)
                isControlPressed = false;

            return false;
        }

        public bool IsWithinCharacterLimit(int newTextLenght = 0) =>
            text.Length + newTextLenght < characterLimit;

        public void InsertTextAtCaretPosition(string newText)
        {
            InsertTextAtPosition(newText, stringPosition);
            OnSelect(null);
        }

        /// <summary>
        ///     Selects the input field, gives it focus, replaces its content if the text is not null and moves the caret to its correct position.
        /// </summary>
        /// <param name="newText">The new content of the input field, can be null </param>
        public void SelectInputField(string? newText = null)
        {
            OnSelect(null);

            if (newText == null) return;

            text = newText;
            caretPosition = newText.Length;
        }

        public void ResetInputField()
        {
            text = string.Empty;
            ActivateInputField();
            isControlPressed = false;
        }

        /// <summary>
        ///     This Method inserts a text starting in a specific position.
        ///     Only the text that fits inside the input field character limits will be inserted, anything else will be lost.
        /// </summary>
        private void InsertTextAtPosition(string pastedText, int position)
        {
            int remainingSpace = characterLimit - text.Length;

            if (remainingSpace <= 0) return;

            string textToInsert = pastedText.Length > remainingSpace ? pastedText[..remainingSpace] : pastedText;

            text = text.Insert(position, textToInsert);
            stringPosition = position + textToInsert.Length;
        }

        /// <summary>
        ///     This method replaces a replaceAmount of characters starting at replaceAt position with a newValue and adds an empty character after the inserted text
        /// </summary>
        public void ReplaceTextAtPosition(int replaceAt, int replaceAmount, string newValue, bool notify = false)
        {
            int textLenghtDifference = newValue.Length - replaceAmount;

            if (!IsWithinCharacterLimit(textLenghtDifference)) return;

            stringBuilder.Clear();

            stringBuilder.Append(text.AsSpan(0, replaceAt))
                         .Append(newValue)
                         .Append(" ")
                         .Append(text.AsSpan(replaceAt + replaceAmount));

            if (notify) text = stringBuilder.ToString();
            else
                SetTextWithoutNotify(stringBuilder.ToString());

            stringPosition += replaceAt + newValue.Length + 1;
        }
    }
}
