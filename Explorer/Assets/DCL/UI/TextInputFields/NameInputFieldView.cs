using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class NameInputFieldView : MonoBehaviour
    {
        public event Action<bool>? InputValueChanged;

        [SerializeField] private TMP_InputField inputField;

        [Header("CHARACTERS COUNT")]
        [SerializeField] private TMP_Text characterCountLabel;
        [SerializeField] [Min(3)] private int maxNameLength = 15;
        [SerializeField] private string characterLimitReachedMessage = "Character limit reached";
        [SerializeField] private Color textErrorColor = Color.red;

        [Header("ERROR")]
        [SerializeField] private GameObject errorContainer;
        [SerializeField] private TMP_Text inputErrorMessage;

        [Header("FIELD OUTLINE")]
        [SerializeField] private Image outline;
        [SerializeField] private Color outlineNormalColor = Color.white;
        [SerializeField] private Color outlineErrorColor = Color.red;

        [Header("CARET")]
        [SerializeField] private Color caretColor = Color.black;
        [SerializeField] [Min(0)] private int caretWidth = 2;
        [SerializeField] [Min(0)] private float caretBlinkRate = 0.85f;

        private Coroutine? activateInputCoroutine;

        public string Text => inputField.text;
        public bool IsValidName => inputField.text.Length > 0 && inputField.text.Length <= maxNameLength;

        private void Awake()
        {
            inputErrorMessage.text = characterLimitReachedMessage;

            inputField.caretColor = caretColor;
            inputField.caretWidth = caretWidth;
            inputField.caretBlinkRate = caretBlinkRate;

            inputField.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;

            inputField.text = string.Empty;
            characterCountLabel.text = $"{0}/{maxNameLength}";
        }

        private void OnEnable()
        {
            inputField.text = string.Empty;

            outline.enabled = false;
            outline.color = outlineNormalColor;
            errorContainer.SetActive(false);

            activateInputCoroutine = StartCoroutine(ActivateInputFieldDelayed());

            characterCountLabel.color = outlineNormalColor;

            // Listeners
            inputField.onValueChanged.AddListener(OnInputValueChanged);
            inputField.onEndEdit.AddListener(OnInputEndEdit);
            inputField.onSelect.AddListener(OnInputSelected);
        }

        private void OnDisable()
        {
            if (activateInputCoroutine != null)
            {
                StopCoroutine(activateInputCoroutine);
                activateInputCoroutine = null;
            }

            // Listeners
            inputField.onValueChanged.RemoveAllListeners();
            inputField.onEndEdit.RemoveAllListeners();
            inputField.onSelect.RemoveAllListeners();
        }

        private void OnInputValueChanged(string text)
        {
            characterCountLabel.text = $"{text.Length}/{maxNameLength}";
            SetErrorState(!IsValidName);

            InputValueChanged?.Invoke(IsValidName);
        }

        private void SetErrorState(bool hasError)
        {
            outline.color = hasError ? outlineErrorColor : outlineNormalColor;
            characterCountLabel.color = hasError ? textErrorColor : outlineNormalColor;

            errorContainer.SetActive(hasError);

            inputErrorMessage.text =
                inputField.text.Length == 0 ? "Name can't be empty." : characterLimitReachedMessage;
        }

        private void OnInputEndEdit(string text)
        {
            if (IsValidName)
                outline.enabled = false;
            else
                SetErrorState(true);
        }

        private void OnInputSelected(string _)
        {
            outline.enabled = true;
            SetErrorState(false);
        }

        private IEnumerator ActivateInputFieldDelayed()
        {
            yield return null;

            inputField.caretPosition = 0;
            inputField.ActivateInputField();
            inputField.Select();

            activateInputCoroutine = null;
        }
    }
}
