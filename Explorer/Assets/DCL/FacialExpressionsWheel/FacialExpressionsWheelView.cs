using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.CharacterPreview;
using DCL.UI;
using MVC;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.FacialExpressionsWheel
{
    public class FacialExpressionsWheelView : ViewBase, IView
    {
        // Mirrors EmotesWheelView: lets graphic raycaster + slot input work before the open
        // animation finishes, so users can commit a slot the instant it's visible.
        private const float ANIMATION_LOCK_DURATION = 0.05f;

        public event Action? Closed;

        [SerializeField]
        private Button[] closeButtons = null!;

        [field: SerializeField]
        public Button EmotesTabButton { get; private set; } = null!;

        [field: SerializeField]
        public FacialExpressionWheelSlotView[] Slots { get; private set; } = null!;

        [field: SerializeField]
        public NumericCyclerView EyebrowsCycler { get; private set; } = null!;

        [field: SerializeField]
        public NumericCyclerView EyesCycler { get; private set; } = null!;

        [field: SerializeField]
        public NumericCyclerView MouthCycler { get; private set; } = null!;

        [field: SerializeField]
        public CharacterPreviewView CharacterPreview { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text CurrentExpressionName { get; private set; } = null!;

        [field: SerializeField]
        public Animator WheelAnimator { get; private set; } = null!;

        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig OpenAudio { get; private set; } = null!;

        [field: SerializeField]
        public AudioClipConfig CloseAudio { get; private set; } = null!;

        private void Awake()
        {
            foreach (Button button in closeButtons)
                button.onClick.AddListener(() => Closed?.Invoke());
        }

        private void OnEnable() =>
            UIAudioEventsBus.Instance.SendPlayAudioEvent(OpenAudio);

        private void OnDisable() =>
            UIAudioEventsBus.Instance.SendPlayAudioEvent(CloseAudio);

        protected override UniTask PlayShowAnimationAsync(CancellationToken ct) =>
            UniTask.WaitUntil(() => WheelAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime > ANIMATION_LOCK_DURATION,
                cancellationToken: ct);

        protected override UniTask PlayHideAnimationAsync(CancellationToken ct)
        {
            WheelAnimator.SetTrigger(UIAnimationHashes.OUT);

            return UniTask.WaitUntil(() => WheelAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime > 1,
                cancellationToken: ct);
        }
    }
}
