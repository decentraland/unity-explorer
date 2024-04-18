using DCL.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

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
