using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.UI;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utility;

namespace DCL.InWorldCamera.CameraReel.Components
{
    public class ReelThumbnailView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("References")]
        public Image thumbnailImage;
        [SerializeField] private LoadingBrightView loadingBrightView;
        [SerializeField] private RectTransform optionButtonContainer;
        [SerializeField] private Button button;
        [SerializeField] private GameObject outline;

        [Header("Configuration")]
        [SerializeField] private Vector3 optionButtonOffset = new (-15.83997f, -22f, 0);
        [SerializeField] private float scaleFactorOnHover = 1.03f;
        [SerializeField] private float scaleAnimationDuration = 0.3f;
        [SerializeField] private float thumbnailLoadedAnimationDuration = 0.3f;

        [HideInInspector] public CameraReelResponse cameraReelResponse;

        private CancellationTokenSource loadImageCts;
        private ICameraReelScreenshotsStorage cameraReelScreenshotsStorage;
        private OptionButtonController optionButton;

        public event Action<CameraReelResponse, Sprite> OnThumbnailLoaded;
        public event Action<CameraReelResponse> OnThumbnailClicked;

        public void Setup(CameraReelResponse cameraReelData, ICameraReelScreenshotsStorage cameraReelScreenshotsStorageService, OptionButtonController optionsButton)
        {
            this.cameraReelResponse = cameraReelData;
            this.cameraReelScreenshotsStorage = cameraReelScreenshotsStorageService;
            this.optionButton = optionsButton;

            if (this.optionButton is not null)
                this.optionButton.Hide += ToNormalAnimation;

            loadImageCts = loadImageCts.SafeRestart();
            thumbnailImage.sprite = null;
            LoadImage(cameraReelScreenshotsStorage, loadImageCts.Token).Forget();
        }

        private async UniTask LoadImage(ICameraReelScreenshotsStorage cameraReelScreenshotsStorage, CancellationToken token)
        {
            loadingBrightView.StartLoadingAnimation(thumbnailImage.gameObject);

            Texture2D thumbnailTexture = await cameraReelScreenshotsStorage.GetScreenshotThumbnailAsync(cameraReelResponse.thumbnailUrl, token);
            thumbnailImage.sprite = Sprite.Create(thumbnailTexture, new Rect(0, 0, thumbnailImage.rectTransform.rect.width, thumbnailImage.rectTransform.rect.height), Vector2.zero);

            loadingBrightView.FinishLoadingAnimation(thumbnailImage.gameObject);

            thumbnailImage.DOFade(1f, thumbnailLoadedAnimationDuration);

            OnThumbnailLoaded?.Invoke(cameraReelResponse, thumbnailImage.sprite);
            button.onClick.AddListener( () => OnThumbnailClicked?.Invoke(cameraReelResponse));
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            transform.DOScale(Vector3.one * scaleFactorOnHover, scaleAnimationDuration);
            optionButton?.Show(cameraReelResponse, optionButtonContainer.transform, optionButtonOffset);
            outline.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (optionButton != null)
            {
                if (optionButton.IsContextMenuOpen()) return;

                optionButton.HideControl();
                ToNormalAnimation();
            }
            else
                ToNormalAnimation();
        }

        private void ToNormalAnimation()
        {
            transform.DOScale(Vector3.one, scaleAnimationDuration);
            outline.SetActive(false);
        }

        public void Release()
        {
            OnThumbnailLoaded = null;
            OnThumbnailClicked = null;
            button.onClick.RemoveAllListeners();
            outline.SetActive(false);
            optionButton.Hide -= ToNormalAnimation;
        }
    }
}
