using DCL.Audio;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Backpack
{
    public class BackpackSortDropdownView : MonoBehaviour
    {
        private const float ANIMATION_TIME = 0.2f;

        [Header("Audio")]
        [field: SerializeField]
        public UIAudioType OpenDropDownAudio = UIAudioType.GENERIC_DROPDOWN;

        [field: SerializeField]
        public CanvasGroup CanvasGroup { get; private set; }

        [field: SerializeField]
        public DeselectableUiElement SortContentDeselectable { get; private set; }

        [field: SerializeField]
        internal Toggle sortNewest { get; private set; }

        [field: SerializeField]
        internal Toggle sortOldest { get; private set; }

        [field: SerializeField]
        internal Toggle sortRarest { get; private set; }

        [field: SerializeField]
        internal Toggle sortLessRares { get; private set; }

        [field: SerializeField]
        internal Toggle sortNameAz { get; private set; }

        [field: SerializeField]
        internal Toggle sortNameZa { get; private set; }

        [field: SerializeField]
        internal Toggle collectiblesOnly { get; private set; }

        [field: SerializeField]
        internal Button sortDropdownButton { get; private set; }

        private void Start()
        {
            sortDropdownButton.onClick.AddListener(OnSortDropdownClick);
            SortContentDeselectable.gameObject.SetActive(false);
            SortContentDeselectable.OnDeselectEvent += OnSortDropdownClick;
        }

        private void OnSortDropdownClick()
        {
            UIAudioEventsBus.Instance.SendAudioEvent(OpenDropDownAudio);

            if (SortContentDeselectable.gameObject.activeInHierarchy) { CanvasGroup.DOFade(0, ANIMATION_TIME).SetEase(Ease.InOutQuad).OnComplete(() => SortContentDeselectable.gameObject.SetActive(false)); }
            else
            {
                SortContentDeselectable.gameObject.SetActive(true);
                SortContentDeselectable.SelectElement();
                CanvasGroup.DOFade(1, ANIMATION_TIME).SetEase(Ease.InOutQuad);
            }
        }
    }
}
