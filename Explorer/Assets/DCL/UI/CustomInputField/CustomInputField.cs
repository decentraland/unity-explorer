using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

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

        //I don't like this, but given that I cannot override properly the TMP_InputField methods, this is kind of the only way.
        //Copying the existing logic and adding the specific change I need to handle special behaviours
        //Otherwise I have no way of avoiding the Input Field from getting certain events that we need to filter out.
#region Code Adapted from TMP_InputField

        private readonly Event processingEvent = new ();
        private int compositionLength => compositionString?.Length ?? 0;
        private string? compositionString => inputSystem?.compositionString;

        private BaseInput? inputSystem
        {
            get
            {
                if (EventSystem.current && EventSystem.current.currentInputModule)
                    return EventSystem.current.currentInputModule.input;

                return null;
            }
        }


        public override void OnUpdateSelected(BaseEventData eventData)
        {
            if (!isFocused)
                return;

            var consumedEvent = false;

            while (Event.PopEvent(processingEvent))
            {
                if (TryHandleSpecialKeys())
                {
                    consumedEvent = true;
                    continue;
                }

                EventType eventType = processingEvent.rawType;

                if (eventType == EventType.KeyUp)
                    continue;

                if (eventType == EventType.KeyDown)
                {
                    consumedEvent = true;

                    // Special handling on OSX which produces more events which need to be suppressed.
                    if (compositionLength == 0)
                    {
                        // Suppress other events related to navigation or termination of composition sequence.
                        if (processingEvent.character == 0 && processingEvent.modifiers == EventModifiers.None)
                            continue;
                    }

                    EditState editState = KeyPressed(processingEvent);
                    if (editState == EditState.Finish)
                    {
                        if (!wasCanceled)
                            SendOnSubmit();

                        DeactivateInputField();
                        break;
                    }

                    UpdateLabel();
                    textComponent.ForceMeshUpdate();

                    continue;
                }

                if (eventType is EventType.ValidateCommand or EventType.ExecuteCommand)
                    if (processingEvent.commandName == "SelectAll")
                    {
                        SelectAll();
                        consumedEvent = true;
                    }
            }

            if (consumedEvent)
            {
                UpdateLabel();
                eventData.Use();
            }
        }

#endregion

        private bool TryHandleSpecialKeys()
        {
            EventType eventType = processingEvent.type;

            if (eventType == EventType.KeyDown)
            {
                //This whole logic is so we can capture Ctrl+V events before they are sent to the input field
                //Otherwise the input field inserts each character pasted one by one, and sending on Input changed events
                //For each character which is not desirable, slows down the game quite a bit and also overflows our sounds manager
                //trying to play 200 sounds simultaneously.
                if (processingEvent.keyCode == KeyCode.LeftCommand || processingEvent.keyCode == KeyCode.LeftControl)
                    isControlPressed = true;
                else if (isControlPressed && processingEvent.keyCode == KeyCode.V)
                {
                    OnPasteShortcutPerformedEvent?.Invoke();
                    return true;
                }
                else if (!UpAndDownArrowsEnabled &&
                         processingEvent.keyCode is KeyCode.UpArrow or KeyCode.DownArrow)
                {
                    return true;
                }
            }
            else if (eventType == EventType.KeyUp)
                if (processingEvent.keyCode == KeyCode.LeftCommand || processingEvent.keyCode == KeyCode.LeftControl)
                    isControlPressed = false;

            return false;
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
