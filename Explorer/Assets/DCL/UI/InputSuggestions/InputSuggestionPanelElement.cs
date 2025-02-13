using MVC;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace DCL.UI.SuggestionPanel
{
    public class InputSuggestionPanelElement : MonoBehaviour, IViewWithGlobalDependencies
    {
        public event SuggestionSelectedDelegate SuggestionSelectedEvent;

        [Header("Panel Configuration")]
        [SerializeField] private SuggestionPanelConfigurationSO configurationSo;

        [Header("References")]
        [field: SerializeField] private Transform suggestionContainer;
        [field: SerializeField] private ScrollRect scrollViewComponent;
        [field: SerializeField] private GameObject noResultsIndicator;
        [field: SerializeField] public RectTransform ScrollViewRect { get; private set; }

        public bool IsActive { get; private set; }

        private readonly Dictionary<InputSuggestionType, ObjectPool<BaseInputSuggestionElement>> suggestionItemsPools = new ();
        private readonly Dictionary<InputSuggestionType, int> maxSuggestionsPerType = new ();
        private readonly List<BaseInputSuggestionElement> usedPoolItems = new ();

        private InputSuggestionType currentSuggestionType;
        private int currentIndex = 0;
        private BaseInputSuggestionElement lastSelectedInputSuggestion;
        private ViewDependencies viewDependencies;

        private void Awake()
        {
            CreateSuggestionPools();
        }

        public void InjectDependencies(ViewDependencies dependencies)
        {
            viewDependencies = dependencies;
        }

        private void CreateSuggestionPools()
        {
            foreach (BaseInputSuggestionElement suggestionElement in configurationSo.SuggestionElements)
            {
                var suggestionPool = new ObjectPool<BaseInputSuggestionElement>(
                    () => CreatePoolElement(suggestionElement),
                    element => { element.OnGet(); },
                    element => { element.OnReleased(); },
                    defaultCapacity: 10
                );

                suggestionItemsPools.Add(suggestionElement.GetSuggestionType(), suggestionPool);
                maxSuggestionsPerType.Add(suggestionElement.GetSuggestionType(), suggestionElement.MaxSuggestionAmount);
            }
        }

        private BaseInputSuggestionElement CreatePoolElement(BaseInputSuggestionElement prefab)
        {
            BaseInputSuggestionElement inputSuggestionElement = Instantiate(prefab, suggestionContainer);
            inputSuggestionElement.SuggestionSelectedEvent += OnSuggestionSelected;
            return inputSuggestionElement;
        }

        private void OnSuggestionSelected(string suggestionId)
        {
            SuggestionSelectedEvent?.Invoke(suggestionId);
            SetPanelVisibility(false);
        }

        private void OnSubmit(InputAction.CallbackContext obj)
        {
            if (lastSelectedInputSuggestion != null && IsActive)
                SuggestionSelectedEvent?.Invoke(lastSelectedInputSuggestion.SuggestionId);
            else
                SetPanelVisibility(false);
        }

        private void OnArrowUp(InputAction.CallbackContext obj)
        {
            if (currentIndex > 0)
                SetSelection(currentIndex - 1);
            else
                SetSelection(usedPoolItems.Count - 1);
        }

        private void OnArrowDown(InputAction.CallbackContext obj)
        {
            if (currentIndex < usedPoolItems.Count - 1)
                SetSelection(currentIndex + 1);
            else
                SetSelection(0);
        }

        public void SetSuggestionValues(InputSuggestionType suggestionType, IList<IInputSuggestionElementData> foundSuggestions)
        {
            noResultsIndicator.gameObject.SetActive(foundSuggestions.Count == 0);

            if (suggestionType == InputSuggestionType.NONE) return;

            int maxSuggestions = maxSuggestionsPerType[suggestionType];

            scrollViewComponent.vertical = foundSuggestions.Count > maxSuggestions;

            float scrollViewHeight = configurationSo.maxHeight;

            if (foundSuggestions.Count <= 1)
                scrollViewHeight = configurationSo.minHeight;
            else if (foundSuggestions.Count <= maxSuggestions)
                scrollViewHeight = (configurationSo.entryHeight * foundSuggestions.Count) + configurationSo.padding;

            ScrollViewRect.sizeDelta = new Vector2(ScrollViewRect.sizeDelta.x, scrollViewHeight);

            //if the suggestion type is different, we release all items from the pool,
            //otherwise, we only release the elements that are over the found suggestion amount.
            if (currentSuggestionType != InputSuggestionType.NONE)
            {
                if (currentSuggestionType != suggestionType)
                {
                    for (var i = 0; i < usedPoolItems.Count; i++)
                        suggestionItemsPools[currentSuggestionType].Release(usedPoolItems[i]);

                    usedPoolItems.Clear();
                }
                else
                {
                    for (int i = foundSuggestions.Count; i < usedPoolItems.Count; i++)
                        suggestionItemsPools[currentSuggestionType].Release(usedPoolItems[i]);

                    for (int i = usedPoolItems.Count - 1; i >= foundSuggestions.Count; i--)
                        usedPoolItems.RemoveAt(i);
                }
            }

            currentSuggestionType = suggestionType;

                for (var i = 0; i < foundSuggestions.Count; i++)
                {
                    IInputSuggestionElementData elementData = foundSuggestions[i];

                    if (usedPoolItems.Count > i)
                    {
                        usedPoolItems[i].Setup(elementData);
                        usedPoolItems[i].gameObject.transform.SetAsLastSibling();
                    }
                    else
                    {
                        BaseInputSuggestionElement suggestionElement = suggestionItemsPools[suggestionType].Get();
                        suggestionElement.Setup(elementData);
                        suggestionElement.gameObject.transform.SetAsLastSibling();
                        usedPoolItems.Add(suggestionElement);
                    }

            }

            if (usedPoolItems.Count > 0)
                SetSelection(0);
            else
                lastSelectedInputSuggestion = null;
        }

        private void SetSelection(int index)
        {
            if (!gameObject.activeInHierarchy)
                return;

            if (lastSelectedInputSuggestion != null)
                lastSelectedInputSuggestion.SetSelectionState(false);

            currentIndex = index;
            usedPoolItems[index].SetSelectionState(true);
            lastSelectedInputSuggestion = usedPoolItems[index];
        }

        public void SetPanelVisibility(bool isVisible)
        {
            if (isVisible)
            {
                viewDependencies.DclInput.UI.ActionUp.performed += OnArrowUp;
                viewDependencies.DclInput.UI.ActionDown.performed += OnArrowDown;
                viewDependencies.DclInput.UI.Submit.performed += OnSubmit;
            }
            else
            {
                viewDependencies.DclInput.UI.ActionUp.performed -= OnArrowUp;
                viewDependencies.DclInput.UI.ActionDown.performed -= OnArrowDown;
                viewDependencies.DclInput.UI.Submit.performed -= OnSubmit;
            }

            IsActive = isVisible;
            gameObject.SetActive(isVisible);
        }
    }
}
