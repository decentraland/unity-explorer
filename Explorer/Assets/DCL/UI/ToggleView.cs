using DCL.Audio;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class ToggleView : MonoBehaviour
    {
        [field: SerializeField] internal bool autoToggleImagesOnToggle { get; private set; }

        [field: SerializeField] public Toggle Toggle { get; private set; }

        [field: SerializeField] public GameObject OnImage { get; private set; }

        [field: SerializeField] public GameObject OffImage { get; private set; }

        [field: SerializeField] public Image OnBackgroundImage { get; private set; }

        [field: SerializeField] public Image OffBackgroundImage { get; private set; }

        [field: Header("Audio")]
        [field: SerializeField] public AudioClipConfig ToggleOnAudio { get; private set; }
        [field: SerializeField] public AudioClipConfig ToggleOffAudio { get; private set; }
        
        [field: Header("Interactable color scheme")]
        [field: SerializeField] private CanvasGroup canvasGroup  { get; set; }
        [field: SerializeField] private float interactableAlpha { get; set; }
        [field: SerializeField] private float nonInteractableAlpha { get; set; }

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
            if(autoToggleImagesOnToggle)
                SetToggleGraphics(Toggle.isOn);
            Toggle.onValueChanged.AddListener(OnToggle);
        }

        private void OnDisable()
        {
            Toggle.onValueChanged.RemoveListener(OnToggle);
        }

        public void SetToggle(bool isOn, bool withoutNotify = false)
        {
            if(withoutNotify)
                Toggle.SetIsOnWithoutNotify(isOn);
            else
                Toggle.isOn = isOn;
            
            OnToggle(isOn);
        }

        public void SetToggleGraphics(bool toggle)
        {
            OnImage.SetActive(toggle);
            OffImage.SetActive(!toggle);
            OnBackgroundImage.gameObject.SetActive(toggle);
            OffBackgroundImage.gameObject.SetActive(!toggle);
            Toggle.targetGraphic = toggle ? OnBackgroundImage : OffBackgroundImage;
        }

        public void SetInteractable(bool isInteractable)
        {
            if(canvasGroup != null)
                canvasGroup.alpha = isInteractable ? interactableAlpha : nonInteractableAlpha;
            
            Toggle.interactable = isInteractable;
        }

        private void OnToggle(bool toggle)
        {
            if(IsSoundEnabled)
                UIAudioEventsBus.Instance.SendPlayAudioEvent(toggle ? ToggleOnAudio : ToggleOffAudio);

            if (autoToggleImagesOnToggle)
                SetToggleGraphics(toggle);
        }
    }
}
