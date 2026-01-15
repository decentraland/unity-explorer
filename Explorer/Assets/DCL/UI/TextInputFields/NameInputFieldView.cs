using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class NameInputFieldView : MonoBehaviour
    {
        public event Action<bool>? NameValidityChanged;

        [SerializeField] private TMP_InputField NameInput;

        [Header("CHARACTERS COUNT")]
        [SerializeField] private TMP_Text characterCountLabel;
        [SerializeField] [Min(3)] private int maxNameLength = 15;
        [SerializeField] private string characterLimitReachedMessage = "Character limit reached";

        [Header("ERROR")]
        [SerializeField] private GameObject errorContainer;
        [SerializeField] private TMP_Text inputErrorMessage;

        [Header("FIELD OUTLINE")]
        [SerializeField] private Image inputOutline;
        [SerializeField] private Color outlineNormalColor = Color.white;
        [SerializeField] private Color outlineErrorColor = Color.red;

        [Header("CARET")]
        [SerializeField] private Color caretColor = Color.black;
        [SerializeField] [Min(0)] private int caretWidth = 2;
        [SerializeField] [Min(0)] private float caretBlinkRate = 0.85f;

        public string CurrentNameText => NameInput.text;

        private Coroutine? activateInputCoroutine;

        private void Awake()
        {
            inputErrorMessage.text = characterLimitReachedMessage;

            NameInput.caretColor = caretColor;
            NameInput.caretWidth = caretWidth;
            NameInput.caretBlinkRate = caretBlinkRate;

            NameInput.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;

            NameInput.text = string.Empty;
            characterCountLabel.text = $"{0}/{maxNameLength}";
        }

        private void OnEnable()
        {
            activateInputCoroutine = StartCoroutine(ActivateInputFieldDelayed());

            NameInput.onValueChanged.AddListener(OnNameInputValueChanged);
            NameInput.text = string.Empty; // it will trigger UpdateState
            errorContainer.SetActive(false);
        }

        private void OnDisable()
        {
            if (activateInputCoroutine != null)
            {
                StopCoroutine(activateInputCoroutine);
                activateInputCoroutine = null;
            }

            NameInput.onValueChanged.RemoveListener(OnNameInputValueChanged);
        }

        private void OnNameInputValueChanged(string text)
        {
            characterCountLabel.text = $"{text.Length}/{maxNameLength}";
            SetErrorState(text.Length > maxNameLength);
        }

        private void SetErrorState(bool hasError)
        {
            inputOutline.color = characterCountLabel.color = hasError ? outlineErrorColor : outlineNormalColor;

            if (errorContainer.activeSelf != hasError)
                NameValidityChanged?.Invoke(!hasError);

            errorContainer.SetActive(hasError);
        }

        private IEnumerator ActivateInputFieldDelayed()
        {
            yield return null;

            NameInput.caretPosition = 0;
            NameInput.ActivateInputField();
            NameInput.Select();

            activateInputCoroutine = null;
        }
    }
}
