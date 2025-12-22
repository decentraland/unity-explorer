using System;
using System.Linq;
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
    [Header("References")]
    [Tooltip("Скрытый TMP_InputField для захвата ввода")]
    [SerializeField] private TMP_InputField hiddenInput;

    [Tooltip("TMP_Text компоненты для отображения цифр в каждом слоте")]
    [SerializeField] private TMP_Text[] slotTexts;

    [Tooltip("Image компоненты для фона слотов (для подсветки)")]
    [SerializeField] private Image[] slotBackgrounds;

    [Header("Settings")]
    [Tooltip("Количество цифр в коде")]
    [SerializeField] private int codeLength = 6;

    [Tooltip("Разрешить только цифры 0-9")]
    [SerializeField] private bool digitsOnly = true;

    [Header("Colors")]
    [SerializeField] private Color normalColor = new (0.15f, 0.15f, 0.18f, 1f);
    [SerializeField] private Color activeColor = new (0.25f, 0.45f, 0.85f, 1f);
    [SerializeField] private Color filledColor = new (0.2f, 0.2f, 0.24f, 1f);

    /// <summary>Вызывается при любом изменении кода</summary>
    public event Action<string> OnCodeChanged;

    /// <summary>Вызывается когда все цифры введены</summary>
    public event Action<string> OnCodeComplete;

    /// <summary>Текущий введённый код</summary>
    public string Code { get; private set; } = "";

    /// <summary>Все ли цифры введены</summary>
    public bool IsComplete => Code.Length == codeLength;

    /// <summary>В фокусе ли поле ввода</summary>
    public bool IsFocused => hiddenInput != null && hiddenInput.isFocused;

    private int lastCaretPos = -1;
    private bool suppressEvents;

    private void Awake()
    {
        ValidateReferences();
        SetupHiddenInput();
        RefreshVisuals();
    }

    private void ValidateReferences()
    {
        if (hiddenInput == null)
            Debug.LogError($"[{nameof(OtpInputBox)}] hiddenInput не назначен!");

        if (slotTexts == null || slotTexts.Length == 0)
            Debug.LogError($"[{nameof(OtpInputBox)}] slotTexts не назначен!");

        if (slotBackgrounds != null && slotBackgrounds.Length != slotTexts?.Length)
            Debug.LogWarning($"[{nameof(OtpInputBox)}] slotBackgrounds должен иметь {slotTexts?.Length} элементов");
    }

    private void SetupHiddenInput()
    {
        if (hiddenInput == null) return;

        // Лимит символов
        hiddenInput.characterLimit = codeLength;

        // Валидация: только цифры
        hiddenInput.onValidateInput += OnValidateChar;

        // Обработка изменений
        hiddenInput.onValueChanged.AddListener(OnInputValueChanged);
        hiddenInput.onSelect.AddListener(_ => RefreshVisuals());
        hiddenInput.onDeselect.AddListener(_ => RefreshVisuals());

        // Делаем input невидимым, но функциональным
        MakeInputInvisible();
    }

    private void MakeInputInvisible()
    {
        // Прозрачный фон
        Image? bg = hiddenInput.GetComponent<Image>();

        if (bg != null)
        {
            bg.color = Color.clear;
            bg.raycastTarget = true; // Важно для клика!
        }

        // Прозрачный текст
        if (hiddenInput.textComponent != null)
            hiddenInput.textComponent.color = Color.clear;

        // Прозрачный курсор и выделение
        hiddenInput.caretColor = Color.clear;
        hiddenInput.selectionColor = Color.clear;
    }

    private char OnValidateChar(string text, int charIndex, char addedChar)
    {
        if (!digitsOnly) return addedChar;
        return char.IsDigit(addedChar) ? addedChar : '\0';
    }

    private void OnInputValueChanged(string newValue)
    {
        if (suppressEvents) return;

        // Фильтруем
        string filtered = digitsOnly
            ? new string(newValue.Where(char.IsDigit).ToArray())
            : newValue;

        // Обрезаем до лимита
        if (filtered.Length > codeLength)
            filtered = filtered.Substring(0, codeLength);

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

        // События
        if (Code != previousCode)
        {
            OnCodeChanged?.Invoke(Code);

            if (Code.Length == codeLength)
                OnCodeComplete?.Invoke(Code);
        }
    }

    private void Update()
    {
        if (hiddenInput == null || !hiddenInput.isFocused) return;

        // Обновляем подсветку если каретка переместилась
        int caret = hiddenInput.caretPosition;

        if (caret != lastCaretPos)
        {
            lastCaretPos = caret;
            RefreshSlotHighlight();
        }
    }

    private void RefreshVisuals()
    {
        RenderDigits();
        RefreshSlotHighlight();
    }

    private void RenderDigits()
    {
        if (slotTexts == null) return;

        for (var i = 0; i < slotTexts.Length; i++)
        {
            if (slotTexts[i] == null) continue;
            slotTexts[i].text = i < Code.Length ? Code[i].ToString() : "";
        }
    }

    private void RefreshSlotHighlight()
    {
        if (slotBackgrounds == null) return;

        bool focused = hiddenInput != null && hiddenInput.isFocused;
        int caret = hiddenInput != null ? hiddenInput.caretPosition : 0;

        // Активный слот — где каретка, но не дальше последнего
        int activeSlot = Mathf.Clamp(caret, 0, slotBackgrounds.Length - 1);

        for (var i = 0; i < slotBackgrounds.Length; i++)
        {
            if (slotBackgrounds[i] == null) continue;

            Color color;

            if (focused && i == activeSlot)
                color = activeColor;
            else if (i < Code.Length)
                color = filledColor;
            else
                color = normalColor;

            slotBackgrounds[i].color = color;
        }
    }

    /// <summary>Клик на компонент — активирует ввод</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        Focus();
    }

    /// <summary>Активирует поле ввода</summary>
    public void Focus()
    {
        if (hiddenInput == null) return;

        hiddenInput.ActivateInputField();
        hiddenInput.Select();

        // Каретка в конец текущего ввода
        int pos = Mathf.Min(Code.Length, codeLength);
        hiddenInput.caretPosition = pos;
    }

    /// <summary>Деактивирует поле ввода</summary>
    public void Unfocus()
    {
        hiddenInput?.DeactivateInputField();
    }

    /// <summary>Очищает введённый код</summary>
    public void Clear()
    {
        suppressEvents = true;
        Code = "";

        if (hiddenInput != null)
        {
            hiddenInput.SetTextWithoutNotify("");
            hiddenInput.caretPosition = 0;
        }

        suppressEvents = false;
        lastCaretPos = -1;

        RefreshVisuals();
    }

    /// <summary>Очищает и фокусирует</summary>
    public void ClearAndFocus()
    {
        Clear();
        Focus();
    }

    /// <summary>Устанавливает код программно (например, автозаполнение)</summary>
    public void SetCode(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            Clear();
            return;
        }

        string filtered = digitsOnly
            ? new string(code.Where(char.IsDigit).ToArray())
            : code;

        if (filtered.Length > codeLength)
            filtered = filtered.Substring(0, codeLength);

        suppressEvents = true;
        Code = filtered;

        if (hiddenInput != null)
        {
            hiddenInput.SetTextWithoutNotify(filtered);
            hiddenInput.caretPosition = filtered.Length;
        }

        suppressEvents = false;
        RefreshVisuals();

        OnCodeChanged?.Invoke(Code);

        if (Code.Length == codeLength)
            OnCodeComplete?.Invoke(Code);
    }

    private void OnDestroy()
    {
        if (hiddenInput != null)
        {
            hiddenInput.onValidateInput -= OnValidateChar;
            hiddenInput.onValueChanged.RemoveListener(OnInputValueChanged);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Автоподгонка массивов в редакторе
        if (slotTexts != null && slotTexts.Length != codeLength)
            Array.Resize(ref slotTexts, codeLength);

        if (slotBackgrounds != null && slotBackgrounds.Length != codeLength)
            Array.Resize(ref slotBackgrounds, codeLength);
    }
#endif
}

