using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.UI.CustomInputField
{
    //TODO FRAN: This might need to be an extension of the TMP_InputField -> this way we avoid all the drawbacks of having to repeat input field events and can override certain events
    /// <summary>
    /// This custom class serves as an in-between other classes and the TMP_InputField, capturing some events
    /// and making checks when text is inserted and replaced, to make sure the input field limits aren't exceeded.
    /// </summary>
    public class CustomInputField : TMP_InputField
    {
       private bool isApplePlatform;
       private readonly Event processingEvent = new Event();

       public event Action? OnRightClickEvent;

       public bool UpAndDownArrowsEnabled { get; set; }

       protected override void Awake()
       {
           base.Awake();
           isApplePlatform = SystemInfo.operatingSystemFamily == OperatingSystemFamily.MacOSX || SystemInfo.operatingSystem.Contains("iOS");
       }

       public override void OnPointerClick(PointerEventData eventData)
       {
           if (eventData.button == PointerEventData.InputButton.Right)
           {
               ActivateInputField();
               OnRightClickEvent?.Invoke();
           }
           else
           {
               base.OnPointerClick(eventData);
           }
       }

       public override void OnUpdateSelected(BaseEventData eventData)
       {
           if (!isFocused)
               return;

           while (Event.PopEvent(processingEvent))
           {
               EventType eventType = processingEvent.rawType;
               if (eventType == EventType.KeyDown)
               {
                   if (processingEvent.keyCode == KeyCode.V)
                   {
                       var currentEventModifiers = processingEvent.modifiers;
                       bool ctrl = isApplePlatform ? (currentEventModifiers & EventModifiers.Command) != 0 : (currentEventModifiers & EventModifiers.Control) != 0;
                       bool shift = (currentEventModifiers & EventModifiers.Shift) != 0;
                       bool alt = (currentEventModifiers & EventModifiers.Alt) != 0;
                       bool ctrlOnly = ctrl && !alt && !shift;
                       if (ctrlOnly)
                       {
                           //TODO FRAN: We Could send an OnPaste event upwards?! better maybe?
                           InsertTextAtSelectedPosition(GUIUtility.systemCopyBuffer);
                           processingEvent.Use();
                       }
                   }
                   //If the UpAndDownArrows are not enabled, we skip these events so the input panel does not use them.
                   else if (!UpAndDownArrowsEnabled &&
                              processingEvent.keyCode is KeyCode.UpArrow or KeyCode.DownArrow)
                       processingEvent.Use();
               }
           }

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
