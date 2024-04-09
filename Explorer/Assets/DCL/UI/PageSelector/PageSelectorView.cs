using DCL.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class PageSelectorView : MonoBehaviour
    {
        [field: SerializeField]
        public Button NextPage { get; private set; }

        [field: SerializeField]
        public Button PreviousPage { get; private set; }

        [field: SerializeField]
        public Transform PagesContainer { get; private set; }


        [Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig NextButtonPressed;
        [field: SerializeField]
        public AudioClipConfig PreviousButtonPressed;

        private void OnEnable()
        {
            NextPage.onClick.AddListener(OnClickNextPage);
            PreviousPage.onClick.AddListener(OnClickPreviousPage);
        }

        private void OnDisable()
        {
            NextPage.onClick.RemoveListener(OnClickNextPage);
            PreviousPage.onClick.RemoveListener(OnClickPreviousPage);
        }

        private void OnClickNextPage()
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(NextButtonPressed);
        }
        private void OnClickPreviousPage()
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(PreviousButtonPressed);
        }


    }
}
