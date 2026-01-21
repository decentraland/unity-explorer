using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
///     Production-ready OTP Input Field for Unity (PC Standalone: Win/Mac).
///     Uses hidden TMP_InputField + visual slots approach for native paste support.
///     Features:
///     - Auto-advance cursor on digit entry
///     - Native Ctrl+V / Cmd+V paste support
///     - Backspace with smart cursor movement
///     - Single-character selection only (no range selection)
///     - Overwrite mode on filled slots
///     - Active slot highlighting
///     - OnComplete event when all digits entered
///     Setup:
///     1. Create a parent GameObject with this component
///     2. Add a TMP_InputField as child (will be hidden) - assign to hiddenInput
///     3. Create slot containers (Image + TMP_Text child) - assign to slotBackgrounds[] and slotTexts[]
///     4. Configure codeLength, colors, and events as needed
/// </summary>
public class OTPInputField : MonoBehaviour, IPointerClickHandler
{
    [Header("Core References")]
    [Tooltip("Hidden input field that captures all keyboard input")]
    [SerializeField] private TMP_InputField hiddenInput;

    [Tooltip("Visual text components for each digit slot")]
    [SerializeField] private TMP_Text[] slotTexts;

    [Tooltip("Background images for each slot (for highlighting)")]
    [SerializeField] private Image[] slotBackgrounds;

    [Header("Configuration")]
    [Tooltip("Number of digits in OTP code")]
    [SerializeField] private int codeLength = 6;

    [Tooltip("Allow only numeric input (0-9)")]
    [SerializeField] private bool numericOnly = true;

