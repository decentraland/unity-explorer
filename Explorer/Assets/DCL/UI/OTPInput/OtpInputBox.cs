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
public sealed class OtpInputBox : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TMP_InputField hiddenInput;
    [SerializeField] private OTPSlotView[] slots;
    [SerializeField] private Image caretImage;

    [Header("SETTINGS")]
    [SerializeField] private int codeLength = 6;
    [SerializeField] private float caretBlinkRate = 1.5f;

    public enum State { Normal, Success, Failure }

    public event Action<string> OtpCodeEntered;

    public string Code { get; private set; } = "";

    public State CurrentState { get; private set; } = State.Normal;

    private bool suppressEvents;
    private float caretBlinkTimer;
    private RectTransform caretRectTransform;

    private void Awake()
    {
        caretRectTransform = caretImage.GetComponent<RectTransform>();

        // Hidden Input
        hiddenInput.characterLimit = codeLength;
        hiddenInput.characterValidation = TMP_InputField.CharacterValidation.Digit;

        hiddenInput.onValueChanged.AddListener(OnInputValueChanged);
        hiddenInput.onSelect.AddListener(_ => RefreshVisuals());
        hiddenInput.onDeselect.AddListener(_ => Unfocus());

        MakeInputInvisible();
        RefreshVisuals();
    }

    private void OnDestroy()
    {
        hiddenInput.onValueChanged.RemoveAllListeners();
        hiddenInput.onSelect.RemoveAllListeners();
        hiddenInput.onDeselect.RemoveAllListeners();
    }

    public void Reset()
    {
        CurrentState = State.Normal;
        Clear();
    }

    private void MakeInputInvisible()
    {
        // Прозрачный фон
        Image? bg = hiddenInput.GetComponent<Image>();
        if (bg != null)
        {
            bg.color = Color.clear;
            bg.raycastTarget = true;
        }

        if (hiddenInput.textComponent != null)
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

        string previousCode = Code;
        Code = filtered;

        RefreshVisuals();

        if (Code != previousCode && Code.Length == codeLength)
        {
            OtpCodeEntered?.Invoke(Code);
            Unfocus();
        }
    }

    private void Update()
    {
        bool focused = hiddenInput != null && hiddenInput.isFocused;

        if (focused)
        {
            // Каретка всегда на позиции после последней введённой цифры
            int expectedPos = Code.Length;

            if (hiddenInput.caretPosition != expectedPos)
                hiddenInput.caretPosition = expectedPos;

            // Мигание каретки
            UpdateCaretBlink();
        }
    }

    private void UpdateCaretBlink()
    {
        if (CurrentState != State.Normal) return;

        caretBlinkTimer += Time.deltaTime * caretBlinkRate;

        // Мигание каретки: видима первую половину цикла
        bool visible = (caretBlinkTimer % 1f) < 0.5f;
        caretImage.enabled = visible;
    }

    private void RefreshVisuals(bool? forceFocused = null)
    {
        for (var i = 0; i < slots.Length; i++)
            slots[i].SetSlotText(i < Code.Length ? Code[i].ToString() : "");

        foreach (OTPSlotView slot in slots)
        {
            switch (CurrentState)
            {
                case State.Normal: slot.SetSlotState(OTPSlotView.SlotState.UNSELECTED); break;
                case State.Failure: slot.SetSlotState(OTPSlotView.SlotState.ERROR); break;
                case State.Success: slot.SetSlotState(OTPSlotView.SlotState.SUCCESS); break;
            }
        }

        bool focused = forceFocused ?? hiddenInput.isFocused;
        int activeSlot = Mathf.Clamp(Code.Length, 0, slots.Length - 1);

        if (focused)
        {
            slots[activeSlot].SetSlotState(OTPSlotView.SlotState.SELECTED);
            RectTransform slotRect = slots[activeSlot].GetComponent<RectTransform>();
            caretRectTransform.position = slotRect.position;
        }
        else
            caretImage.enabled = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (hiddenInput.isFocused) return;
        FocusInternal();
    }

    private void FocusInternal()
    {
        // Сброс состояния и очистка при входе в фокус
        CurrentState = State.Normal;

        // Очистка без RefreshVisuals (вызовем вручную с forceFocused)
        suppressEvents = true;
        Code = "";
        hiddenInput.SetTextWithoutNotify("");
        suppressEvents = false;

        hiddenInput.ActivateInputField();
        hiddenInput.Select();

        // Каретка на первый слот
        hiddenInput.caretPosition = 0;

        // Сброс мигания — каретка сразу видима
        caretBlinkTimer = 0f;

        // Принудительно показываем каретку и outline (isFocused ещё false)
        RefreshVisuals(true);
    }

    /// <summary>Деактивирует поле ввода</summary>
    public void Unfocus()
    {
        hiddenInput?.DeactivateInputField();

        caretImage.enabled = false;

        foreach (OTPSlotView slot in slots)
            slot.SetSlotState(OTPSlotView.SlotState.UNSELECTED);
    }

    /// <summary>Очищает введённый код</summary>
    public void Clear()
    {
        suppressEvents = true;
        Code = "";

        hiddenInput.SetTextWithoutNotify("");
        hiddenInput.caretPosition = 0;

        suppressEvents = false;

        RefreshVisuals();
    }

    /// <summary>Очищает, сбрасывает состояние и фокусирует (принудительно, даже если уже в фокусе)</summary>
    public void ClearAndFocus() =>
        FocusInternal();

    [ContextMenu(nameof(SetSuccess))]
    public void SetSuccess()
    {
        CurrentState = State.Success;
        Unfocus();
        RefreshVisuals();
    }

    [ContextMenu(nameof(SetFailure))]
    public void SetFailure()
    {
        CurrentState = State.Failure;
        Unfocus();
        RefreshVisuals();
    }

    [ContextMenu(nameof(ResetState))]
    public void ResetState()
    {
        CurrentState = State.Normal;
        RefreshVisuals();
    }
}



