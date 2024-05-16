using DCL.Audio;
using DCL.Character.CharacterMotion.Components;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.UI
{
    public class TabSelectorView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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

        [field: SerializeField]
        public Animator tabAnimator;

        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig TabClickAudio { get; private set; }

        private void OnEnable()
        {
            tabAnimator.enabled = true;
            if (tabAnimator != null)
            {
                tabAnimator.Rebind();
                tabAnimator.Update(0);
            }
            TabSelectorToggle.onValueChanged.AddListener(OnToggle);
        }

        private void OnDisable()
        {
            TabSelectorToggle.onValueChanged.RemoveListener(OnToggle);
            tabAnimator.enabled = false;
        }

        private void OnToggle(bool toggle)
        {
            if(toggle)
                UIAudioEventsBus.Instance.SendPlayAudioEvent(TabClickAudio);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (tabAnimator != null)
                tabAnimator.SetTrigger(AnimationHashes.HOVER);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if(tabAnimator != null)
                tabAnimator.SetTrigger(AnimationHashes.UNHOVER);
        }
    }
}
