using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.OTPInput
{
    public class MyOTPInputFieldView : MonoBehaviour
    {
        [SerializeField] private TMP_InputField hiddenInput;
        [SerializeField] private OTPSlotView[] slots;
        [SerializeField] private Image caretImage;

        [Header("SETTINGS")]
        [SerializeField] private int codeLength = 6;
        [SerializeField] private float caretBlinkRate = 1.5f;

        private float caretBlinkTimer;

        private void Awake()
        {
            hiddenInput.characterLimit = codeLength;
            hiddenInput.characterValidation = TMP_InputField.CharacterValidation.Digit;

            foreach (OTPSlotView slot in slots)
                slot.SetSlotState(OTPSlotView.SlotState.UNSELECTED);

            caretImage.enabled = false;
        }

        private void Update()
        {
            if (hiddenInput.isFocused)
            {
                caretBlinkTimer += Time.deltaTime * caretBlinkRate;
                caretImage.enabled = caretBlinkTimer % 1f < 0.5f; // Carret blink:  visible first half of the cycle
            }
        }

        private void OnEnable()
        {
            hiddenInput.onSelect.AddListener(OnSelected);
        }

        private void OnDisable()
        {
            hiddenInput.onSelect.RemoveAllListeners();
        }

        private int activeSlotIndex = -1;

        private void OnSelected(string text)
        {
            Debug.Log($"VVV {text}");
            hiddenInput.text = string.Empty;

            // hiddenInput.SetTextWithoutNotify(filtered);

            slots[0].SetSlotState(OTPSlotView.SlotState.SELECTED);
            caretImage.enabled = true;

            // hiddenInput.text;
        }
    }
}
