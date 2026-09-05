using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.Diagnostics;
using DCL.UI;
using DG.Tweening;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using Utility.Ownership;

namespace DCL.InWorldCamera.UI
{
    public class InWorldCameraView : ViewBase, IView
    {
        private const float ANIMATION_SPEED = 0.2f;

        [SerializeField] private CanvasGroup canvasGroup;

        [Header("CAPTURE VFX")]
        [SerializeField] private Image whiteSplashImage;
        [SerializeField] private RectTransform cameraReelIcon;
        [SerializeField] private Image animatedImage;
        [Space]
        [SerializeField] private ContextualImage gridImage;

        private Sequence currentVfxSequence;

        [field: Space]
        [field: SerializeField] public WarningNotificationView NoStorageNotification { get; private set; }

        [field: SerializeField] public ElementWithCloseArea ShortcutsInfoPanel { get; private set; }

        [field: Header("AUDIO")]
        [field: SerializeField] public AudioClipConfig SFXScreenshotCapture { get; private set; }

        [field: Header("BUTTONS")]
        [field: SerializeField] public Button CameraReelButton { get; private set; }
        [field: SerializeField] public Button TakeScreenshotButton { get; private set; }
        [field: SerializeField] public Button CloseButton { get; private set; }
        [field: SerializeField] public Button ShortcutsInfoButton { get; private set; }

        public bool IsVfxInProgress => currentVfxSequence.IsActive() && !currentVfxSequence.IsComplete();

        public override async UniTask ShowAsync(CancellationToken ct)
        {
            await gridImage.TriggerOrWaitReadyAsync(ct);
            await base.ShowAsync(ct);
        }

        public void ScreenshotCaptureAnimation(Texture2D screenshotImage, float splashDuration, float afterSplashPause, float transitionDuration)
        {
            currentVfxSequence?.Kill(complete: true);

            animatedImage.sprite = Sprite.Create(screenshotImage, new Rect(0, 0, screenshotImage.width, screenshotImage.height), Vector2.zero);

            animatedImage.enabled = true;
            whiteSplashImage.enabled = true;

            currentVfxSequence = PrepareVFXSequence(splashDuration, afterSplashPause, transitionDuration).Play();
        }

        protected override UniTask PlayShowAnimationAsync(CancellationToken ct)
        {
            canvasGroup.alpha = 0;
            return canvasGroup.DOFade(1, ANIMATION_SPEED).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct);
        }

        protected override UniTask PlayHideAnimationAsync(CancellationToken ct) =>
            canvasGroup.DOFade(0, ANIMATION_SPEED).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct);

        private Sequence PrepareVFXSequence(float splashDuration, float afterSplashPause, float transitionDuration)
        {
            Sequence vfxSequence = DOTween.Sequence();

            vfxSequence.Append(AnimateSplash(splashDuration));
            vfxSequence.AppendInterval(afterSplashPause); // Delay between splash and transition
            vfxSequence.Append(AnimateVFXImageTransition(transitionDuration));
            vfxSequence.Join(AnimateVFXImageScale(transitionDuration));

            return vfxSequence;
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
            animatedImage.rectTransform.DOScale(GetTargetScale(animatedImage.rectTransform, cameraReelIcon), duration)
                         .SetEase(Ease.InOutQuad)
                         .OnComplete(() =>
                          {
                              animatedImage.enabled = false;
                              animatedImage.rectTransform.localScale = Vector3.one;
                          });

        private Vector3 GetTargetScale(RectTransform animatedRect, RectTransform targetRect)
        {
            Vector2 animatedSize = animatedRect.rect.size;
            Vector2 targetSize = targetRect.rect.size;

            float scaleX = targetSize.x / animatedSize.x;
            float scaleY = targetSize.y / animatedSize.y;

            return new Vector3(scaleX, scaleY, 1f);
        }
    }
}
