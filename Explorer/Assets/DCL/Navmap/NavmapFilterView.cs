using DCL.MapRenderer.MapLayers;
using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Navmap
{
    public class NavmapFilterView : MonoBehaviour
    {
        private const float ANIMATION_TIME = 0.2f;
        public event Action<MapLayer, bool> OnFilterChanged;

        [field: SerializeField]
        public CanvasGroup CanvasGroup { get; private set; }

        [field: SerializeField]
        public Button CloseButtonArea { get; private set; }

        [field: SerializeField]
        public GameObject FilterContent { get; private set; }

        [field: SerializeField]
        public Button filterButton;

        [field: SerializeField]
        public Button infoButton;

        [field: SerializeField]
        public Button daoButton;

        [field: SerializeField]
        public GameObject infoContent;

        [field: SerializeField]
        private Toggle favoritesToggle;

        [field: SerializeField]
        private Toggle poisToggle;

        [field: SerializeField]
        private Toggle friendsToggle;

        [field: SerializeField]
        private Toggle peopleToggle;

        private void Start()
        {
            filterButton.onClick.AddListener(OnSortDropdownClick);
            CloseButtonArea.onClick.AddListener(OnSortDropdownClick);
            FilterContent.SetActive(false);
            CloseButtonArea.gameObject.SetActive(false);

            poisToggle.onValueChanged.AddListener((isOn) => OnFilterChanged?.Invoke(MapLayer.ScenesOfInterest, isOn));
            peopleToggle.onValueChanged.AddListener((isOn) => OnFilterChanged?.Invoke(MapLayer.HotUsersMarkers, isOn));
            favoritesToggle.onValueChanged.AddListener((isOn) => OnFilterChanged?.Invoke(MapLayer.Favorites, isOn));
            /* TODO: add the rest of the toggles once the MapLayers are implemented
            favoritesToggle.onValueChanged.AddListener((isOn) => OnFilterChanged?.Invoke(MapLayer.Favorites, isOn));
            friendsToggle.onValueChanged.AddListener((isOn) => OnFilterChanged?.Invoke(MapLayer.Friends, isOn));
            */
        }

        private void OnSortDropdownClick()
        {
            if (FilterContent.activeInHierarchy)
            {
                CloseButtonArea.gameObject.SetActive(false);
                CanvasGroup.DOFade(0, ANIMATION_TIME).SetEase(Ease.InOutQuad).OnComplete(() => FilterContent.SetActive(false));
            }
            else
            {
                CloseButtonArea.gameObject.SetActive(true);
                FilterContent.SetActive(true);
                CanvasGroup.DOFade(1, ANIMATION_TIME).SetEase(Ease.InOutQuad);
            }
        }
    }
}
