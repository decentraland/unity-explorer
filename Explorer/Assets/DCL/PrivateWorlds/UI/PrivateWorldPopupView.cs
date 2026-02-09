using System;
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
        [SerializeField] private GameObject wrongPasswordWarningObject = null!;
        [SerializeField] private TMP_Text? wrongPasswordMessageText;

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
        }

        private void OnDisable()
        {
            passwordVisibilityToggleButton.onClick.RemoveListener(TogglePasswordVisibility);
            passwordInputField.onValueChanged.RemoveListener(OnPasswordValueChanged);
        }

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
                if (wrongPasswordMessageText != null)
                    wrongPasswordMessageText.text = errorMessage;
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

        private void ResetPasswordSection()
        {
            if (passwordInputField != null)
            {
                passwordInputField.text = string.Empty;
                passwordInputField.contentType = TMP_InputField.ContentType.Password;
                passwordInputField.ForceLabelUpdate();
            }

            UpdatePasswordVisibilityState();

            if (wrongPasswordWarningObject != null)
            {
                wrongPasswordWarningObject.SetActive(false);
            }
        }

        /// <summary>
        /// Focuses the password input field.
        /// </summary>
        public void FocusPasswordInput()
        {
            if (passwordInputField != null && passwordInputField.gameObject.activeInHierarchy)
            {
                passwordInputField.Select();
                passwordInputField.ActivateInputField();
            }
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
            if (string.IsNullOrEmpty(value))
            {
                passwordInputField.contentType = TMP_InputField.ContentType.Password;
                passwordInputField.ForceLabelUpdate();
            }

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
