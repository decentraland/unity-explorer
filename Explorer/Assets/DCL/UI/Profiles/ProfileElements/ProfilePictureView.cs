using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.UI.Profiles.Helpers;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utility;

namespace DCL.UI.ProfileElements
{
    public class ProfilePictureView : MonoBehaviour, IDisposable, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action? PointerEnter;
        public event Action? PointerExit;

        [SerializeField] private ImageView thumbnailImageView;
        [SerializeField] private Image thumbnailBackground;
        [SerializeField] private Sprite defaultEmptyThumbnail;
        [SerializeField] private Image thumbnailFrame;

        private ProfileRepositoryWrapper profileRepositoryWrapper;
        private CancellationTokenSource? cts;
        private string? currentUrl;

        private Color originalThumbnailImageColor;
        private Color originalThumbnailBackgroundColor;
        private Color originalThumbnailFrameColor;

        private bool isColorInitialized;
        private float greyOutOpacity;

        private void Awake()
        {
            if (!isColorInitialized)
            {
                if (thumbnailImageView != null)
                    originalThumbnailImageColor = thumbnailImageView.ImageColor;

                if(thumbnailFrame != null)
                    originalThumbnailFrameColor = thumbnailFrame.color;

                isColorInitialized = true;
            }

            GreyOut(greyOutOpacity);
        }

        public void Dispose()
        {
            cts.SafeCancelAndDispose();
        }

        public async UniTask SetupAsync(ProfileRepositoryWrapper profileDataProvider, Color userColor, string? faceSnapshotUrl, string _, CancellationToken ct)
        {
            this.profileRepositoryWrapper = profileDataProvider;
            SetupOnlyColor(userColor);

            await LoadThumbnailAsync(faceSnapshotUrl, ct);
        }

        public void Setup(ProfileRepositoryWrapper profileDataProvider, Color userColor, string faceSnapshotUrl, string _="")
        {
            this.profileRepositoryWrapper = profileDataProvider;
            SetupOnlyColor(userColor);
            LoadThumbnailAsync(faceSnapshotUrl).Forget();
        }

        public void SetupOnlyColor(Color userColor)
        {
            if (!isColorInitialized)
            {
                if (thumbnailImageView != null)
                    originalThumbnailImageColor = thumbnailImageView.ImageColor;

                if(thumbnailFrame != null)
                    originalThumbnailFrameColor = thumbnailFrame.color;

                isColorInitialized = true;
            }

            originalThumbnailBackgroundColor = userColor;

            GreyOut(greyOutOpacity);
        }

        public void SetLoadingState(bool isLoading)
        {
            thumbnailImageView.IsLoading = isLoading;
            thumbnailImageView.ImageEnabled = !isLoading;
        }

        public void SetDefaultThumbnail()
        {
            thumbnailImageView.SetImage(defaultEmptyThumbnail, true);
            currentUrl = null;
        }

        private async UniTask SetThumbnailImageWithAnimationAsync(Sprite sprite, CancellationToken ct)
        {
            thumbnailImageView.SetImage(sprite, true);
            thumbnailImageView.ImageEnabled = true;
            await thumbnailImageView.FadeInAsync(0.5f, ct);
        }

        private async UniTask LoadThumbnailAsync(string? faceSnapshotUrl, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(faceSnapshotUrl)) return;
            if (faceSnapshotUrl.Equals(currentUrl)) return;

            cts = ct != default ? cts.SafeRestartLinked(ct) : cts.SafeRestart();
            currentUrl = faceSnapshotUrl;

            try
            {
                ct.ThrowIfCancellationRequested();

                Sprite? sprite = profileRepositoryWrapper.GetProfileThumbnail(faceSnapshotUrl);

                if (sprite != null)
                {
                    thumbnailImageView.SetImage(sprite, true);
                    SetLoadingState(false);
                    thumbnailImageView.Alpha = 1f;
                    return;
                }

                SetLoadingState(true);
                thumbnailImageView.Alpha = 0f;

                sprite = await profileRepositoryWrapper.GetProfileThumbnailAsync(faceSnapshotUrl, cts.Token);

                if (sprite == null)
                    currentUrl = null;

                await SetThumbnailImageWithAnimationAsync(sprite ? sprite! : defaultEmptyThumbnail, cts.Token);
            }
            catch (OperationCanceledException)
            {
                currentUrl = null;
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.UI);

                currentUrl = null;
                await SetThumbnailImageWithAnimationAsync(defaultEmptyThumbnail, cts.Token);
            }
        }

        public void OnPointerEnter(PointerEventData eventData) =>
            PointerEnter?.Invoke();

        public void OnPointerExit(PointerEventData eventData) =>
            PointerExit?.Invoke();

        public void GreyOut(float opacity)
        {
            if (!isColorInitialized)
            {
                // The method was called before Awake, it stores the value to be applied on Awake later
                greyOutOpacity = opacity;
                return;
            }

            if(thumbnailImageView != null)
                thumbnailImageView.ImageColor = Color.Lerp(originalThumbnailImageColor, new Color(0.0f, 0.0f, 0.0f, originalThumbnailImageColor.a), opacity);

            if(thumbnailBackground != null)
                thumbnailBackground.color = Color.Lerp(originalThumbnailBackgroundColor, new Color(0.0f, 0.0f, 0.0f, originalThumbnailBackgroundColor.a), opacity);

            if(thumbnailFrame != null)
                thumbnailFrame.color = Color.Lerp(originalThumbnailFrameColor, new Color(0.0f, 0.0f, 0.0f, originalThumbnailFrameColor.a), opacity);
        }
    }
}
