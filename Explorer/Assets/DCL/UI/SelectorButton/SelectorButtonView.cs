using DCL.UI.Utilities;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace DCL.UI.SelectorButton
{
    public class SelectorButtonView : MonoBehaviour
    {
        public event Action<int>? OptionClicked;

        [SerializeField] private Button selectorButton;
        [SerializeField] private TMP_Text selectorButtonText;
        [SerializeField] private RectTransform selectorPanel;
        [SerializeField] private RectTransform selectorPanelContentContainer;
        [SerializeField] private float selectorPanelMaxHeight = 150f;
        [SerializeField] private ScrollRect selectorPanelScrollRect;
        [SerializeField] private Transform selectorPanelParent;
        [SerializeField] private SelectorButtonOptionItemView optionItemGameObject;
        [SerializeField] private Button backgroundCloseButton;
        [SerializeField] private int defaultPoolCapacity = 10;

        private IObjectPool<SelectorButtonOptionItemView> optionsPool;
        private readonly List<SelectorButtonOptionItemView> currentOptions = new ();

        private Transform? originalParent;
        private Vector2 originalLocalPosition;

        private void Awake()
        {
            originalParent = selectorPanel.parent;
            originalLocalPosition = selectorPanel.localPosition;
            selectorPanelScrollRect.SetScrollSensitivityBasedOnPlatform();

            optionsPool = new ObjectPool<SelectorButtonOptionItemView>(
                InstantiateOptionItem,
                defaultCapacity: defaultPoolCapacity,
                actionOnGet: optionItemView =>
                {
                    optionItemView.gameObject.SetActive(true);
                    optionItemView.Clicked += OnOptionClicked;
                    optionItemView.VisibilityChanged += CheckSelectorButtonInteractivity;
                },
                actionOnRelease: equippedItemView =>
                {
                    equippedItemView.gameObject.SetActive(false);
                    equippedItemView.Clicked -= OnOptionClicked;
                    equippedItemView.VisibilityChanged -= CheckSelectorButtonInteractivity;
                });
        }

        private void OnEnable()
        {
            backgroundCloseButton.onClick.AddListener(OnCloseOptionsPanel);
            selectorButton.onClick.AddListener(OnOpenOptionsPanel);
            OnCloseOptionsPanel();
        }

        private void OnDisable()
        {
            backgroundCloseButton.onClick.RemoveListener(OnCloseOptionsPanel);
            selectorButton.onClick.RemoveListener(OnOpenOptionsPanel);
        }

        public void SetMainButtonText(string text) =>
            selectorButtonText.text = text;

        private SelectorButtonOptionItemView InstantiateOptionItem()
        {
            SelectorButtonOptionItemView optionItemView = Instantiate(optionItemGameObject, optionItemGameObject.transform.parent);
            return optionItemView;
        }

        public void SetOptions(List<string> options)
        {
            ClearOptions();

            if (optionItemGameObject == null)
                return;

            for (var index = 0; index < options.Count; index++)
            {
                string option = options[index];
                SelectorButtonOptionItemView optionItem = optionsPool.Get();
                optionItem.Setup(option);
                optionItem.transform.name = $"OptionItem_{index}";
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
                optionsPool.Release(optionItemView);

            currentOptions.Clear();
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
            selectorPanel.gameObject.SetActive(true);
            selectorPanelScrollRect.verticalNormalizedPosition = 1f;
            RefreshSelectorPanelSize();

            if (selectorPanelParent != null)
            {
                selectorPanel.parent = originalParent;
                selectorPanel.localPosition = originalLocalPosition;
                selectorPanel.parent = selectorPanelParent;
            }
        }

        private void OnCloseOptionsPanel() =>
            selectorPanel.gameObject.SetActive(false);

        private void RefreshSelectorPanelSize()
        {
            float contentHeight = selectorPanelContentContainer.rect.height;
            float maxHeight = Mathf.Min(contentHeight, selectorPanelMaxHeight);
            selectorPanel.sizeDelta = new Vector2(selectorPanel.sizeDelta.x, maxHeight);
        }
    }
}
