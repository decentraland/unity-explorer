using System;
using System.Collections;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class EmailInputView : MonoBehaviour
    {
        private static readonly Regex EMAIL_PATTERN_REGEX = new (@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled);
        public event Action? StartButtonPressed;

        [SerializeField] private TMP_InputField emailInput;
        [SerializeField] private Button startButton;

        [SerializeField] private Image emailInputOutline;
        [SerializeField] private Color outlineNormalColor;
        [SerializeField] private Color outlineErrorColor;
        [SerializeField] private GameObject emailErrorMark;

        public string CurrentEmailText => emailInput.text;

        private Coroutine? activateInputCoroutine;

        private void OnEnable()
        {
            startButton.onClick.AddListener(EmitStartButtonPressedEvent);

            emailInput.onValueChanged.AddListener(OnEmailInputValueChanged);
            emailInput.onEndEdit.AddListener(OnEmailInputEndEdit);
            emailInput.onSelect.AddListener(OnEmailInputSelect);

            emailInput.text = string.Empty;

            startButton.interactable = false;
            emailInputOutline.color = outlineNormalColor;
            emailErrorMark.SetActive(false);

            activateInputCoroutine = StartCoroutine(ActivateInputFieldDelayed());
        }

        private void OnDisable()
        {
            if (activateInputCoroutine != null)
            {
                StopCoroutine(activateInputCoroutine);
                activateInputCoroutine = null;
            }

            startButton.onClick.RemoveAllListeners();

            emailInput.onValueChanged.RemoveAllListeners();
            emailInput.onEndEdit.RemoveAllListeners();
            emailInput.onSelect.RemoveAllListeners();
        }

        private IEnumerator ActivateInputFieldDelayed()
        {
            yield return null;

            emailInput.caretPosition = 0;
            emailInput.ActivateInputField();
            emailInput.Select();

            activateInputCoroutine = null;
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
            emailErrorMark.SetActive(hasError);
        }

        private static bool IsValidEmail(string email) =>
            !string.IsNullOrEmpty(email) && EMAIL_PATTERN_REGEX.IsMatch(email);
    }
}
