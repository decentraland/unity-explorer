using DCL.Audio;
using MVC;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.ExplorePanel
{
    public class PersistentExploreOpenerView : ViewBase, IView, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly int HOVER = Animator.StringToHash("Hover");
        private static readonly int UNHOVER = Animator.StringToHash("Unhover");
        private static readonly int PRESSED = Animator.StringToHash("Pressed");
        
        [field: SerializeField]
        public Button OpenExploreButton { get; private set; }

        [field: SerializeField]
        public Animator ExploreOpenerAnimator { get; private set; }

        private void Awake()
        {
            OpenExploreButton.onClick.AddListener(OnOpenExploreButtonClicked);
        }

        private void OnOpenExploreButtonClicked()
        {
            ExploreOpenerAnimator.SetTrigger(PRESSED);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            ExploreOpenerAnimator.SetTrigger(HOVER);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ExploreOpenerAnimator.SetTrigger(UNHOVER);
        }
        
        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig ButtonPressedAudio { get; private set; }
    }
}