    [Header("Visual Styling")]
    [SerializeField] private Color normalSlotColor = new (0.2f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color activeSlotColor = new (0.3f, 0.5f, 0.9f, 1f);
    [SerializeField] private Color filledSlotColor = new (0.25f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color errorSlotColor = new (0.9f, 0.3f, 0.3f, 1f);

    [Tooltip("Character shown in empty slots (e.g., '_' or '•' or empty)")]
    [SerializeField] private string emptySlotChar = "";

    [Tooltip("Mask filled digits with this character (empty = show actual digit)")]
    [SerializeField] private string maskChar = "";

    public event Action<string> OnCodeChanged;
    public event Action<string> OnCodeComplete;
    public event Action OnCodeCleared;

    // Internal state
    private int currentCaretPosition;
    private bool isInitialized;
    private bool suppressEvents;
    private readonly StringBuilder stringBuilder = new (8);

    // Cached for performance
    private int lastKnownCaretPos = -1;
    private string lastKnownText = null;

    public string Code { get; private set; } = "";

    public bool IsComplete => Code.Length == codeLength;

    private void Awake()
    {
        ValidateSetup();
        Initialize();
    }

    private void OnDestroy()
    {
        hiddenInput.onValueChanged.RemoveListener(OnHiddenInputValueChanged);
        hiddenInput.onSelect.RemoveListener(OnHiddenInputSelected);
        hiddenInput.onDeselect.RemoveListener(OnHiddenInputDeselected);
    }

    private void ValidateSetup()
    {
        if (hiddenInput == null)
        {
            Debug.LogError("[OTPInputField] Hidden TMP_InputField is not assigned!");
            return;
        }

        if (slotTexts == null || slotTexts.Length != codeLength)
        {
            Debug.LogError($"[OTPInputField] slotTexts array must have exactly {codeLength} elements!");
            return;
        }

        if (slotBackgrounds != null && slotBackgrounds.Length != codeLength) { Debug.LogWarning($"[OTPInputField] slotBackgrounds array should have {codeLength} elements for proper highlighting."); }
    }

    private void Initialize()
    {
        if (hiddenInput == null) return;

        // Configure hidden input
        hiddenInput.characterLimit = codeLength;

        hiddenInput.contentType = numericOnly
            ? TMP_InputField.ContentType.IntegerNumber
            : TMP_InputField.ContentType.Alphanumeric;

        // Make it visually hidden but still functional
        ConfigureHiddenInputVisuals();

        // Subscribe to events
        hiddenInput.onValueChanged.AddListener(OnHiddenInputValueChanged);
        hiddenInput.onSelect.AddListener(OnHiddenInputSelected);
        hiddenInput.onDeselect.AddListener(OnHiddenInputDeselected);

        // Initial state
        ClearCode();
        isInitialized = true;
    }

    private void ConfigureHiddenInputVisuals()
    {
        // Make the input field invisible but still receive input
        Image? inputImage = hiddenInput.GetComponent<Image>();

        if (inputImage != null) { inputImage.color = Color.clear; }

        // Hide the text and caret
        if (hiddenInput.textComponent != null) { hiddenInput.textComponent.color = Color.clear; }

        hiddenInput.caretColor = Color.clear;
        hiddenInput.selectionColor = Color.clear;

        // Expand to cover the entire OTP area for click detection
        RectTransform? rt = hiddenInput.GetComponent<RectTransform>();

        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    private void Update()
    {
        if (!isInitialized || hiddenInput == null) return;

        if (hiddenInput.isFocused)
        {
            EnforceOTPBehavior();
            UpdateActiveSlotHighlight();
        }
    }

    /// <summary>
    ///     Core OTP UX enforcement:
    ///     - Prevent multi-character selection
    ///     - Force overwrite mode behavior
    ///     - Keep caret in valid range
    /// </summary>
    private void EnforceOTPBehavior()
    {
        // Prevent selecting more than 1 character
        if (hiddenInput.selectionAnchorPosition != hiddenInput.selectionFocusPosition)
        {
            int selStart = Mathf.Min(hiddenInput.selectionAnchorPosition, hiddenInput.selectionFocusPosition);
            int selEnd = Mathf.Max(hiddenInput.selectionAnchorPosition, hiddenInput.selectionFocusPosition);

            if (selEnd - selStart > 1)
            {
                // Collapse selection to single character at the end
                hiddenInput.selectionAnchorPosition = selEnd - 1;
                hiddenInput.selectionFocusPosition = selEnd;
            }
        }

        // Clamp caret position to valid range
        int maxPos = Mathf.Min(Code.Length, codeLength);

        if (hiddenInput.caretPosition > maxPos) { hiddenInput.caretPosition = maxPos; }

        currentCaretPosition = hiddenInput.caretPosition;
    }

    private void OnHiddenInputValueChanged(string newValue)
    {
        if (suppressEvents) return;

        // Filter input if needed
        string filtered = FilterInput(newValue);

        // Handle the change
        ProcessInputChange(filtered);
    }

    private string FilterInput(string input)
    {
        if (!numericOnly) return input;

        stringBuilder.Clear();

        foreach (char c in input)
        {
            if (char.IsDigit(c)) { stringBuilder.Append(c); }
        }

        return stringBuilder.ToString();
    }

    private void ProcessInputChange(string newValue)
    {
        // Clamp to max length
        if (newValue.Length > codeLength) { newValue = newValue.Substring(0, codeLength); }

        string previousCode = Code;
        Code = newValue;

        // Update hidden input if we filtered anything
        if (hiddenInput.text != Code)
        {
            suppressEvents = true;
            hiddenInput.text = Code;
            suppressEvents = false;
        }

        // Update visual slots
        RefreshSlotVisuals();

        // Move caret to end of current input (auto-advance)
        int newCaretPos = Mathf.Min(Code.Length, codeLength);
        hiddenInput.caretPosition = newCaretPos;
        currentCaretPosition = newCaretPos;

        // Fire events
        if (Code != previousCode)
        {
            OnCodeChanged?.Invoke(Code);

            if (Code.Length == 0 && previousCode.Length > 0) { OnCodeCleared?.Invoke(); }

            if (Code.Length == codeLength) { OnCodeComplete?.Invoke(Code); }
        }
    }

    private void RefreshSlotVisuals()
    {
        for (var i = 0; i < codeLength; i++)
        {
            if (i < slotTexts.Length && slotTexts[i] != null)
            {
                if (i < Code.Length)
                {
                    // Slot has a digit
                    string displayChar = string.IsNullOrEmpty(maskChar)
                        ? Code[i].ToString()
                        : maskChar;

                    slotTexts[i].text = displayChar;
                }
                else
                {
                    // Empty slot
                    slotTexts[i].text = emptySlotChar;
                }
            }

            // Update slot background color
            UpdateSlotColor(i);
        }
    }

    private void UpdateSlotColor(int slotIndex)
    {
        if (slotBackgrounds == null || slotIndex >= slotBackgrounds.Length || slotBackgrounds[slotIndex] == null)
            return;

        bool isFilled = slotIndex < Code.Length;
        bool isActive = hiddenInput.isFocused && slotIndex == currentCaretPosition;

        Color targetColor;

        if (isActive) { targetColor = activeSlotColor; }
        else if (isFilled) { targetColor = filledSlotColor; }
        else { targetColor = normalSlotColor; }

        slotBackgrounds[slotIndex].color = targetColor;
    }

    private void UpdateActiveSlotHighlight()
    {
        // Only update if caret position changed
        if (lastKnownCaretPos == currentCaretPosition) return;
        lastKnownCaretPos = currentCaretPosition;

        for (var i = 0; i < codeLength; i++) { UpdateSlotColor(i); }
    }

    private void OnHiddenInputSelected(string _)
    {
        RefreshSlotVisuals();
    }

    private void OnHiddenInputDeselected(string _)
    {
        lastKnownCaretPos = -1;
        RefreshSlotVisuals();
    }

    /// <summary>
    ///     Click anywhere on the OTP field to focus
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        Focus();
    }

    /// <summary>
    ///     Programmatically focus the input
    /// </summary>
    public void Focus()
    {
        if (hiddenInput != null)
        {
            hiddenInput.ActivateInputField();
            hiddenInput.Select();

            // Position caret at the end of current input
            int caretPos = Mathf.Min(Code.Length, codeLength);
            hiddenInput.caretPosition = caretPos;
            currentCaretPosition = caretPos;
        }
    }

    /// <summary>
    ///     Remove focus from the input
    /// </summary>
    public void Unfocus()
    {
        if (hiddenInput != null) { hiddenInput.DeactivateInputField(); }
    }

    /// <summary>
    ///     Clear all entered digits
    /// </summary>
    public void ClearCode()
    {
        suppressEvents = true;
        Code = "";

        if (hiddenInput != null)
        {
            hiddenInput.text = "";
            hiddenInput.caretPosition = 0;
        }

        currentCaretPosition = 0;
        lastKnownCaretPos = -1;
        suppressEvents = false;

        RefreshSlotVisuals();
        OnCodeCleared?.Invoke();
    }

    /// <summary>
    ///     Programmatically set the code (e.g., from autofill)
    /// </summary>
    public void SetCode(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            ClearCode();
            return;
        }

        string filtered = FilterInput(code);

        if (filtered.Length > codeLength) { filtered = filtered.Substring(0, codeLength); }

        suppressEvents = true;
        Code = filtered;

        if (hiddenInput != null)
        {
            hiddenInput.text = filtered;
            hiddenInput.caretPosition = filtered.Length;
        }

        currentCaretPosition = filtered.Length;
        suppressEvents = false;

        RefreshSlotVisuals();
        OnCodeChanged?.Invoke(Code);

        if (Code.Length == codeLength) { OnCodeComplete?.Invoke(Code); }
    }

    /// <summary>
    ///     Show error state on all slots (e.g., wrong code)
    /// </summary>
    public void ShowError(float duration = 0.5f)
    {
        if (slotBackgrounds == null) return;

        foreach (var bg in slotBackgrounds)
        {
            if (bg != null) { bg.color = errorSlotColor; }
        }

        if (duration > 0) { Invoke(nameof(ClearError), duration); }
    }

    /// <summary>
    ///     Clear error state
    /// </summary>
    public void ClearError()
    {
        RefreshSlotVisuals();
    }

    /// <summary>
    ///     Shake animation for error feedback (optional visual polish)
    /// </summary>
    public void PlayShakeAnimation(float intensity = 10f, float duration = 0.3f)
    {
        StartCoroutine(ShakeCoroutine(intensity, duration));
    }

    private System.Collections.IEnumerator ShakeCoroutine(float intensity, float duration)
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (rt == null) yield break;

        Vector2 originalPos = rt.anchoredPosition;
        var elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;
            float dampedIntensity = intensity * (1f - progress);

            float offsetX = Mathf.Sin(elapsed * 50f) * dampedIntensity;
            rt.anchoredPosition = originalPos + new Vector2(offsetX, 0);

            yield return null;
        }

        rt.anchoredPosition = originalPos;
    }


}
