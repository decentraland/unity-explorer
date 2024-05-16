using DCL.Audio;
using DCL.Character.CharacterMotion.Components;
using MVC;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.ExplorePanel
{
    public class PersistentExploreOpenerView : ViewBase, IView, IPointerEnterHandler, IPointerExitHandler
    {
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
            ExploreOpenerAnimator.SetTrigger(AnimationHashes.PRESSED);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            ExploreOpenerAnimator.SetTrigger(AnimationHashes.HOVER);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ExploreOpenerAnimator.SetTrigger(AnimationHashes.UNHOVER);
        }

        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig ButtonPressedAudio { get; private set; }
    }
}
