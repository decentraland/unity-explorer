using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Places
{
    public class PlacesFilterSelectorView : MonoBehaviour
    {
        private const float ANIMATION_TIME = 0.2f;

        public event Action<bool>? SortByBestRatedSelected;
        public event Action<bool>? SortByMostActiveSelected;
        public event Action<bool>? SDK7OnlySelected;

        [SerializeField] private CanvasGroup canvasGroup = null!;
        [SerializeField] private DeselectableUiElement sortContentDeselectable = null!;
        [SerializeField] private Toggle sortByBestRated = null!;
        [SerializeField] private Toggle sortByMostActive = null!;
        [SerializeField] private Toggle sdk7Only = null!;
        [SerializeField] private Button sortDropdownButton = null!;

        private void Start()
        {
            ResetFilters();

            sortContentDeselectable.gameObject.SetActive(false);
            sortContentDeselectable.OnDeselectEvent += OnSortDropdownClick;
            sortDropdownButton.onClick.AddListener(OnSortDropdownClick);
            sortByBestRated.onValueChanged.AddListener(isOn => SortByBestRatedSelected?.Invoke(isOn));
            sortByMostActive.onValueChanged.AddListener(isOn => SortByMostActiveSelected?.Invoke(isOn));
            sdk7Only.onValueChanged.AddListener(isOn => SDK7OnlySelected?.Invoke(isOn));
        }

        private void OnDestroy()
        {
            sortContentDeselectable.OnDeselectEvent -= OnSortDropdownClick;
            sortDropdownButton.onClick.RemoveAllListeners();
            sortByBestRated.onValueChanged.RemoveAllListeners();
            sortByMostActive.onValueChanged.RemoveAllListeners();
            sdk7Only.onValueChanged.RemoveAllListeners();
        }

        public void ResetFilters(bool invokeEvents = true)
        {
            sortContentDeselectable.gameObject.SetActive(true);

            if (invokeEvents)
            {
                sortByBestRated.isOn = true;
                sdk7Only.isOn = true;
            }
            else
            {
                sortByBestRated.SetIsOnWithoutNotify(true);
                sdk7Only.SetIsOnWithoutNotify(true);
            }

            sortContentDeselectable.gameObject.SetActive(false);
        }

        private void OnSortDropdownClick()
        {
            if (sortContentDeselectable.gameObject.activeInHierarchy)
                canvasGroup.DOFade(0, ANIMATION_TIME).SetEase(Ease.InOutQuad).OnComplete(() => sortContentDeselectable.gameObject.SetActive(false));
            else
            {
                sortContentDeselectable.gameObject.SetActive(true);
                sortContentDeselectable.SelectElement();
                canvasGroup.DOFade(1, ANIMATION_TIME).SetEase(Ease.InOutQuad);
            }
        }
    }
}
