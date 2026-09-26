using DCL.Audio;
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


        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig OpenAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig CloseAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig ToggleOnAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig ToggleOffAudio { get; private set; }

        private void Start()
        {
            filterButton.onClick.AddListener(OnSortDropdownClick);
            CloseButtonArea.onClick.AddListener(OnSortDropdownClick);
            FilterContent.SetActive(false);
            CloseButtonArea.gameObject.SetActive(false);

            poisToggle.onValueChanged.AddListener((isOn) => OnToggleClicked(MapLayer.ScenesOfInterest, isOn));
            peopleToggle.onValueChanged.AddListener((isOn) => OnToggleClicked(MapLayer.HotUsersMarkers, isOn));
            favoritesToggle.onValueChanged.AddListener((isOn) => OnToggleClicked(MapLayer.Favorites, isOn));
            /* TODO: add the rest of the toggles once the MapLayers are implemented
            favoritesToggle.onValueChanged.AddListener((isOn) => OnFilterChanged?.Invoke(MapLayer.Favorites, isOn));
            friendsToggle.onValueChanged.AddListener((isOn) => OnFilterChanged?.Invoke(MapLayer.Friends, isOn));
            */
        }

        private void OnToggleClicked(MapLayer mapLayer, bool isOn)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(isOn? ToggleOnAudio : ToggleOffAudio);
            OnFilterChanged?.Invoke(mapLayer, isOn);
        }

        private void OnSortDropdownClick()
        {
            if (FilterContent.activeInHierarchy)
            {
                UIAudioEventsBus.Instance.SendPlayAudioEvent(CloseAudio);
                CloseButtonArea.gameObject.SetActive(false);
                CanvasGroup.DOFade(0, ANIMATION_TIME).SetEase(Ease.InOutQuad).OnComplete(() => FilterContent.SetActive(false));
            }
            else
            {
                UIAudioEventsBus.Instance.SendPlayAudioEvent(OpenAudio);
                CloseButtonArea.gameObject.SetActive(true);
                FilterContent.SetActive(true);
                CanvasGroup.DOFade(1, ANIMATION_TIME).SetEase(Ease.InOutQuad);
            }
        }
    }
}
