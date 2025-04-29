using DCL.Audio;
using DCL.UI.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.UI
{
    public class DropdownView : MonoBehaviour, IPointerDownHandler
    {
        [field: SerializeField]
        public TMP_Dropdown Dropdown { get; private set; }


        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig DropDownClickedAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig DropDownInteractedAudio { get; private set; }

        private void Awake()
        {
            Dropdown.template.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
        }

        private void OnEnable()
        {
            Dropdown.onValueChanged.AddListener(OnClick);
        }

        private void OnDisable()
        {
            Dropdown.onValueChanged.RemoveListener(OnClick);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(DropDownInteractedAudio);
        }

        private void OnClick(int value)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(DropDownClickedAudio);
        }


    }
}
