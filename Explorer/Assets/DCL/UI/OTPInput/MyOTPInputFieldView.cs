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

        [Header("CARET")]
        [SerializeField] private Image caretImage;
        [SerializeField] private float caretBlinkRate = 1.5f;

        private float caretBlinkTimer;
        private int codeLength;

        private void Awake()
        {
            codeLength = slots.Length;

            hiddenInput.characterLimit = codeLength;
            hiddenInput.characterValidation = TMP_InputField.CharacterValidation.Digit;
            hiddenInput.onFocusSelectAll = false;

            foreach (OTPSlotView slot in slots)
                slot.SetState(OTPSlotView.SlotState.UNSELECTED);

            caretImage.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (hiddenInput.isFocused)
            {
                hiddenInput.caretPosition = hiddenInput.text.Length; // keep always at the end

                if (caretImage.gameObject.activeSelf)
                {
                    caretBlinkTimer += Time.deltaTime * caretBlinkRate;
                    caretImage.enabled = caretBlinkTimer % 1f < 0.5f; // Carret blink:  visible first half of the cycle
                }
            }
        }

        private void OnEnable()
        {
            hiddenInput.onSelect.AddListener(OnSelected);
            hiddenInput.onValueChanged.AddListener(UpdateSlotsWithText);
            hiddenInput.onEndEdit.AddListener(OnEndEdit);
        }

        private void OnDisable()
        {
            hiddenInput.onSelect.RemoveAllListeners();
            hiddenInput.onValueChanged.RemoveAllListeners();
            hiddenInput.onEndEdit.RemoveAllListeners();
        }


        private void OnSelected(string text)
        {
            UpdateSlotsWithText(text);
            caretBlinkTimer = 0f;
        }

        private void OnEndEdit(string _)
        {
            // hiddenInput.DeactivateInputField(clearSelection: true);
            // hiddenInput.ReleaseSelection();
            caretImage.gameObject.SetActive(false);

            foreach (OTPSlotView slot in slots)
                slot.SetState(OTPSlotView.SlotState.UNSELECTED);
        }

        private void UpdateSlotsWithText(string text)
        {
            Debug.Log($"VVV InputChanged {text}");

            for (var i = 0; i < slots.Length; i++)
            {
                slots[i].SetState(OTPSlotView.SlotState.UNSELECTED);
                slots[i].Text = i < text.Length ? text[i].ToString() : string.Empty;
            }

            bool textIsFull = text.Length >= slots.Length;

            if (textIsFull)
            {
                hiddenInput.DeactivateInputField(clearSelection: true);
                hiddenInput.ReleaseSelection();
            }
            else
            {
                OTPSlotView activeSlot = slots[text.Length];
                activeSlot.SetState(OTPSlotView.SlotState.SELECTED);
                caretImage.rectTransform.position = activeSlot.Center;
            }

            caretImage.gameObject.SetActive(!textIsFull);
        }

        [ContextMenu(nameof(SetSuccess))]
        public void SetSuccess()
        {
            foreach (OTPSlotView slot in slots)
                slot.SetState(OTPSlotView.SlotState.SUCCESS);
        }

        [ContextMenu(nameof(SetFailure))]
        public void SetFailure()
        {
            foreach (OTPSlotView slot in slots)
                slot.SetState(OTPSlotView.SlotState.ERROR);
        }
    }
}
