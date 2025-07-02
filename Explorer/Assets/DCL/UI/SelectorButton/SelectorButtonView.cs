using DCL.UI.Utilities;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.SelectorButton
{
    public class SelectorButtonView : MonoBehaviour
    {
        public event Action<int>? OptionClicked;

        [SerializeField] private Button? selectorButton;
        [SerializeField] private TMP_Text? selectorButtonText;
        [SerializeField] private GameObject? selectorPanel;
        [SerializeField] private ScrollRect? selectorPanelScrollRect;
        [SerializeField] private Transform? selectorPanelParent;
        [SerializeField] private SelectorButtonOptionItemView? optionItemGameObject;
        [SerializeField] private Button? backgroundCloseButton;

        private readonly List<SelectorButtonOptionItemView> currentOptions = new ();

        private Transform? originalParent;
        private Vector2 originalLocalPosition;

        private void Awake()
        {
            originalParent = selectorPanel != null ? selectorPanel.transform.parent : null;
            originalLocalPosition = selectorPanel != null ? selectorPanel.transform.localPosition : Vector2.zero;
            selectorPanelScrollRect?.SetScrollSensitivityBasedOnPlatform();
        }

        private void OnEnable()
        {
            backgroundCloseButton?.onClick.AddListener(OnCloseOptionsPanel);
            selectorButton?.onClick.AddListener(OnOpenOptionsPanel);
            OnCloseOptionsPanel();
        }

        private void OnDisable()
        {
            backgroundCloseButton?.onClick.RemoveListener(OnCloseOptionsPanel);
            selectorButton?.onClick.RemoveListener(OnOpenOptionsPanel);
        }

        public void SetMainButtonText(string text)
        {
            if (selectorButtonText == null)
                return;

            selectorButtonText.text = text;
        }

        public void SetOptions(List<string> options)
        {
            ClearOptions();

            if (optionItemGameObject == null)
                return;

            for (var index = 0; index < options.Count; index++)
            {
                string option = options[index];
                SelectorButtonOptionItemView optionItem = Instantiate(optionItemGameObject, optionItemGameObject.transform.parent);
                optionItem.Setup(option);
                optionItem.transform.name = $"OptionItem_{index}";
                optionItem.gameObject.SetActive(true);
                optionItem.Clicked += OnOptionClicked;
                optionItem.VisibilityChanged += CheckSelectorButtonInteractivity;
                currentOptions.Add(optionItem);
            }
        }

        public SelectorButtonOptionItemView? GetOption(string option)
        {
            foreach (SelectorButtonOptionItemView optionItem in currentOptions)
            {
                if (optionItem != null && optionItem.OptionTitle == option)
                    return optionItem;
            }

            return null;
        }

        private void ClearOptions()
        {
            foreach (SelectorButtonOptionItemView optionItemView in currentOptions)
            {
                optionItemView.Clicked -= OnOptionClicked;
                optionItemView.VisibilityChanged -= CheckSelectorButtonInteractivity;
                Destroy(optionItemView.gameObject);
            }

            currentOptions.Clear();

            if (selectorButton != null)
                selectorButton.interactable = true;
        }

        private void OnOptionClicked(SelectorButtonOptionItemView optionItem)
        {
            int index = currentOptions.IndexOf(optionItem);
            if (index < 0)
                return;

            OptionClicked?.Invoke(index);
            OnCloseOptionsPanel();
        }

        private void CheckSelectorButtonInteractivity()
        {
            if (selectorButton == null)
                return;

            var allOptionsHidden = true;
            foreach (SelectorButtonOptionItemView option in currentOptions)
            {
                if (option.IsHidden)
                    continue;

                allOptionsHidden = false;
                break;
            }

            selectorButton.interactable = !allOptionsHidden;
        }

        private void OnOpenOptionsPanel()
        {
            selectorPanel?.SetActive(true);
            if (selectorPanelScrollRect != null)
                selectorPanelScrollRect.verticalNormalizedPosition = 1f;

            if (selectorPanelParent != null && selectorPanel != null)
            {
                selectorPanel.transform.parent = originalParent;
                selectorPanel.transform.localPosition = originalLocalPosition;
                selectorPanel.transform.parent = selectorPanelParent;
            }
        }

        private void OnCloseOptionsPanel() =>
            selectorPanel?.SetActive(false);
    }
}
