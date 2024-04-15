using DCL.Audio;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class TabSelectorView : MonoBehaviour
    {
        [field: SerializeField]
        public Toggle TabSelectorToggle { get; private set; }

        [field: SerializeField]
        public Image SelectedBackground { get; private set; }

        [field: SerializeField]
        public Image UnselectedImage { get; private set; }

        [field: SerializeField]
        public Image SelectedImage { get; private set; }

        [field: SerializeField]
        public GameObject UnselectedText { get; private set; }

        [field: SerializeField]
        public GameObject SelectedText { get; private set; }

        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig TabClickAudio { get; private set; }


        private void OnEnable()
        {
            TabSelectorToggle.onValueChanged.AddListener(OnToggle);
        }

        private void OnDisable()
        {
            TabSelectorToggle.onValueChanged.RemoveListener(OnToggle);
        }

        private void OnToggle(bool toggle)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(TabClickAudio);
        }
    }
}
