using DCL.Audio;
using DCL.MapRenderer.MapLayers;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Navmap.FilterPanel
{
    public class NavmapFilterPanelView : MonoBehaviour
    {
        public event Action<MapLayer, bool> OnFilterChanged;

        [field: SerializeField]
        internal CanvasGroup canvasGroup;

        [field: SerializeField]
        private Toggle minigamesToggle;

        [field: SerializeField]
        private Toggle liveEventsToggle;

        [field: SerializeField]
        private Toggle favoritesToggle;

        [field: SerializeField]
        private Toggle poisToggle;

        [field: SerializeField]
        private Toggle peopleToggle;

        [field: SerializeField]
        private Toggle satelliteButton;

        [field: SerializeField]
        private GameObject satelliteButtonHighlight;

        [field: SerializeField]
        private Toggle parcelButton;

        [field: SerializeField]
        private GameObject parcelButtonHighlight;


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
            minigamesToggle.onValueChanged.AddListener((isOn) => OnToggleClicked(MapLayer.Pins, isOn));
            poisToggle.onValueChanged.AddListener((isOn) => OnToggleClicked(MapLayer.ScenesOfInterest, isOn));
            peopleToggle.onValueChanged.AddListener((isOn) => OnToggleClicked(MapLayer.HotUsersMarkers, isOn));
            favoritesToggle.onValueChanged.AddListener((isOn) => OnToggleClicked(MapLayer.Favorites, isOn));
            liveEventsToggle.onValueChanged.AddListener((isOn) => OnToggleClicked(MapLayer.LiveEvents, isOn));

            satelliteButton.onValueChanged.AddListener(ToggleSatelliteMap);
            parcelButton.onValueChanged.AddListener(ToggleParcelMap);
        }

        private void OnEnable()
        {
            ToggleSatelliteMap(true);
        }

        public void ToggleFilterPanel(bool isOn)
        {
            canvasGroup.alpha = isOn ? 1 : 0;
            canvasGroup.blocksRaycasts = isOn;
            canvasGroup.interactable = isOn;
            UIAudioEventsBus.Instance.SendPlayAudioEvent(isOn ? OpenAudio : CloseAudio);
        }

        private void ToggleSatelliteMap(bool isOn)
        {
            if (!isOn)
                return;

            satelliteButtonHighlight.SetActive(true);
            parcelButtonHighlight.SetActive(false);
            UIAudioEventsBus.Instance.SendPlayAudioEvent(ToggleOnAudio);
            OnFilterChanged?.Invoke(MapLayer.ParcelsAtlas, false);
            OnFilterChanged?.Invoke(MapLayer.SatelliteAtlas, true);
        }

        private void ToggleParcelMap(bool isOn)
        {
            if (!isOn)
                return;

            satelliteButtonHighlight.SetActive(false);
            parcelButtonHighlight.SetActive(true);
            UIAudioEventsBus.Instance.SendPlayAudioEvent(ToggleOnAudio);
            OnFilterChanged?.Invoke(MapLayer.SatelliteAtlas, false);
            OnFilterChanged?.Invoke(MapLayer.ParcelsAtlas, true);
        }

        private void OnToggleClicked(MapLayer mapLayer, bool isOn)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(isOn? ToggleOnAudio : ToggleOffAudio);
            OnFilterChanged?.Invoke(mapLayer, isOn);
        }
    }
}
