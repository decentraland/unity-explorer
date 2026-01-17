# OTP Input Field for Unity (PC Standalone)

Production-ready OTP/PIN input component for Unity 6.x with uGUI + TextMeshPro.
Implements "banking app" UX standards for Windows/Mac standalone builds.

## Features

- **Hidden Input + Visual Slots** — Native clipboard paste (Ctrl+V / Cmd+V) works perfectly
- **Auto-advance cursor** — Jumps to next slot after each digit
- **Overwrite mode** — Typing on filled slot replaces the digit (no insert mode)
- **Single-char selection only** — Mouse drag cannot select ranges > 1 character
- **Active slot highlighting** — Visual feedback for current position
- **Backspace handling** — Deletes current/previous digit with proper cursor movement
- **Error state + shake animation** — For wrong code feedback
- **Event-driven** — `OnCodeComplete`, `OnCodeChanged`, `OnCodeCleared`

## Quick Setup

### Option A: Automatic (Recommended)

1. Copy `OTPInputField.cs` to `Assets/Scripts/`
2. Copy `Editor/OTPInputFieldCreator.cs` to `Assets/Editor/`
3. In Unity: **GameObject → UI → OTP Input Field (6 digits)**
4. Done! Configure colors in Inspector.

### Option B: Manual Setup

1. Create a parent GameObject with `OTPInputField` component
2. Add child structure:

```
OTPInputField (with HorizontalLayoutGroup)
├── Slot_0 (Image + child TMP_Text "Digit")
├── Slot_1
├── Slot_2
├── Slot_3
├── Slot_4
├── Slot_5
└── HiddenInput (TMP_InputField, stretched to fill parent)
    └── Text Area (RectMask2D)
        └── Text (TMP_Text, color = clear)
```

3. Assign references in Inspector:
   - `hiddenInput` → the TMP_InputField
   - `slotTexts[]` → the 6 TMP_Text "Digit" components
   - `slotBackgrounds[]` → the 6 slot Image components

## Inspector Settings

| Property | Description |
|----------|-------------|
| `codeLength` | Number of digits (default: 6) |
| `numericOnly` | Allow only 0-9 (default: true) |
| `normalSlotColor` | Empty, unfocused slot color |
| `activeSlotColor` | Currently focused slot color |
| `filledSlotColor` | Filled slot color |
| `errorSlotColor` | Error state color |
| `emptySlotChar` | Placeholder in empty slots ("_", "•", or empty) |
| `maskChar` | Hide digits with this char (empty = show digits) |

## Usage Example

```csharp
public class MyAuthScreen : MonoBehaviour
{
    [SerializeField] private OTPInputField otpInput;

    void Start()
    {
        otpInput.OnCodeComplete += OnCodeEntered;
        otpInput.Focus(); // Auto-focus on start
    }

    void OnCodeEntered(string code)
    {
        Debug.Log($"Verifying code: {code}");
        StartCoroutine(VerifyWithServer(code));
    }

    IEnumerator VerifyWithServer(string code)
    {
        // Your API call here
        bool success = yield return MyAPI.VerifyOTP(code);
        
        if (!success)
        {
            otpInput.ShowError();
            otpInput.PlayShakeAnimation();
            otpInput.ClearCode();
            otpInput.Focus();
        }
    }
}
```

## API Reference

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Code` | `string` | Current entered code |
| `IsComplete` | `bool` | True when all digits entered |
| `IsFocused` | `bool` | True when input is active |

### Methods

| Method | Description |
|--------|-------------|
| `Focus()` | Activate input and show keyboard focus |
| `Unfocus()` | Deactivate input |
| `ClearCode()` | Clear all entered digits |
| `SetCode(string)` | Programmatically set the code (e.g., autofill) |
| `ShowError(float duration)` | Flash error color on all slots |
| `ClearError()` | Reset slot colors to normal |
| `PlayShakeAnimation(float intensity, float duration)` | Shake animation for error feedback |

### Events

| Event | Signature | When |
|-------|-----------|------|
| `OnCodeChanged` | `Action<string>` | Any digit added/removed |
| `OnCodeComplete` | `Action<string>` | All digits entered (trigger auto-verify here!) |
| `OnCodeCleared` | `Action` | Code was cleared |

## Architecture Notes

### Why Hidden Input + Visual Slots?

The alternative (6 separate TMP_InputFields with manual focus switching) has problems:
- Paste doesn't work natively (need to intercept and distribute)
- Focus switching can be buggy on different platforms
- Tab/Shift+Tab navigation is unpredictable
- More complex selection handling

The hidden input approach:
- Single point of text entry = native paste works
- No focus switching = no edge cases
- Selection clamping is simpler
- Easier to maintain

### Selection Clamping

In `EnforceOTPBehavior()`, we check every frame if selection > 1 character and collapse it.
This prevents mouse-drag selecting multiple characters while still allowing:
- Single character selection (for overwrite)
- Caret positioning
- Keyboard navigation (arrows, home, end)

### Overwrite Mode

TMP_InputField doesn't have native overwrite mode, but we achieve it by:
1. Allowing typing normally (which inserts)
2. Immediately truncating to `codeLength` in `ProcessInputChange()`
3. Moving caret to end of current text

This means if you type on a full 6-digit code, the last digit gets replaced.

## Customization Ideas

### Rounded Slot Corners
Create a 9-sliced sprite with rounded corners and assign to slot Images.

### Animated Focus
Add DOTween/LeanTween for smooth color transitions:
```csharp
slotBackgrounds[i].DOColor(activeSlotColor, 0.15f);
```

### Cursor Blink
Add a blinking caret Image inside each slot, toggle visibility based on `isActive`.

### Haptic Feedback (if supporting controllers)
```csharp
private void OnCodeChanged(string code)
{
    // Gamepad rumble on each digit
    Gamepad.current?.SetMotorSpeeds(0.1f, 0.1f);
}
```

## Troubleshooting

**Q: Paste doesn't work**  
A: Ensure `hiddenInput` is assigned and the TMP_InputField is receiving focus. Check that another UI element isn't stealing focus.

**Q: Can't click to focus**  
A: The hidden input's Image must have `raycastTarget = true` and be positioned over the slots.

**Q: Digits not showing**  
A: Check that `slotTexts[]` array is properly assigned in Inspector.

**Q: Selection still allows ranges**  
A: Ensure `Update()` is running (component enabled, GameObject active).

## License

MIT — Use freely in commercial and personal projects.

---

Made for Unity 6.x | uGUI + TextMeshPro | PC Standalone (Win/Mac)
