using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class OtpCodeInput2 : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private TMP_InputField input;
    [SerializeField] private TMP_Text[] slotTexts; // N элементов
    [SerializeField] private Graphic[] slotBackgrounds; // N элементов (Image/Graphic)

    [Header("Behavior")]
    [SerializeField] private bool digitsOnly = true;
    [SerializeField] [Min(0f)] private float completeDebounceSeconds = 0.25f;

    [Header("PC UX policy")]
    [SerializeField] private bool preventRangeSelection = true;
    [SerializeField] private bool overwriteWhenCaretInsideText = true;

    [Header("Visuals")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color activeColor = new (1f, 1f, 1f, 0.85f);
    [SerializeField] private Color filledColor = new (1f, 1f, 1f, 0.95f);

    [Serializable] public sealed class StringEvent : UnityEvent<string> { }
    public StringEvent OnChanged;
    public StringEvent OnCompletedDebounced;

    private int N => slotTexts?.Length ?? 0;

    private Coroutine debounceRoutine;
    private string lastCompleted = "";

    private void Awake()
    {
        if (!input) throw new MissingReferenceException($"{nameof(OtpCodeInput)}: input is not assigned.");
        if (slotTexts == null || slotTexts.Length == 0) throw new MissingReferenceException($"{nameof(OtpCodeInput)}: slotTexts is empty.");

        if (slotBackgrounds == null || slotBackgrounds.Length != slotTexts.Length)
            Debug.LogWarning($"{nameof(OtpCodeInput)}: slotBackgrounds should match slotTexts length (N).");

        input.characterLimit = N;

        // По желанию: можно и так ограничить (но всё равно санитайзим ниже).
        // onValidateInput — нативная точка валидации символа :contentReference[oaicite:2]{index=2}
        input.onValidateInput += ValidateChar;

        input.onValueChanged.AddListener(HandleChanged);
        input.onSelect.AddListener(_ => RefreshAll());
        input.onDeselect.AddListener(_ => RefreshAll());

        RefreshAll();
    }

    private char ValidateChar(string text, int charIndex, char addedChar)
    {
        if (!digitsOnly) return addedChar;
        return char.IsDigit(addedChar) ? addedChar : '\0';
    }

    private void Update()
    {
        if (input.isFocused)
        {
            EnforcePcSelectionPolicy();
            RefreshHighlightOnly();
        }
    }

    public void ClearAndFocus()
    {
        CancelDebounce();

        // SetTextWithoutNotify — не триггерит onValueChanged :contentReference[oaicite:3]{index=3}
        input.SetTextWithoutNotify("");
        input.caretPosition = 0; // caretPosition: фокусная позиция каретки :contentReference[oaicite:4]{index=4}
        input.ActivateInputField();

        lastCompleted = "";
        RefreshAll();
    }

    public void FocusSlot(int index)
    {
        index = Mathf.Clamp(index, 0, N);
        input.ActivateInputField();

        string t = input.text ?? "";
        int len = t.Length;
        int i = Mathf.Clamp(index, 0, len);

        // Если слот заполнен — выделяем ровно 1 символ (overwrite при следующем вводе).
        if (i < len)
        {
            // Важно: делаем focus = i, anchor = i+1 — тогда caretPosition остаётся i. :contentReference[oaicite:5]{index=5}
            input.selectionStringFocusPosition = i;
            input.selectionStringAnchorPosition = i + 1;
        }
        else
        {
            input.selectionStringFocusPosition = i;
            input.selectionStringAnchorPosition = i;
        }

        input.caretPosition = i;
        RefreshHighlightOnly();
    }

    private void HandleChanged(string raw)
    {
        raw ??= "";

        string sanitized = raw;

        if (digitsOnly)
            sanitized = new string(sanitized.Where(char.IsDigit).ToArray());

        if (sanitized.Length > N)
            sanitized = sanitized.Substring(0, N);

        if (!string.Equals(sanitized, raw, StringComparison.Ordinal))
        {
            int caret = Mathf.Clamp(input.caretPosition, 0, sanitized.Length);
            input.SetTextWithoutNotify(sanitized); // :contentReference[oaicite:6]{index=6}
            input.caretPosition = caret;
        }

        RenderSlots(sanitized);
        OnChanged?.Invoke(sanitized);

        if (sanitized.Length == N) StartDebounce(sanitized);
        else CancelDebounce();
    }

    /// <summary>
    ///     PC-политика: запрет выделения диапазона > 1 и авто-overwrite внутри текста.
    ///     Работает для мыши/drag, Ctrl+A, Shift+стрелок и т.п.
    /// </summary>
    private void EnforcePcSelectionPolicy()
    {
        string t = input.text ?? "";
        int len = t.Length;

        // caretPosition — это "focus position" даже при выделении :contentReference[oaicite:7]{index=7}
        int caret = Mathf.Clamp(input.caretPosition, 0, len);

        int a = input.selectionStringAnchorPosition; // :contentReference[oaicite:8]{index=8}
        int f = input.selectionStringFocusPosition; // :contentReference[oaicite:9]{index=9}

        int selStart = Mathf.Min(a, f);
        int selEnd = Mathf.Max(a, f);
        int selLen = selEnd - selStart;

        if (preventRangeSelection && selLen > 1)
        {
            // Схлопываем любое "длинное" выделение.
            input.selectionStringFocusPosition = caret;
            input.selectionStringAnchorPosition = caret;
            a = caret;
            f = caret;
            selLen = 0;
        }

        if (overwriteWhenCaretInsideText && caret < len)
        {
            // Насильно держим выделенным 1 символ "под кареткой".
            // Делаем focus = caret, anchor = caret+1 => caretPosition остаётся caret.
            input.selectionStringFocusPosition = caret;
            input.selectionStringAnchorPosition = caret + 1;
        }
        else
        {
            // В конце строки/на пустом — без выделения.
            input.selectionStringFocusPosition = caret;
            input.selectionStringAnchorPosition = caret;
        }
    }

    private void RenderSlots(string code)
    {
        for (var i = 0; i < N; i++)
            slotTexts[i].text = i < code.Length ? code[i].ToString() : "";

        if (slotBackgrounds == null || slotBackgrounds.Length != N) return;

        for (var i = 0; i < N; i++)
            slotBackgrounds[i].color = i < code.Length ? filledColor : normalColor;
    }

    private void RefreshHighlightOnly()
    {
        if (slotBackgrounds == null || slotBackgrounds.Length != N) return;

        string code = input.text ?? "";
        int len = code.Length;

        if (!input.isFocused)
        {
            for (var i = 0; i < N; i++)
                slotBackgrounds[i].color = i < len ? filledColor : normalColor;

            return;
        }

        // Мы держим caretPosition = индекс активного символа даже при выделении (focus=i, anchor=i+1),
        // поэтому подсветка просто по caretPosition.
        int active = Mathf.Clamp(input.caretPosition, 0, N - 1);

        for (var i = 0; i < N; i++)
        {
            if (i == active) slotBackgrounds[i].color = activeColor;
            else slotBackgrounds[i].color = i < len ? filledColor : normalColor;
        }
    }

    private void RefreshAll()
    {
        RenderSlots(input.text ?? "");
        RefreshHighlightOnly();
    }

    private void StartDebounce(string code)
    {
        CancelDebounce();
        debounceRoutine = StartCoroutine(DebounceCoroutine(code));
    }

    private IEnumerator DebounceCoroutine(string code)
    {
        yield return new WaitForSeconds(completeDebounceSeconds);

        string current = input.text ?? "";
        if (current.Length != N) yield break;
        if (!string.Equals(current, code, StringComparison.Ordinal)) yield break;

        if (string.Equals(lastCompleted, code, StringComparison.Ordinal)) yield break;
        lastCompleted = code;

        OnCompletedDebounced?.Invoke(code);
    }

    private void CancelDebounce()
    {
        if (debounceRoutine != null)
        {
            StopCoroutine(debounceRoutine);
            debounceRoutine = null;
        }
    }
}
