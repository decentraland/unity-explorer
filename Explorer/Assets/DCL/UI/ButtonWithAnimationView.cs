using DCL.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.UI
{
    public class ButtonWithAnimationView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [field: SerializeField]
        public Button Button { get; private set; }

        [field: SerializeField]
        public Animator ButtonAnimator { get; private set; }

        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig ButtonPressedAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig ButtonHoverAudio { get; private set; }

        private void OnEnable()
        {
            Button.onClick.AddListener(OnClick);
            ButtonAnimator.enabled = true;
            ButtonAnimator.Rebind();
            ButtonAnimator.Update(0);
        }

        private void OnDisable()
        {
            Button.onClick.RemoveListener(OnClick);
            ButtonAnimator.enabled = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            ButtonAnimator.SetTrigger(UIAnimationHashes.HOVER);
            UIAudioEventsBus.Instance.SendPlayAudioEvent(ButtonHoverAudio);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ButtonAnimator.SetTrigger(UIAnimationHashes.UNHOVER);
        }

        private void OnClick()
        {
            ButtonAnimator.SetTrigger(UIAnimationHashes.PRESSED);
            UIAudioEventsBus.Instance.SendPlayAudioEvent(ButtonPressedAudio);
        }
    }
}
