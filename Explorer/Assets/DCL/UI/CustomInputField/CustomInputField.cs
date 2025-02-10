using System;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.UI.CustomInputField
{
    /// <summary>
    /// This custom class serves as an in-between other classes and the TMP_InputField, capturing some events
    /// and making checks when text is inserted and replaced, to make sure the input field limits aren't exceeded.
    /// </summary>
    public class CustomInputField : TMP_InputField
    {
        private bool isControlPressed;
        private readonly StringBuilder stringBuilder = new ();

        public event Action? OnRightClickEvent;
        public event Action? OnPasteShortcutPerformedEvent;

        public bool UpAndDownArrowsEnabled { get; set; }

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                OnSelect(null);
                int insertionIndex = TMP_TextUtilities.GetCursorIndexFromPosition(m_TextComponent, eventData.position, eventData.pressEventCamera);
                caretPosition = insertionIndex;
                OnRightClickEvent?.Invoke();
            }
            else
                base.OnPointerClick(eventData);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            isControlPressed = false;
            base.OnDeselect(eventData);
        }

        public override void OnUpdateSelected(BaseEventData eventData)
        {
            if (!isFocused)
                return;

            var shouldCallBase = true;

            if (Event.current != null)
            {
                EventType eventType = Event.current.type;

                if (eventType == EventType.KeyDown)
                {
                    //This whole logic is so we can capture Ctrl+V events before they are sent to the input field
                    //Otherwise the input field inserts each character pasted one by one, and sending on Input changed events
                    //For each character which is not desirable, slows down the game quite a bit and also overflows our sounds manager
                    //trying to play 200 sounds simultaneously.
                    if (Event.current.keyCode == KeyCode.LeftCommand || Event.current.keyCode == KeyCode.LeftControl)
                        isControlPressed = true;
                    else if (isControlPressed && Event.current.keyCode == KeyCode.V)
                    {
                        OnPasteShortcutPerformedEvent?.Invoke();
                        Event.current.Use();
                        shouldCallBase = false;
                    }
                    else if (!UpAndDownArrowsEnabled &&
                             Event.current.keyCode is KeyCode.UpArrow or KeyCode.DownArrow)
                    {
                        Event.current.Use();
                        shouldCallBase = false;
                    }
                }
                else if (eventType == EventType.KeyUp)
                    if (Event.current.keyCode == KeyCode.LeftCommand || Event.current.keyCode == KeyCode.LeftControl)
                        isControlPressed = false;
            }

            if (shouldCallBase)
                base.OnUpdateSelected(eventData);
        }

        public bool IsWithinCharacterLimit(int newTextLenght = 0) =>
            text.Length + newTextLenght < characterLimit;

        public void InsertTextAtSelectedPosition(string newText)
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
        ///     This method replaces a replaceAmount of characters starting at replaceAt position with a newValue
        /// </summary>
        public void ReplaceTextAtPosition(int replaceAt, int replaceAmount, string newValue, bool notify = false)
        {
            int textLenghtDifference = newValue.Length - replaceAmount;

            if (!IsWithinCharacterLimit(textLenghtDifference)) return;

            stringBuilder.Clear();

            stringBuilder.Append(text.AsSpan(0, replaceAt))
                         .Append(newValue)
                         .Append(text.AsSpan(replaceAt + replaceAmount));

            if (notify) text = stringBuilder.ToString();
            else
                SetTextWithoutNotify(stringBuilder.ToString());

            stringPosition += replaceAt + replaceAmount;
        }
    }
}
