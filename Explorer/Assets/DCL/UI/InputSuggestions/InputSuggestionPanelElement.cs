using DCL.Optimization.Pools;
using MVC;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DCL.UI.SuggestionPanel
{
    public class InputSuggestionPanelElement : MonoBehaviour, IViewWithGlobalDependencies
    {
        public delegate void SuggestionSelectedDelegate(string hasFocus, bool shouldClose);

        public event SuggestionSelectedDelegate SuggestionSelectedEvent;

        [Header("Panel Configuration")]
        [field: SerializeField] private SuggestionPanelConfigurationSO configurationSo;

        [Header("References")]
        [field: SerializeField] private Transform suggestionContainer;
        [field: SerializeField] private RectTransform scrollView;
        [field: SerializeField] private ScrollRect scrollViewComponent;
        [field: SerializeField] private GameObject noResultsIndicator;

        public bool IsActive { get; private set; }

        private readonly Dictionary<InputSuggestionType, GameObjectPool<BaseInputSuggestionElement>> suggestionItemsPools = new ();
        private readonly List<BaseInputSuggestionElement> usedPoolItems = new ();



        private InputSuggestionType currentSuggestionType;
        private int currentIndex = 0;
        private BaseInputSuggestionElement lastSelectedInputSuggestion;
        private ViewDependencies viewDependencies;

        public void InjectDependencies(ViewDependencies dependencies)
        {
            viewDependencies = dependencies;
        }

        public InputSuggestionPanelElement()
        {
            CreateSuggestionPools();
        }

        private void CreateSuggestionPools()
        {
            foreach (var suggestionElement in configurationSo.SuggestionElements)
            {
                var suggestionPool = new GameObjectPool<BaseInputSuggestionElement>(
                    suggestionContainer!,
                    () => CreatePoolElement(suggestionElement),
                    onRelease: element => { element.OnReleased(); },
                    maxSize: 80,
                    onGet: element => { element.OnGet(); }
                );

                suggestionItemsPools.Add(suggestionElement.GetSuggestionType(), suggestionPool);
            }
        }

        private BaseInputSuggestionElement CreatePoolElement(BaseInputSuggestionElement prefab)
        {
            BaseInputSuggestionElement inputSuggestionElement = Instantiate(prefab, suggestionContainer);
            inputSuggestionElement.SuggestionSelectedEvent += (suggestionId) => SuggestionSelectedEvent?.Invoke(suggestionId, true);
            return inputSuggestionElement;
        }


        private void OnSubmit(InputAction.CallbackContext obj)
        {
            if (lastSelectedInputSuggestion != null && IsActive)
                SuggestionSelectedEvent?.Invoke(lastSelectedInputSuggestion.SuggestionId, false);
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


        public void SetSuggestionValues(InputSuggestionType suggestionType, List<ISuggestionElementData> foundSuggestions)
        {
            noResultsIndicator.gameObject.SetActive(foundSuggestions.Count == 0);

            scrollViewComponent.vertical = foundSuggestions.Count > 7;

            switch (foundSuggestions.Count)
            {
                case <= 1:
                    scrollView.sizeDelta = new Vector2(scrollView.sizeDelta.x, configurationSo.minHeight);
                    break;
                case <= 7:
                    scrollView.sizeDelta = new Vector2(scrollView.sizeDelta.x, (configurationSo.entryHeight * foundSuggestions.Count) + configurationSo.padding);
                    break;
                default:
                    scrollView.sizeDelta = new Vector2(scrollView.sizeDelta.x, configurationSo.maxHeight);
                    break;
            }

            //if the suggestion type is different, we release all items from the pool,
            //otherwise, we only release the elements that are over the found suggestion amount.
            if (currentSuggestionType != suggestionType)
            {
                for (var i = 0; i < usedPoolItems.Count; i++)
                    suggestionItemsPools[currentSuggestionType].Release(usedPoolItems[i]);
                usedPoolItems.Clear();
            }
            else
            {
                for(int i = foundSuggestions.Count; i < usedPoolItems.Count; i++)
                    suggestionItemsPools[currentSuggestionType].Release(usedPoolItems[i]);

                for(int i = usedPoolItems.Count - 1; i >= foundSuggestions.Count; i--)
                    usedPoolItems.RemoveAt(i);
            }

            for (var i = 0; i < foundSuggestions.Count; i++)
            {
                ISuggestionElementData elementData = foundSuggestions[i];
                if(usedPoolItems.Count > i)
                {
                    usedPoolItems[i].Setup(elementData);
                    usedPoolItems[i].gameObject.transform.SetAsLastSibling();
                }
                else
                {
                    BaseInputSuggestionElement emojiSuggestionView = suggestionItemsPools[suggestionType].Get();
                    emojiSuggestionView.Setup(elementData);
                    emojiSuggestionView.gameObject.transform.SetAsLastSibling();
                    usedPoolItems.Add(emojiSuggestionView);
                }
            }

            if(usedPoolItems.Count > 0)
                SetSelection(0);
            else
                lastSelectedInputSuggestion = null;
        }

        private void SetSelection(int index)
        {
            if (!gameObject.activeInHierarchy)
                return;

            if (lastSelectedInputSuggestion != null)
                lastSelectedInputSuggestion.SetSelectionState(false);//SelectedBackground.SetActive(false));

            currentIndex = index;
            usedPoolItems[index].SetSelectionState(true);//SelectedBackground.SetActive(true));
            lastSelectedInputSuggestion = usedPoolItems[index];
        }

        public void SetPanelVisibility(bool isVisible)
        {
            if (isVisible)
            {
                viewDependencies.DclInput.Player.ActionForward.performed += OnArrowUp;
                viewDependencies.DclInput.Player.ActionBackward.performed += OnArrowDown;
                viewDependencies.DclInput.UI.Submit.performed += OnSubmit;
            }
            else
            {
                viewDependencies.DclInput.Player.ActionForward.performed -= OnArrowUp;
                viewDependencies.DclInput.Player.ActionBackward.performed -= OnArrowDown;
                viewDependencies.DclInput.UI.Submit.performed -= OnSubmit;
            }

            IsActive = isVisible;
            gameObject.SetActive(isVisible);
        }
    }
}
