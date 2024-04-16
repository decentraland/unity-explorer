using DCL.Audio;
using TMPro;
using UnityEngine;

namespace DCL.UI
{
    public class DropdownView : MonoBehaviour
    {
        [field: SerializeField]
        public TMP_Dropdown Dropdown { get; private set; }


        [field: Header("Audio")]
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

        private void OnClick(int value)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(DropDownInteractedAudio);
        }


    }
}
