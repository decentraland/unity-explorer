using DCL.Audio;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.UI
{
    public class TabSelectorView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly int HOVER = Animator.StringToHash("Hover");
        private static readonly int UNHOVER = Animator.StringToHash("Unhover");
        private static readonly int ACTIVE = Animator.StringToHash("Active");

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

        [field: SerializeField]
        public Animator tabAnimator;

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
            tabAnimator.SetTrigger(ACTIVE);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (tabAnimator != null)
                tabAnimator.SetTrigger(HOVER);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if(tabAnimator != null)
                tabAnimator.SetTrigger(UNHOVER);
        }
    }
}
