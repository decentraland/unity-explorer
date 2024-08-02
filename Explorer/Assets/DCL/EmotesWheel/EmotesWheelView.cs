using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.UI;
using MVC;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.EmotesWheel
{
    public class EmotesWheelView : ViewBase, IView
    {
        public event Action? OnClose;

        [SerializeField]
        private Button[] closeButtons = null!;

        [field: SerializeField]
        public Button EditButton { get; set; } = null!;

        [field: SerializeField]
        public EmoteWheelSlotView[] Slots { get; set; } = null!;

        [field: SerializeField]
        public TMP_Text CurrentEmoteName { get; set; } = null!;

        [field: SerializeField]
        public Animator EmotesWheelAnimator { get; set; } = null!;

        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig OpenAudio { get; private set; }

        [field: SerializeField]
        public AudioClipConfig CloseAudio { get; private set; }

        private void Awake()
        {
            foreach (Button button in closeButtons)
                button.onClick.AddListener(() => { OnClose?.Invoke(); });
        }

        private void OnEnable()
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(OpenAudio);
        }

        private void OnDisable()
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(CloseAudio);
        }

        protected override UniTask PlayShowAnimationAsync(CancellationToken ct)
        {
            return UniTask.WaitUntil(() => EmotesWheelAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime > 1,
                cancellationToken: ct);
        }

        protected override UniTask PlayHideAnimationAsync(CancellationToken ct)
        {
            EmotesWheelAnimator.SetTrigger(UIAnimationHashes.OUT);

            return UniTask.WaitUntil(() => EmotesWheelAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime > 1,
                cancellationToken: ct);
        }
    }
}
