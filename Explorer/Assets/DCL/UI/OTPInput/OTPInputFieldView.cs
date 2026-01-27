using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.OTPInput
{
    public class OTPInputFieldView : MonoBehaviour
    {
        [SerializeField] private TMP_InputField hiddenInput;
        [SerializeField] private GameObject slotsParent;
        [SerializeField] private OTPSlotView[] slots;

        [Header("CARET")]
        [SerializeField] private Image caretImage;
        [SerializeField] private float caretBlinkRate = 1.5f;

        [Header("RESULT")]
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private GameObject resultSuccessIcon;
        [SerializeField] private GameObject resultErrorIcon;

        public event Action<string>? CodeEntered;

        private float caretBlinkTimer;
        private int codeLength;
        private bool prevIsFocused;
        private Coroutine? clearAndFocusCoroutine;

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
                int textLength = hiddenInput.text.Length;
                hiddenInput.caretPosition = textLength; // keep always at the end

                if (caretImage.gameObject.activeSelf)
                {
                    caretBlinkTimer += Time.deltaTime * caretBlinkRate;
                    caretImage.enabled = caretBlinkTimer % 1f < 0.5f; // Carret blink:  visible first half of the cycle
                }

                // set slot selected with caret disabled
                if (prevIsFocused == false && textLength >= slots.Length)
                    slots[textLength - 1].SetState(OTPSlotView.SlotState.SELECTED);
            }

            prevIsFocused = hiddenInput.isFocused;
        }

        private void OnEnable()
        {
            resultText.gameObject.SetActive(false);

            hiddenInput.onSelect.AddListener(OnSelected);
            hiddenInput.onValueChanged.AddListener(UpdateSlotsWithText);
            hiddenInput.onEndEdit.AddListener(UnselectAll);
        }

        private void OnDisable()
        {
            StopClearAndFocusCoroutine();
            Clear();

            hiddenInput.onSelect.RemoveAllListeners();
            hiddenInput.onValueChanged.RemoveAllListeners();
            hiddenInput.onEndEdit.RemoveAllListeners();
        }

        private void OnSelected(string text)
        {
            UpdateSlotsWithText(text);
            caretBlinkTimer = 0f;
        }

        private void UnselectAll(string _)
        {
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
                CodeEntered?.Invoke(hiddenInput.text);
                Debug.Log($"VVV CodeEntered {hiddenInput.text}");
            }
            else
            {
                OTPSlotView activeSlot = slots[text.Length];
                activeSlot.SetState(OTPSlotView.SlotState.SELECTED);
                caretImage.rectTransform.position = activeSlot.Center;
            }

            caretImage.gameObject.SetActive(!textIsFull);
        }

        [ContextMenu(nameof(Clear))]
        public void Clear()
        {
            StopClearAndFocusCoroutine();
            resultText.gameObject.SetActive(false);

            hiddenInput.DeactivateInputField(clearSelection: true);
            hiddenInput.text = string.Empty;
            UnselectAll(string.Empty);
        }

        private void ClearAndFocus()
        {
            StopClearAndFocusCoroutine();
            resultText.gameObject.SetActive(false);

            hiddenInput.interactable = true;
            hiddenInput.SetTextWithoutNotify(string.Empty);
            UpdateSlotsWithText(string.Empty);

            caretBlinkTimer = 0f;
            prevIsFocused = false;

            hiddenInput.ActivateInputField();
            hiddenInput.Select();
        }

        private IEnumerator ClearAndFocusAfterDelay(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            ClearAndFocus();
            clearAndFocusCoroutine = null;
        }

        private void StopClearAndFocusCoroutine()
        {
            if (clearAndFocusCoroutine == null) return;
            StopCoroutine(clearAndFocusCoroutine);
            clearAndFocusCoroutine = null;
        }

        [ContextMenu(nameof(SetSuccess))]
        public void SetSuccess()
        {
            hiddenInput.interactable = false;

            foreach (OTPSlotView slot in slots)
                slot.SetState(OTPSlotView.SlotState.SUCCESS);

            resultText.gameObject.SetActive(true);
            resultText.text = "Success";
            resultSuccessIcon.SetActive(true);
            resultErrorIcon.SetActive(false);
        }

        [ContextMenu(nameof(SetFailure))]
        public void SetFailure()
        {
            hiddenInput.interactable = false;

            foreach (OTPSlotView slot in slots)
                slot.SetState(OTPSlotView.SlotState.ERROR);

            ShakeAnimation();

            resultText.gameObject.SetActive(true);
            resultText.text = "Incorrect code";
            resultSuccessIcon.SetActive(false);
            resultErrorIcon.SetActive(true);

            // After showing error feedback, clear the field and refocus it so user can retry
            StopClearAndFocusCoroutine();
            clearAndFocusCoroutine = StartCoroutine(ClearAndFocusAfterDelay(1.5f));
        }

        private void ShakeAnimation()
        {
            // throw new NotImplementedException();
        }
    }
}
