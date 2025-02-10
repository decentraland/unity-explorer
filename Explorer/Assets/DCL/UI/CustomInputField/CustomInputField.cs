using System;
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
                    if (Event.current.keyCode == KeyCode.LeftCommand || Event.current.keyCode == KeyCode.LeftControl)
                    {
                        isControlPressed = true;
                    }
                    else if ( isControlPressed && Event.current.keyCode == KeyCode.V)
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
                {
                    if (Event.current.keyCode == KeyCode.LeftCommand || Event.current.keyCode == KeyCode.LeftControl) { isControlPressed = false; }
                }
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

        public void ReplaceText(string oldValue, string newValue)
        {
            int textLenghtDifference = newValue.Length - oldValue.Length;

            if (!IsWithinCharacterLimit(textLenghtDifference)) return;

            text = text.Replace(oldValue, newValue);
            stringPosition += textLenghtDifference;
        }
    }
}
