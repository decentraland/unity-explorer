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
        private Button satelliteButton;

        [field: SerializeField]
        private Button parcelButton;


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
            //liveEventsToggle.onValueChanged.AddListener((isOn) => OnToggleClicked(MapLayer.ScenesOfInterest, isOn));
            poisToggle.onValueChanged.AddListener((isOn) => OnToggleClicked(MapLayer.ScenesOfInterest, isOn));
            peopleToggle.onValueChanged.AddListener((isOn) => OnToggleClicked(MapLayer.HotUsersMarkers, isOn));
            favoritesToggle.onValueChanged.AddListener((isOn) => OnToggleClicked(MapLayer.Favorites, isOn));
        }

        private void OnToggleClicked(MapLayer mapLayer, bool isOn)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(isOn? ToggleOnAudio : ToggleOffAudio);
            OnFilterChanged?.Invoke(mapLayer, isOn);
        }
    }
}
