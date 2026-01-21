using DCL.UI.OTPInput;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
///     OTP Input Box — визуальный ввод кода с отдельными слотами.
///     Итерация 1: базовый функционал.
///     - Скрытый TMP_InputField для захвата ввода и поддержки paste
///     - Визуальные слоты (Image + TMP_Text) для отображения цифр
///     - Клик на любой слот/область активирует ввод
///     - Цифры заполняют слоты последовательно
///     - Лимит по количеству цифр (по умолчанию 6)
/// </summary>
public sealed class OTPInputFieldView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TMP_InputField hiddenInput;
    [SerializeField] private OTPSlotView[] slots;
    [SerializeField] private Image caretImage;

    [Header("SETTINGS")]
    [SerializeField] private int codeLength = 6;
    [SerializeField] private float caretBlinkRate = 1.5f;

    private enum State { NORMAL, SUCCESS, FAILURE }

    public event Action<string>? OTPCodeEntered;

    private string code = "";
    private State currentState = State.NORMAL;

    private bool suppressEvents;
    private float caretBlinkTimer;

    private RectTransform caretRectTransform;

    private void Awake()
    {
        caretRectTransform = caretImage.GetComponent<RectTransform>();

        // Hidden Input
        hiddenInput.characterLimit = codeLength;
        hiddenInput.characterValidation = TMP_InputField.CharacterValidation.Digit;

        // hiddenInput.onValueChanged.AddListener(OnInputValueChanged);
        hiddenInput.onSelect.AddListener(OnSelected);

        // hiddenInput.onDeselect.AddListener(_ => Unfocus());

        MakeInputInvisible();
        RefreshVisuals();
    }

    private void OnDestroy()
    {
        // hiddenInput.onValueChanged.RemoveAllListeners();
        hiddenInput.onSelect.RemoveAllListeners();

        // hiddenInput.onDeselect.RemoveAllListeners();
    }

    private void Update()
    {
        if (!hiddenInput.isFocused) return;

        // caret is always in position after the last entered digit
        int expectedPos = code.Length;

        if (hiddenInput.caretPosition != expectedPos)
            hiddenInput.caretPosition = expectedPos;

        if (currentState == State.NORMAL)
            UpdateCaretBlink();

        return;

        void UpdateCaretBlink()
        {
            caretBlinkTimer += Time.deltaTime * caretBlinkRate;

            bool visible = caretBlinkTimer % 1f < 0.5f; // Carret blink:  visible first half of the cycle
            caretImage.enabled = visible;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (hiddenInput.isFocused) return;
        ClearAndFocusInternal();
    }

    private void OnSelected(string _)
    {
        // hiddenInput.text;
    }

    private void RefreshVisuals(bool? forceFocused = null)
    {
        for (var i = 0; i < slots.Length; i++)
        {
            OTPSlotView slot = slots[i];
            slot.Text = i < code.Length ? code[i].ToString() : "";

            switch (currentState)
            {
                case State.NORMAL: slot.SetState(OTPSlotView.SlotState.UNSELECTED); break;
                case State.FAILURE: slot.SetState(OTPSlotView.SlotState.ERROR); break;
                case State.SUCCESS: slot.SetState(OTPSlotView.SlotState.SUCCESS); break;
            }
        }

        bool focused = forceFocused ?? hiddenInput.isFocused;
        int activeSlot = Mathf.Clamp(code.Length, 0, slots.Length - 1);

        if (focused)
        {
            slots[activeSlot].SetState(OTPSlotView.SlotState.SELECTED);
            RectTransform slotRect = slots[activeSlot].GetComponent<RectTransform>();
            caretRectTransform.position = slotRect.position;
        }
        else
            caretImage.enabled = false;
    }

    public void Clear()
    {
        suppressEvents = true;
        code = "";

        hiddenInput.SetTextWithoutNotify("");
        hiddenInput.caretPosition = 0;

        suppressEvents = false;

        RefreshVisuals();
    }

    private void MakeInputInvisible()
    {
        Image? bg = hiddenInput.GetComponent<Image>();
        if (bg != null)
        {
            bg.color = Color.clear;
            bg.raycastTarget = true;
        }

        hiddenInput.textComponent.color = Color.clear;
        hiddenInput.caretColor = Color.clear;
        hiddenInput.selectionColor = Color.clear;
    }

    private void OnInputValueChanged(string newValue)
    {
        if (suppressEvents) return;

        string filtered = newValue;
        if (filtered.Length > codeLength)
            filtered = filtered[..codeLength];

        // Если изменили — обновляем без повторного события
        if (filtered != newValue)
        {
            suppressEvents = true;
            hiddenInput.SetTextWithoutNotify(filtered);
            hiddenInput.caretPosition = Mathf.Min(hiddenInput.caretPosition, filtered.Length);
            suppressEvents = false;
        }

        string previousCode = code;
        code = filtered;

        RefreshVisuals();

        if (code != previousCode && code.Length == codeLength)
        {
            OTPCodeEntered?.Invoke(code);
            Unfocus();
        }
    }

    private void ClearAndFocusInternal()
    {
        currentState = State.NORMAL;

        suppressEvents = true;
        code = "";
        hiddenInput.SetTextWithoutNotify("");
        suppressEvents = false;

        hiddenInput.ActivateInputField();
        hiddenInput.Select();

        hiddenInput.caretPosition = 0;
        caretBlinkTimer = 0f;

        RefreshVisuals(true);
    }

    public void ClearAndForceFocus() =>
        ClearAndFocusInternal();

    private void Unfocus()
    {
        hiddenInput?.DeactivateInputField();
        caretImage.enabled = false;

        foreach (OTPSlotView slot in slots)
            slot.SetState(OTPSlotView.SlotState.UNSELECTED);
    }

    [ContextMenu(nameof(SetSuccess))]
    public void SetSuccess()
    {
        currentState = State.SUCCESS;
        Unfocus();
        RefreshVisuals();
    }

    [ContextMenu(nameof(SetFailure))]
    public void SetFailure()
    {
        currentState = State.FAILURE;
        Unfocus();
        RefreshVisuals();
    }

    [ContextMenu(nameof(ResetState))]
    public void ResetState()
    {
        currentState = State.NORMAL;
        RefreshVisuals();
    }
}
