using DCL.Audio;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class TabSelectorView : MonoBehaviour
    {
        [Header("Audio")]
        [field: SerializeField]
        public UIAudioType AudioType = UIAudioType.GENERIC_TAB_SELECTED;
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
            UIAudioEventsBus.Instance.SendAudioEvent(AudioType);
        }
    }
}
