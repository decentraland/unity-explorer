using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
///     Example integration of OTPInputField with auto-verification.
///     Demonstrates how to connect the component to your authentication flow.
/// </summary>
public class OTPVerificationExample : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OTPInputField otpInput;
    [SerializeField] private Button resendButton;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private GameObject loadingIndicator;

    [Header("Configuration")]
    [SerializeField] private float resendCooldown = 60f;
    [SerializeField] private string mockValidCode = "123456"; // For testing

    private float resendTimer;
    private bool isVerifying;

    private void Start()
    {
        // Subscribe to OTP events
        if (otpInput != null)
        {
            otpInput.OnCodeComplete += OnCodeComplete;
            otpInput.OnCodeChanged += OnCodeChanged;
            otpInput.OnCodeCleared += OnCodeCleared;
        }

        if (resendButton != null) { resendButton.onClick.AddListener(OnResendClicked); }

        // Start resend cooldown
        StartResendCooldown();

        // Auto-focus on start
        otpInput?.Focus();

        UpdateStatus("Enter the 6-digit code sent to your email");
    }

    private void Update()
    {
        // Update resend timer
        if (resendTimer > 0)
        {
            resendTimer -= Time.deltaTime;

            if (timerText != null)
            {
                int seconds = Mathf.CeilToInt(resendTimer);
                timerText.text = $"Resend in {seconds}s";
            }

            if (resendButton != null) { resendButton.interactable = false; }

            if (resendTimer <= 0)
            {
                if (timerText != null) timerText.text = "";
                if (resendButton != null) resendButton.interactable = true;
            }
        }
    }

    /// <summary>
    ///     Called when all 6 digits have been entered.
    ///     This is where you trigger auto-verification.
    /// </summary>
    private void OnCodeComplete(string code)
    {
        Debug.Log($"[OTP] Code complete: {code}");

        if (!isVerifying) { StartCoroutine(VerifyCodeAsync(code)); }
    }

    private void OnCodeChanged(string code)
    {
        // Optional: Update UI as user types
        if (statusText != null && !isVerifying)
        {
            statusText.color = Color.white;
            statusText.text = $"Entering code... ({code.Length}/6)";
        }
    }

    private void OnCodeCleared()
    {
        UpdateStatus("Enter the 6-digit code sent to your email");
    }

    /// <summary>
    ///     Simulates async verification (replace with your actual API call)
    /// </summary>
    private IEnumerator VerifyCodeAsync(string code)
    {
        isVerifying = true;

        // Show loading state
        if (loadingIndicator != null) loadingIndicator.SetActive(true);
        UpdateStatus("Verifying...");

        // Simulate network delay
        yield return new WaitForSeconds(1.5f);

        // Mock verification (replace with actual API call)
        bool isValid = code == mockValidCode;

        if (loadingIndicator != null) loadingIndicator.SetActive(false);

        if (isValid) { OnVerificationSuccess(); }
        else { OnVerificationFailed(); }

        isVerifying = false;
    }

    private void OnVerificationSuccess()
    {
        UpdateStatus("✓ Code verified successfully!", Color.green);

        // Disable further input
        otpInput?.Unfocus();

        // TODO: Proceed to next step in your flow
        Debug.Log("[OTP] Verification successful! Proceeding...");

        // Example: Load next scene after delay
        // StartCoroutine(LoadNextSceneAfterDelay(1f));
    }

    private void OnVerificationFailed()
    {
        UpdateStatus("✗ Invalid code. Please try again.", Color.red);

        // Show error animation
        otpInput?.ShowError(0.3f);
        otpInput?.PlayShakeAnimation(8f, 0.4f);

        // Clear and refocus after a moment
        StartCoroutine(ClearAndRefocusAfterDelay(0.5f));
    }

    private IEnumerator ClearAndRefocusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        otpInput?.ClearCode();
        otpInput?.Focus();

        UpdateStatus("Enter the 6-digit code sent to your email");
    }

    private void OnResendClicked()
    {
        Debug.Log("[OTP] Resending code...");

        // TODO: Call your resend API here

        UpdateStatus("New code sent!", Color.cyan);
        StartResendCooldown();

        otpInput?.ClearCode();
        otpInput?.Focus();

        // Reset status after a moment
        StartCoroutine(ResetStatusAfterDelay(2f));
    }

    private IEnumerator ResetStatusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        UpdateStatus("Enter the 6-digit code sent to your email");
    }

    private void StartResendCooldown()
    {
        resendTimer = resendCooldown;
    }

    private void UpdateStatus(string message, Color? color = null)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color ?? Color.white;
        }
    }

    private void OnDestroy()
    {
        if (otpInput != null)
        {
            otpInput.OnCodeComplete -= OnCodeComplete;
            otpInput.OnCodeChanged -= OnCodeChanged;
            otpInput.OnCodeCleared -= OnCodeCleared;
        }
    }
}
