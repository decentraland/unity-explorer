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

            satelliteButton.onValueChanged.AddListener((isOn) => ToggleMapType(MapLayer.SatelliteAtlas, isOn));
            parcelButton.onValueChanged.AddListener((isOn) => ToggleMapType(MapLayer.ParcelsAtlas, isOn));
        }

        private void OnEnable()
        {
            ToggleMapType(MapLayer.ParcelsAtlas, false);
            ToggleMapType(MapLayer.SatelliteAtlas, true);
        }

        public void ToggleFilterPanel(bool isOn)
        {
            canvasGroup.alpha = isOn ? 1 : 0;
            canvasGroup.blocksRaycasts = isOn;
            canvasGroup.interactable = isOn;
            UIAudioEventsBus.Instance.SendPlayAudioEvent(isOn ? OpenAudio : CloseAudio);
        }

        private void ToggleMapType(MapLayer mapLayer, bool isOn)
        {
            satelliteButtonHighlight.SetActive(mapLayer == MapLayer.SatelliteAtlas && isOn);
            parcelButtonHighlight.SetActive(mapLayer == MapLayer.ParcelsAtlas && isOn);
            UIAudioEventsBus.Instance.SendPlayAudioEvent(isOn ? ToggleOnAudio : ToggleOffAudio);
            OnFilterChanged?.Invoke(mapLayer, isOn);
        }

        private void OnToggleClicked(MapLayer mapLayer, bool isOn)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(isOn? ToggleOnAudio : ToggleOffAudio);
            OnFilterChanged?.Invoke(mapLayer, isOn);
        }
    }
}
