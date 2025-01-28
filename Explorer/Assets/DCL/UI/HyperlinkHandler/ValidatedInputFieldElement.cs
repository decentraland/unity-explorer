using System;
using TMPro;
using UnityEngine;

namespace DCL.UI.InputFieldValidator
{
    public class ValidatedInputFieldElement : MonoBehaviour
    {
        public event Action<string> OnInputValidated;

        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private InputFieldsValidator fieldsValidator;

        private int lastLenght;

        private void Awake()
        {
            inputField.characterValidation = TMP_InputField.CharacterValidation.CustomValidator;
            inputField.inputValidator = fieldsValidator;
            inputField.onValueChanged.AddListener(Validate);
        }

        private void Validate(string text)
        {
            if (lastLenght > text.Length)
            {
                int position = inputField.stringPosition;
                fieldsValidator.ValidateOnBackspace(ref text, ref position);
                inputField.stringPosition = position;
            }

            lastLenght = text.Length;
            OnInputValidated?.Invoke(text);
        }
    }
}
