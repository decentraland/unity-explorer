using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.PrivateWorlds.UI
{
    /// <summary>
    /// View for the private world access popup.
    /// Supports two modes: PasswordRequired and AccessDenied (Invitation Only).
    /// </summary>
    public class PrivateWorldPopupView : ViewBase, IView
    {
        [Header("Content Groups")]
        [SerializeField] private GameObject passwordRequiredContent = null!;
        [SerializeField] private GameObject accessDeniedContent = null!;
        
        [Header("Password")]
        [SerializeField] private TMP_InputField passwordInputField = null!;
        [SerializeField] private Image? passwordInputOutlineImage;
        [SerializeField] private GameObject wrongPasswordWarningObject = null!;
        [SerializeField] private TMP_Text? errorMessageText;

        private static readonly Color PASSWORD_INPUT_ERROR_OUTLINE_COLOR = new (1f, 0.18f, 0.33f, 1f);
        private static readonly Color PASSWORD_INPUT_DEFAULT_OUTLINE_COLOR = new (0.988f, 0.988f, 0.988f, 1f);

        [Header("Password Visibility")]
        [SerializeField] private Button passwordVisibilityToggleButton = null!;
        [SerializeField] private GameObject passwordVisibleIcon = null!;
        [SerializeField] private GameObject passwordHiddenIcon = null!;

        [Header("Buttons")]
        [SerializeField] private Button passwordConfirmButton = null!;
        [SerializeField] private Button passwordCancelButton = null!;
        [SerializeField] private Button invitationButtonConfirm = null!;
        [SerializeField] private Button backgroundCloseButton = null!;

        private PrivateWorldPopupMode currentMode;

        public Button InvitationConfirmButton => invitationButtonConfirm;
        public Button PasswordConfirmButton => passwordConfirmButton;
        public Button PasswordCancelButton => passwordCancelButton;
        public Button BackgroundCloseButton => backgroundCloseButton;
        public PrivateWorldPopupMode CurrentMode => currentMode;

        /// <summary>
        /// Password entered by user.
        /// </summary>
        public string EnteredPassword => passwordInputField != null ? passwordInputField.text : string.Empty;

        private void OnEnable()
        {
            passwordVisibilityToggleButton.onClick.AddListener(TogglePasswordVisibility);
            passwordInputField.onValueChanged.AddListener(OnPasswordValueChanged);
            passwordInputField.onSubmit.AddListener(OnPasswordSubmitted);
        }

        private void OnDisable()
        {
            passwordVisibilityToggleButton.onClick.RemoveListener(TogglePasswordVisibility);
            passwordInputField.onValueChanged.RemoveListener(OnPasswordValueChanged);
            passwordInputField.onSubmit.RemoveListener(OnPasswordSubmitted);
        }

        private void OnPasswordSubmitted(string _) =>
            passwordConfirmButton.onClick.Invoke();

        /// <summary>
        /// Configures the popup for the given parameters.
        /// </summary>
        public void Configure(PrivateWorldPopupParams parameters)
        {
            currentMode = parameters.Mode;

            switch (parameters.Mode)
            {
                case PrivateWorldPopupMode.PasswordRequired:
                    ConfigureForPasswordRequired(parameters.ErrorMessage);
                    break;

                case PrivateWorldPopupMode.AccessDenied:
                    ConfigureForAccessDenied();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void ConfigureForPasswordRequired(string? errorMessage)
        {
            passwordRequiredContent.SetActive(true);
            accessDeniedContent.SetActive(false);

            ResetPasswordSection();
            if (!string.IsNullOrEmpty(errorMessage))
            {
                wrongPasswordWarningObject.SetActive(true);
                SetPasswordInputOutlineError(true);
                if (errorMessageText != null)
                {
                    errorMessageText.text = errorMessage;
                    errorMessageText.gameObject.SetActive(true);
                }
            }
            else
            {
                SetPasswordInputOutlineError(false);
                if (errorMessageText != null)
                    errorMessageText.gameObject.SetActive(false);
            }
        }

        private void ConfigureForAccessDenied()
        {
            passwordRequiredContent.SetActive(false);
            accessDeniedContent.SetActive(true);
        }

        /// <summary>
        /// Gets the confirm button click task.
        /// </summary>
        public UniTask WaitForConfirmAsync(CancellationToken ct) =>
            passwordConfirmButton.OnClickAsync(ct);

        /// <summary>
        /// Gets the cancel button click task.
        /// </summary>
        public UniTask WaitForCancelAsync(CancellationToken ct) =>
            passwordCancelButton.OnClickAsync(ct);

        /// <summary>
        /// Resets the popup state (password field and wrong-password warning).
        /// </summary>
        public void ResetState()
        {
            ResetPasswordSection();
        }

        /// <summary>
        /// Enables or disables the confirm button during validation to prevent double-clicks.
        /// When validation ends, restores interactable based on whether the password field has text.
        /// </summary>
        public void SetValidating(bool validating)
        {
            passwordConfirmButton.interactable = !validating &&
                                                 !string.IsNullOrEmpty(passwordInputField?.text);
        }

        /// <summary>
        /// Shows the password error in-place without resetting the password field.
        /// </summary>
        public void ShowPasswordError(string message)
        {
            wrongPasswordWarningObject.SetActive(true);
            SetPasswordInputOutlineError(true);
            if (errorMessageText != null)
            {
                errorMessageText.text = message;
                errorMessageText.gameObject.SetActive(true);
            }
        }

        private void ResetPasswordSection()
        {
            if (passwordInputField != null)
            {
                passwordInputField.text = string.Empty;
                passwordInputField.contentType = TMP_InputField.ContentType.Password;
                passwordInputField.ForceLabelUpdate();
            }

            UpdatePasswordVisibilityState();
            SetPasswordInputOutlineError(false);

            if (wrongPasswordWarningObject != null)
                wrongPasswordWarningObject.SetActive(false);

            if (errorMessageText != null)
                errorMessageText.gameObject.SetActive(false);

            passwordConfirmButton.interactable = false;
        }

        private void SetPasswordInputOutlineError(bool isError)
        {
            if (passwordInputOutlineImage == null)
                return;

            passwordInputOutlineImage.color = isError ? PASSWORD_INPUT_ERROR_OUTLINE_COLOR : PASSWORD_INPUT_DEFAULT_OUTLINE_COLOR;
        }

        /// <summary>
        /// Focuses the password input field after a short delay.
        /// The popup controller implements IBlocksChat, so the MVC system automatically
        /// minimizes the chat before the popup shows — no manual chat closing needed.
        /// The single-frame delay lets the UI layout settle before activating the field.
        /// </summary>
        public void FocusPasswordInput()
        {
            if (passwordInputField != null && passwordInputField.gameObject.activeInHierarchy)
                StartCoroutine(ActivateInputFieldDelayed());
        }

        private IEnumerator ActivateInputFieldDelayed()
        {
            yield return null;

            passwordInputField.caretPosition = 0;
            passwordInputField.ActivateInputField();
            passwordInputField.Select();
        }

        private void TogglePasswordVisibility()
        {
            passwordInputField.contentType = passwordInputField.contentType == TMP_InputField.ContentType.Password
                ? TMP_InputField.ContentType.Standard
                : TMP_InputField.ContentType.Password;

            passwordInputField.ForceLabelUpdate();
            UpdatePasswordVisibilityState();
        }

        private void OnPasswordValueChanged(string value)
        {
            bool hasText = !string.IsNullOrEmpty(value);

            if (!hasText)
            {
                passwordInputField.contentType = TMP_InputField.ContentType.Password;
                passwordInputField.ForceLabelUpdate();
                wrongPasswordWarningObject.SetActive(false);
                SetPasswordInputOutlineError(false);
                if (errorMessageText != null)
                    errorMessageText.gameObject.SetActive(false);
            }

            passwordConfirmButton.interactable = hasText;
            UpdatePasswordVisibilityState();
        }

        private void UpdatePasswordVisibilityState()
        {
            bool hasText = passwordInputField != null && !string.IsNullOrEmpty(passwordInputField.text);
            bool isVisible = passwordInputField != null && passwordInputField.contentType != TMP_InputField.ContentType.Password;

            passwordVisibilityToggleButton.gameObject.SetActive(hasText);

            if (passwordVisibleIcon != null)
                passwordVisibleIcon.SetActive(isVisible);

            if (passwordHiddenIcon != null)
                passwordHiddenIcon.SetActive(!isVisible);
        }
    }
}
