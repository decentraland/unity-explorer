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

    [Tooltip("Префабы слотов — из них автоматически извлекаются компоненты")]
    [SerializeField] private GameObject[] slots;

    [Tooltip("Image для имитации каретки (вертикальная линия)")]
    [SerializeField] private Image caretImage;

    // Заполняются автоматически в Awake из slots
    private TMP_Text[] slotTexts;
    private Image[] slotBackgrounds;
    private Image[] slotOutlines;

    [Header("Settings")]
    [Tooltip("Количество цифр в коде")]
    [SerializeField] private int codeLength = 6;

    [Tooltip("Разрешить только цифры 0-9")]
    [SerializeField] private bool digitsOnly = true;

    [Header("Colors")]
    [SerializeField] private Color normalColor = new (0.15f, 0.15f, 0.18f, 1f);
    [SerializeField] private Color successColor = new (0.2f, 0.7f, 0.4f, 1f);
    [SerializeField] private Color failureColor = new (0.8f, 0.3f, 0.3f, 1f);

    [Header("Caret")]
    [Tooltip("Скорость мигания каретки (раз в секунду)")]
    [SerializeField] private float caretBlinkRate = 1.5f;

    public enum State { Normal, Success, Failure }

    /// <summary>Вызывается при любом изменении кода</summary>
    public event Action<string> OnCodeChanged;

    /// <summary>Вызывается когда все цифры введены</summary>
    public event Action<string> OtpCodeEntered;

    /// <summary>Текущий введённый код</summary>
    public string Code { get; private set; } = "";

    /// <summary>Все ли цифры введены</summary>
    public bool IsComplete => Code.Length == codeLength;

    /// <summary>В фокусе ли поле ввода</summary>
    public bool IsFocused => hiddenInput != null && hiddenInput.isFocused;

    /// <summary>Текущее состояние компонента</summary>
    public State CurrentState { get; private set; } = State.Normal;

    private bool suppressEvents;
    private float caretBlinkTimer;
    private RectTransform caretRectTransform;

    private void Awake()
    {
        InitializeSlotArrays();
        InitializeCaret();
        ValidateReferences();
        SetupHiddenInput();
        RefreshVisuals();
    }

    private void InitializeCaret()
    {
        if (caretImage != null)
            caretRectTransform = caretImage.GetComponent<RectTransform>();
    }

    private void InitializeSlotArrays()
    {
        if (slots == null || slots.Length == 0)
        {
            Debug.LogError($"[{nameof(OtpInputBox)}] slots не назначены!");
            return;
        }

        int count = slots.Length;
        slotTexts = new TMP_Text[count];
        slotBackgrounds = new Image[count];
        slotOutlines = new Image[count];

        for (int i = 0; i < count; i++)
        {
            GameObject slot = slots[i];

            if (slot == null)
            {
                Debug.LogWarning($"[{nameof(OtpInputBox)}] slots[{i}] is null!");
                continue;
            }

            // Background — Image на руте слота
            slotBackgrounds[i] = slot.GetComponent<Image>();

            // Text — TMP_Text в детях
            slotTexts[i] = slot.GetComponentInChildren<TMP_Text>();

            // Outline — Image в детях (не на руте)
            slotOutlines[i] = FindChildImage(slot.transform);
        }
    }

    /// <summary>Находит Image в дочерних объектах (не на руте)</summary>
    private static Image FindChildImage(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Image img = parent.GetChild(i).GetComponent<Image>();

            if (img != null)
                return img;
        }

        return null;
    }

    private void ValidateReferences()
    {
        if (hiddenInput == null)
            Debug.LogError($"[{nameof(OtpInputBox)}] hiddenInput не назначен!");

        if (slotTexts == null || slotTexts.Length == 0)
            Debug.LogError($"[{nameof(OtpInputBox)}] slotTexts не инициализированы!");
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
        hiddenInput.onDeselect.AddListener(_ => Unfocus());

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
            {
                OtpCodeEntered?.Invoke(Code);
                Unfocus();
            }
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
        if (caretImage == null || CurrentState != State.Normal) return;

        caretBlinkTimer += Time.deltaTime * caretBlinkRate;

        // Мигание каретки: видима первую половину цикла
        bool visible = (caretBlinkTimer % 1f) < 0.5f;
        caretImage.enabled = visible;
    }

    private void RefreshVisuals(bool? forceFocused = null)
    {
        RenderDigits();
        RefreshSlotHighlight(forceFocused);
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

    private void RefreshSlotHighlight(bool? forceFocused = null)
    {
        if (slotBackgrounds == null) return;

        bool focused = forceFocused ?? (hiddenInput != null && hiddenInput.isFocused);

        // Активный слот — всегда после последней введённой цифры
        int activeSlot = Mathf.Clamp(Code.Length, 0, slotBackgrounds.Length - 1);

        // Цвет группы зависит от состояния
        Color groupColor = CurrentState switch
                           {
                               State.Success => successColor,
                               State.Failure => failureColor,
                               _ => normalColor,
                           };

        for (int i = 0; i < slotBackgrounds.Length; i++)
        {
            // Все ячейки одного цвета
            if (slotBackgrounds[i] != null)
                slotBackgrounds[i].color = groupColor;

            // Outline: только на активной ячейке при фокусе и в Normal состоянии
            if (slotOutlines != null && i < slotOutlines.Length && slotOutlines[i] != null)
            {
                bool showOutline = focused && i == activeSlot && CurrentState == State.Normal;
                slotOutlines[i].enabled = showOutline;
            }
        }

        // Позиционирование каретки
        UpdateCaretPosition(focused, activeSlot);
    }

    private void UpdateCaretPosition(bool focused, int activeSlot)
    {
        if (caretImage == null) return;

        bool showCaret = focused && CurrentState == State.Normal;

        if (!showCaret)
        {
            caretImage.enabled = false;
            return;
        }

        // Позиционируем каретку в центр активного слота
        if (slots != null && activeSlot < slots.Length && slots[activeSlot] != null)
        {
            RectTransform slotRect = slots[activeSlot].GetComponent<RectTransform>();

            if (slotRect != null && caretRectTransform != null)
                caretRectTransform.position = slotRect.position;
        }
    }

    /// <summary>Клик на компонент — активирует ввод только если не в фокусе</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        // Если уже в фокусе — ничего не делаем
        if (IsFocused) return;

        Focus();
    }

    /// <summary>Активирует поле ввода (очищает и ставит на первый слот)</summary>
    public void Focus()
    {
        // Если уже в фокусе — ничего не делаем
        if (IsFocused) return;

        FocusInternal();
    }

    private void FocusInternal()
    {
        if (hiddenInput == null) return;

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

        // Скрываем каретку и outline вместе
        HideCaretAndOutline();
    }

    /// <summary>Скрывает каретку и все outline (они всегда вместе)</summary>
    private void HideCaretAndOutline()
    {
        if (caretImage != null)
            caretImage.enabled = false;

        if (slotOutlines != null)
            foreach (Image outline in slotOutlines)
                if (outline != null)
                    outline.enabled = false;
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

        RefreshVisuals();
    }

    /// <summary>Очищает, сбрасывает состояние и фокусирует (принудительно, даже если уже в фокусе)</summary>
    public void ClearAndFocus() =>
        FocusInternal();

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
            OtpCodeEntered?.Invoke(Code);
    }

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

    /// <summary>Полный сброс: очищает код и возвращает в Normal состояние</summary>
    public void Reset()
    {
        CurrentState = State.Normal;
        Clear();
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
        // Автоподгонка массива slots в редакторе
        if (slots != null && slots.Length != codeLength)
            Array.Resize(ref slots, codeLength);
    }
#endif
}

