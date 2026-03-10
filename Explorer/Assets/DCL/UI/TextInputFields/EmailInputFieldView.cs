using System;
using System.Collections;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class EmailInputFieldView : MonoBehaviour
    {
        private static readonly Regex EMAIL_PATTERN_REGEX = new (@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled);

        public event Action? Submitted;

        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private bool autoFocus;

        [Space]
        [SerializeField] private Button startButton;
        [SerializeField] private GameObject errorContainer;

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

        private void Awake()
        {
            inputField.caretColor = caretColor;
            inputField.caretWidth = caretWidth;
            inputField.caretBlinkRate = caretBlinkRate;

            inputField.characterValidation = TMP_InputField.CharacterValidation.EmailAddress;
        }

        private void OnEnable()
        {
            inputField.text = string.Empty;

            outline.enabled = false;
            outline.color = outlineNormalColor;
            errorContainer.SetActive(false);

            if (autoFocus)
                activateInputCoroutine = StartCoroutine(ActivateInputFieldDelayed());

            startButton.interactable = false;

            // Listeners
            inputField.onValueChanged.AddListener(OnInputValueChanged);
            inputField.onEndEdit.AddListener(OnInputEndEdit);
            inputField.onSelect.AddListener(OnInputSelected);

            startButton.onClick.AddListener(EmitStartButtonPressedEvent);
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

            startButton.onClick.RemoveAllListeners();
        }

        private void OnInputValueChanged(string email)
        {
            bool isValidEmail = IsValidEmail(email);
            startButton.interactable = isValidEmail;

            if (isValidEmail)
                SetErrorState(false);
        }

        private void SetErrorState(bool hasError)
        {
            outline.color = hasError ? outlineErrorColor : outlineNormalColor;
            errorContainer.SetActive(hasError);
        }

        private void EmitStartButtonPressedEvent() =>
            Submitted?.Invoke();

        private void OnInputEndEdit(string text)
        {
            if (text == string.Empty)
                outline.enabled = false;
            else if (!IsValidEmail(text))
                SetErrorState(true);
            else
                Submitted?.Invoke();
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

        private static bool IsValidEmail(string email) =>
            EMAIL_PATTERN_REGEX.IsMatch(email);
    }
}
