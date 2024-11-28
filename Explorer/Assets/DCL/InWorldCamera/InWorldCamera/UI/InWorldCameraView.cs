using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.UI;
using DG.Tweening;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.InWorldCamera.ScreencaptureCamera.UI
{
    public class InWorldCameraView : ViewBase, IView
    {
        private const float ANIMATION_SPEED = 0.2f;

        [SerializeField] private CanvasGroup canvasGroup;

        [Header("CAPTURE VFX")]
        [SerializeField] private Image whiteSplashImage;
        [SerializeField] private RectTransform cameraReelIcon;
        [SerializeField] private Image animatedImage;
        private Sequence currentVfxSequence;

        [field: Space]
        [field: SerializeField] public WarningNotificationView NoStorageNotification { get; private set; }

        [field: SerializeField] public ElementWithCloseArea ShortcutsInfoPanel { get; private set; }

        [field: Header("AUDIO")]
        [field: SerializeField] public AudioClipConfig SFXScreenshotCapture { get; private set; }
        [field: SerializeField] public AudioClipConfig Open { get; private set; }
        [field: SerializeField] public AudioClipConfig Close { get; private set; }

        [field: Header("BUTTONS")]
        [field: SerializeField] public Button CameraReelButton { get; private set; }
        [field: SerializeField] public Button TakeScreenshotButton { get; private set; }
        [field: SerializeField] public Button CloseButton { get; private set; }
        [field: SerializeField] public Button ShortcutsInfoButton { get; private set; }

        public void ScreenshotCaptureAnimation(Texture2D screenshotImage, float splashDuration, float afterSplashPause, float transitionDuration)
        {
            currentVfxSequence?.Complete();
            currentVfxSequence?.Kill();

            animatedImage.sprite = Sprite.Create(screenshotImage, new Rect(0, 0, screenshotImage.width, screenshotImage.height), Vector2.zero);

            animatedImage.enabled = true;
            whiteSplashImage.enabled = true;

            currentVfxSequence = CaptureVFXSequence(splashDuration, afterSplashPause, transitionDuration).Play();
        }

        protected override UniTask PlayShowAnimationAsync(CancellationToken ct)
        {
            canvasGroup.alpha = 0;
            UIAudioEventsBus.Instance.SendPlayAudioEvent(Open);
            return canvasGroup.DOFade(1, ANIMATION_SPEED).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct);
        }

        protected override UniTask PlayHideAnimationAsync(CancellationToken ct)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(Close);
            return canvasGroup.DOFade(0, ANIMATION_SPEED).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct);
        }

        private Sequence CaptureVFXSequence(float splashDuration, float afterSplashPause, float transitionDuration)
        {
            Sequence sequence = DOTween.Sequence();

            sequence.Append(AnimateSplash(splashDuration));
            sequence.AppendInterval(afterSplashPause); // Delay between splash and transition
            sequence.Append(AnimateVFXImageTransition(transitionDuration));
            sequence.Join(AnimateVFXImageScale(transitionDuration));
            sequence.OnComplete(() => animatedImage.enabled = false);

            return sequence;
        }

        private Tween AnimateSplash(float duration)
        {
            return whiteSplashImage.DOFade(0f, duration)
                                   .SetEase(Ease.InOutQuad)
                                   .OnComplete(() =>
                                    {
                                        whiteSplashImage.enabled = false;
                                        whiteSplashImage.color = Color.white;
                                    });
        }

        private Tween AnimateVFXImageTransition(float duration)
        {
            Vector3 cachedPosition = animatedImage.rectTransform.position;

            return animatedImage.rectTransform.DOMove(cameraReelIcon.position, duration)
                                .SetEase(Ease.InOutQuad)
                                .OnComplete(() => { animatedImage.rectTransform.position = cachedPosition; });
        }

        private Tween AnimateVFXImageScale(float duration) =>
            animatedImage.rectTransform.DOScale(Vector2.zero, duration)
                         .SetEase(Ease.InOutQuad)
                         .OnComplete(() => { animatedImage.rectTransform.localScale = Vector2.one; });
    }
}
