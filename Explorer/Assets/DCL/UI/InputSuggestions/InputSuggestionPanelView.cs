using MVC;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace DCL.UI.SuggestionPanel
{
    public class InputSuggestionPanelView : MonoBehaviour
    {
        public event SuggestionSelectedDelegate SuggestionSelected;

        [Header("Panel Configuration")]
        [SerializeField] private SuggestionPanelConfigurationSO configurationSo;

        [Header("References")]
        [field: SerializeField] private Transform suggestionContainer;
        [field: SerializeField] private ScrollRect scrollViewComponent;
        [field: SerializeField] private GameObject noResultsIndicator;
        [field: SerializeField] public RectTransform ScrollViewRect { get; private set; }

        public bool IsActive { get; private set; }

        private readonly Dictionary<InputSuggestionType, ObjectPool<BaseInputSuggestionElement>> suggestionItemsPools = new ();
        private readonly Dictionary<InputSuggestionType, SuggestionElementData> suggestionDataPerType = new ();

        private readonly List<BaseInputSuggestionElement> usedPoolItems = new ();

        private InputSuggestionType currentSuggestionType;
        private int currentIndex;
        private BaseInputSuggestionElement lastSelectedInputSuggestion;
        private int firstVisibleSuggestionIndex;
        private int lastVisibleSuggestionIndex;
        private int visibleSuggestionsCount;
        private bool needToScroll;

        public void InjectDependencies()
        {
            CreateSuggestionPools();
        }

        private void CreateSuggestionPools()
        {
            // Skip if already initialized
            if(suggestionItemsPools.Count != 0)
                return;

            foreach (BaseInputSuggestionElement suggestionElement in configurationSo.SuggestionElements)
            {
                var suggestionPool = new ObjectPool<BaseInputSuggestionElement>(
                    () => CreatePoolElement(suggestionElement),
                    element => { element.OnGet(); },
                    element => { element.OnReleased(); },
                    defaultCapacity: 10
                );

                suggestionItemsPools.Add(suggestionElement.GetSuggestionType(), suggestionPool);
                suggestionDataPerType.Add(suggestionElement.GetSuggestionType(), suggestionElement.SuggestionElementData);
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
            SuggestionSelected?.Invoke(suggestionId);
            SetPanelVisibility(false);
        }

        private void OnSubmit(InputAction.CallbackContext obj)
        {
            if (lastSelectedInputSuggestion != null && IsActive)
                SuggestionSelected?.Invoke(lastSelectedInputSuggestion.SuggestionId);

            SetPanelVisibility(false);
        }

        private void OnArrowDown(InputAction.CallbackContext obj)
        {
            if (currentIndex < usedPoolItems.Count - 1)
            {
                int nextIndex = currentIndex + 1;

                if (needToScroll && nextIndex > lastVisibleSuggestionIndex)
                {
                    float targetPosition = 1f - ((float)(nextIndex + 1 - visibleSuggestionsCount) / (usedPoolItems.Count - visibleSuggestionsCount));
                    scrollViewComponent.verticalNormalizedPosition = Mathf.Clamp01(targetPosition);
                    firstVisibleSuggestionIndex = nextIndex - visibleSuggestionsCount + 1;
                    lastVisibleSuggestionIndex = nextIndex;
                }

                SetSelection(nextIndex);
            }
            else
            {
                if (needToScroll)
                {
                    scrollViewComponent.verticalNormalizedPosition = 1f;
                    firstVisibleSuggestionIndex = 0;
                    lastVisibleSuggestionIndex = Mathf.Min(visibleSuggestionsCount - 1, usedPoolItems.Count - 1);
                }

                SetSelection(0);
            }
        }

        private void OnArrowUp(InputAction.CallbackContext obj)
        {
            if (currentIndex > 0)
            {
                int prevIndex = currentIndex - 1;

                if (needToScroll && prevIndex < firstVisibleSuggestionIndex)
                {
                    float targetPosition = 1f - ((float)prevIndex / (usedPoolItems.Count - visibleSuggestionsCount));
                    scrollViewComponent.verticalNormalizedPosition = Mathf.Clamp01(targetPosition);
                    firstVisibleSuggestionIndex = prevIndex;
                    lastVisibleSuggestionIndex = prevIndex + visibleSuggestionsCount - 1;
                }

                SetSelection(prevIndex);
            }
            else
            {
                int lastIndex = usedPoolItems.Count - 1;

                if (needToScroll)
                {
                    scrollViewComponent.verticalNormalizedPosition = 0f;
                    firstVisibleSuggestionIndex = Mathf.Max(0, lastIndex - visibleSuggestionsCount + 1);
                    lastVisibleSuggestionIndex = lastIndex;
                }

                SetSelection(lastIndex);
            }
        }

        /// <summary>
        /// Processes the list of found suggestions and displays them on the panel
        /// </summary>
        /// <param name="suggestionType"> The type of suggestion will change the max amount we can display as well as which pool to bring elements from </param>
        /// <param name="foundSuggestions"> The list of found suggestions to display </param>
        /// <typeparam name="T"> The type of Suggestion Element Data to use </typeparam>
        public void SetSuggestionValues<T>(InputSuggestionType suggestionType, IList<T> foundSuggestions) where T: IInputSuggestionElementData
        {
            noResultsIndicator.gameObject.SetActive(foundSuggestions.Count == 0);

            if (suggestionType == InputSuggestionType.NONE) return;

            int maxVisibleSuggestions = suggestionDataPerType[suggestionType].MaxSuggestionAmount;

            scrollViewComponent.vertical = foundSuggestions.Count > maxVisibleSuggestions;

            float scrollViewHeight;

            if (foundSuggestions.Count == 0)
                scrollViewHeight = configurationSo.noResultsHeight;
            else if (foundSuggestions.Count > 0 && foundSuggestions.Count <= maxVisibleSuggestions)
                scrollViewHeight = (suggestionDataPerType[suggestionType].SuggestionElementHeight * foundSuggestions.Count) + configurationSo.padding;
            else
            {
                needToScroll = true;
                firstVisibleSuggestionIndex = 0;
                visibleSuggestionsCount = maxVisibleSuggestions;
                lastVisibleSuggestionIndex = maxVisibleSuggestions - 1;
                scrollViewHeight = (suggestionDataPerType[suggestionType].SuggestionElementHeight * maxVisibleSuggestions) + configurationSo.padding;
            }

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
                T elementData = foundSuggestions[i];

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

            scrollViewComponent.verticalNormalizedPosition = 1;
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
                DCLInput.Instance.UI.ActionUp.performed += OnArrowUp;
                DCLInput.Instance.UI.ActionDown.performed += OnArrowDown;
                DCLInput.Instance.UI.Submit.performed += OnSubmit;
                DCLInput.Instance.UI.Tab.performed += OnSubmit;
            }
            else
            {
                needToScroll = false;
                lastSelectedInputSuggestion?.SetSelectionState(false);
                DCLInput.Instance.UI.ActionUp.performed -= OnArrowUp;
                DCLInput.Instance.UI.ActionDown.performed -= OnArrowDown;
                DCLInput.Instance.UI.Submit.performed -= OnSubmit;
                DCLInput.Instance.UI.Tab.performed -= OnSubmit;
            }

            gameObject.SetActive(isVisible);

            if (isVisible && usedPoolItems.Count > 0)
                SetSelection(0);
            else
                lastSelectedInputSuggestion = null;

            IsActive = isVisible;
        }
    }
}
