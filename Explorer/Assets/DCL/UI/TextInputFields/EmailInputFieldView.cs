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
        public event Action? StartButtonPressed;

        [SerializeField] private TMP_InputField emailInput;
        [SerializeField] private Button startButton;
        [SerializeField] private GameObject errorMark;

        [Header("FIELD OUTLINE")]
        [SerializeField] private Image emailInputOutline;
        [SerializeField] private Color outlineNormalColor = Color.white;
        [SerializeField] private Color outlineErrorColor = Color.red;

        [Header("CARET")]
        [SerializeField] private Color caretColor = Color.black;
        [SerializeField] [Min(0)] private int caretWidth = 2;
        [SerializeField] [Min(0)] private float caretBlinkRate = 0.85f;

        public string CurrentEmailText => emailInput.text;

        private Coroutine? activateInputCoroutine;

        private void Awake()
        {
            emailInput.caretColor = caretColor;
            emailInput.caretWidth = caretWidth;
            emailInput.caretBlinkRate = caretBlinkRate;

            emailInput.characterValidation = TMP_InputField.CharacterValidation.EmailAddress;
        }

        private void OnEnable()
        {
            emailInput.text = string.Empty;

            startButton.interactable = false;
            emailInputOutline.color = outlineNormalColor;
            errorMark.SetActive(false);

            activateInputCoroutine = StartCoroutine(ActivateInputFieldDelayed());

            // Listeners
            startButton.onClick.AddListener(EmitStartButtonPressedEvent);

            emailInput.onValueChanged.AddListener(OnEmailInputValueChanged);
            emailInput.onEndEdit.AddListener(OnEmailInputEndEdit);
            emailInput.onSelect.AddListener(OnEmailInputSelect);
        }

        private void OnDisable()
        {
            if (activateInputCoroutine != null)
            {
                StopCoroutine(activateInputCoroutine);
                activateInputCoroutine = null;
            }

            // Listeners
            startButton.onClick.RemoveAllListeners();

            emailInput.onValueChanged.RemoveAllListeners();
            emailInput.onEndEdit.RemoveAllListeners();
            emailInput.onSelect.RemoveAllListeners();
        }

        private void EmitStartButtonPressedEvent() =>
            StartButtonPressed?.Invoke();

        private void OnEmailInputValueChanged(string email)
        {
            bool isValidEmail = IsValidEmail(email);
            startButton.interactable = isValidEmail;

            if (isValidEmail)
                SetErrorState(false);
        }

        private void OnEmailInputEndEdit(string email) =>
            SetErrorState(!IsValidEmail(email));

        private void OnEmailInputSelect(string email) =>
            SetErrorState(false);

        private void SetErrorState(bool hasError)
        {
            emailInputOutline.color = hasError ? outlineErrorColor : outlineNormalColor;
            errorMark.SetActive(hasError);
        }

        private IEnumerator ActivateInputFieldDelayed()
        {
            yield return null;

            emailInput.caretPosition = 0;
            emailInput.ActivateInputField();
            emailInput.Select();

            activateInputCoroutine = null;
        }

        private static bool IsValidEmail(string email) =>
            EMAIL_PATTERN_REGEX.IsMatch(email);
    }
}
