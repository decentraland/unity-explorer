using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class OtpCodeInput : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private TMP_InputField input;
    [SerializeField] private TMP_Text[] slotTexts; // N элементов
    [SerializeField] private Graphic[] slotBackgrounds; // N элементов (Image/Graphic)

    [Header("Behavior")]
    [SerializeField] private bool digitsOnly = true;
    [SerializeField] [Min(0f)] private float completeDebounceSeconds = 0.25f;

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
    private int lastCaret = -1;
    private bool lastFocus;

    private void Reset()
    {
        // На всякий случай для удобства в редакторе.
        input = GetComponentInChildren<TMP_InputField>();
    }

    private void Awake()
    {
        if (!input) throw new MissingReferenceException($"{nameof(OtpCodeInput)}: input is not assigned.");
        if (slotTexts == null || slotTexts.Length == 0) throw new MissingReferenceException($"{nameof(OtpCodeInput)}: slotTexts is empty.");

        if (slotBackgrounds == null || slotBackgrounds.Length != slotTexts.Length)
            Debug.LogWarning($"{nameof(OtpCodeInput)}: slotBackgrounds should match slotTexts length (N).");

        input.characterLimit = N;

        // На PC можно просто фильтровать в onValueChanged (и это корректно для paste).
        // Но дополнительно можно ограничить ввод символов:
        input.onValidateInput += ValidateChar; // Delegate описан в TMP_InputField.OnValidateInput :contentReference[oaicite:4]{index=4}

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

    public void ClearAndFocus()
    {
        CancelDebounce();
        input.SetTextWithoutNotify(""); // SetTextWithoutNotify(): без onValueChanged :contentReference[oaicite:5]{index=5}
        input.caretPosition = 0; // caretPosition property :contentReference[oaicite:6]{index=6}
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

        // Если слот заполнен — выделяем 1 символ, чтобы следующий ввод его заменил (overwrite UX).
        if (index < len)
        {
            // selectionStringAnchorPosition / selectionStringFocusPosition :contentReference[oaicite:7]{index=7}
            input.selectionStringAnchorPosition = index;
            input.selectionStringFocusPosition = index + 1;
        }
        else
        {
            input.selectionStringAnchorPosition = index;
            input.selectionStringFocusPosition = index;
        }

        input.caretPosition = Mathf.Clamp(index, 0, len);
        RefreshHighlightOnly();
    }

    private void Update()
    {
        // Важно: каретка меняется стрелками/мышью без onValueChanged — подсветку нужно обновлять в Update.
        bool focused = input.isFocused;
        int caret = input.caretPosition;

        if (focused != lastFocus || (focused && caret != lastCaret))
        {
            lastFocus = focused;
            lastCaret = caret;
            RefreshHighlightOnly();
        }
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
            // Подправили — применим без повторного onValueChanged :contentReference[oaicite:8]{index=8}
            int caret = Mathf.Min(input.caretPosition, sanitized.Length);
            input.SetTextWithoutNotify(sanitized);
            input.caretPosition = caret;
        }

        RenderSlots(sanitized);
        RefreshHighlightOnly();

        OnChanged?.Invoke(sanitized);

        if (sanitized.Length == N)
            StartDebounce(sanitized);
        else
            CancelDebounce();
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

        // Активный слот: позиция каретки, но в пределах [0..N-1]
        int active = Mathf.Clamp(input.caretPosition, 0, N - 1);

        // Если не в фокусе — подсветку убираем.
        if (!input.isFocused)
        {
            for (var i = 0; i < N; i++)
                slotBackgrounds[i].color = i < len ? filledColor : normalColor;

            return;
        }

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

        // Не спамим одинаковым кодом
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
