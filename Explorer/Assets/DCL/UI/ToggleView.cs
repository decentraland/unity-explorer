using DCL.Audio;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class ToggleView : MonoBehaviour
    {
        [field: SerializeField] internal bool autoToggleImagesOnToggle { get; private set; }

        [field: SerializeField]
        public Toggle Toggle { get; private set; }

        [field: SerializeField]
        public GameObject OnImage { get; private set; }

        [field: SerializeField]
        public GameObject OffImage { get; private set; }

        [field: SerializeField]
        public Image OnBackgroundImage { get; private set; }

        [field: SerializeField]
        public Image OffBackgroundImage { get; private set; }

        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig ToggleOnAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig ToggleOffAudio { get; private set; }
        
        [field: Header("Colors")]
        [field: Header("Interactable Colors")]
        [field: SerializeField] private ColorableElement[] colorableElements { get; set; }

        /// <summary>
        /// Gets or sets whether sound FXs will be played or not when interacting with the toggle.
        /// </summary>
        public bool IsSoundEnabled
        {
            get;
            set;
        }

        private void OnEnable()
        {
            Toggle.onValueChanged.AddListener(OnToggle);
        }

        private void OnDisable()
        {
            Toggle.onValueChanged.RemoveListener(OnToggle);
        }

        public void SetToggle(bool isOn)
        {
            Toggle.isOn = isOn;
            OnToggle(isOn);
        }

        public void SetToggleGraphics(bool toggle)
        {
            OnImage.SetActive(toggle);
            OffImage.SetActive(!toggle);
            OnBackgroundImage.gameObject.SetActive(toggle);
            OffBackgroundImage.gameObject.SetActive(!toggle);
        }

        public void SetInteractable(bool isInteractable)
        {
            foreach (var element in colorableElements)
            {
                element.Set(isInteractable);
            }
            
            Toggle.interactable = isInteractable;
        }

        private void OnToggle(bool toggle)
        {
            if(IsSoundEnabled)
                UIAudioEventsBus.Instance.SendPlayAudioEvent(toggle ? ToggleOnAudio : ToggleOffAudio);

            if (autoToggleImagesOnToggle)
                SetToggleGraphics(toggle);
        }
        
        [Serializable]
        private struct ColorableElement
        {
            [field: SerializeField] private Graphic element { get; set; }
            [field: SerializeField] private Color interactableColor { get; set; }
            [field: SerializeField] private Color nonInteractableColor { get; set; }

            public void Set(bool isInteractable)
            {
                if (element == null) return;
                
                element.color = isInteractable ? interactableColor : nonInteractableColor;
            }
        }
    }
}
