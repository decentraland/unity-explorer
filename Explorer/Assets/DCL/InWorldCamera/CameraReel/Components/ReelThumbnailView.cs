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
        private const int THUMBNAIL_WIDTH = 272;
        private const int THUMBNAIL_HEIGHT = 201;

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

            loadImageCts = loadImageCts.SafeRestart();
            thumbnailImage.sprite = null;
            LoadImage(cameraReelScreenshotsStorage, loadImageCts.Token).Forget();
        }

        private async UniTask LoadImage(ICameraReelScreenshotsStorage cameraReelScreenshotsStorage, CancellationToken token)
        {
            loadingBrightView.StartLoadingAnimation(thumbnailImage.gameObject);

            Texture2D thumbnailTexture = await cameraReelScreenshotsStorage.GetScreenshotThumbnailAsync(cameraReelResponse.thumbnailUrl);
            thumbnailImage.sprite = Sprite.Create(thumbnailTexture, new Rect(0, 0, THUMBNAIL_WIDTH, THUMBNAIL_HEIGHT), Vector2.zero);

            loadingBrightView.FinishLoadingAnimation(thumbnailImage.gameObject);

            thumbnailImage.DOFade(1f, thumbnailLoadedAnimationDuration);

            OnThumbnailLoaded?.Invoke(cameraReelResponse, thumbnailImage.sprite);
            button.onClick.AddListener( () => OnThumbnailClicked?.Invoke(cameraReelResponse));
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            transform.DOScale(Vector3.one * scaleFactorOnHover, scaleAnimationDuration);
            if (optionButton != null)
            {
                optionButton.GetViewGameObject().transform.SetParent(optionButtonContainer.transform);
                optionButton.GetViewGameObject().transform.localPosition = optionButtonOffset;
                optionButton.SetImageData(cameraReelResponse);
                optionButton.GetViewGameObject().SetActive(true);
            }
            outline.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            transform.DOScale(Vector3.one, scaleAnimationDuration);
            if (optionButton != null)
            {
                optionButton.ResetViewState();
                optionButton.GetViewGameObject().SetActive(false);
            }
            outline.SetActive(false);
        }

        public void Release()
        {
            OnThumbnailLoaded = null;
            OnThumbnailClicked = null;
            button.onClick.RemoveAllListeners();
            outline.SetActive(false);
        }
    }
}
