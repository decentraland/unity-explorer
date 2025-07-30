using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
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
        [SerializeField] private ImageView thumbnailImageView;
        [SerializeField] private Image thumbnailBackground;
        [SerializeField] private Sprite defaultEmptyThumbnail;
        [SerializeField] private Image thumbnailFrame;

        private IDisposable? binding;
        private CancellationTokenSource? cts;
        private string? currentUrl;

        private Color originalThumbnailImageColor;
        private Color originalThumbnailBackgroundColor;
        private Color originalThumbnailFrameColor;

        private bool initialized;
        private float greyOutOpacity;

        private void Awake()
        {
            if(thumbnailImageView != null)
                originalThumbnailImageColor = thumbnailImageView.ImageColor;

            if(thumbnailFrame != null)
                originalThumbnailFrameColor = thumbnailFrame.color;

            initialized = true;

            GreyOut(greyOutOpacity);
        }

        [Obsolete]
        private ProfileRepositoryWrapper profileRepositoryWrapper;

        public void Dispose()
        {
            cts.SafeCancelAndDispose();

            binding?.Dispose();
        }

        public void OnPointerEnter(PointerEventData eventData) =>
            PointerEnter?.Invoke();

        public void OnPointerExit(PointerEventData eventData) =>
            PointerExit?.Invoke();

        public event Action? PointerEnter;
        public event Action? PointerExit;

        public void Bind(IReactiveProperty<ProfileThumbnailViewModel.WithColor> viewModelProp)
        {
            binding?.Dispose();

            OnThumbnailWithColorUpdated(viewModelProp.Value);
            binding = viewModelProp.Subscribe(OnThumbnailWithColorUpdated);
        }

        public void Bind(IReactiveProperty<ProfileThumbnailViewModel> viewModelProp, Color userNameColor)
        {
            // Unbind previous binding if exists
            binding?.Dispose();

            thumbnailBackground.color = userNameColor;

            OnThumbnailUpdated(viewModelProp.Value);
            binding = viewModelProp.Subscribe(OnThumbnailUpdated);
        }

        private void OnThumbnailWithColorUpdated(ProfileThumbnailViewModel.WithColor model)
        {
            thumbnailBackground.color = model.ProfileColor;
            OnThumbnailUpdated(model.Thumbnail);
        }

        private void OnThumbnailUpdated(ProfileThumbnailViewModel model)
        {
            switch (model.ThumbnailState)
            {
                case ProfileThumbnailViewModel.State.LOADING:
                    SetLoadingState(true);
                    thumbnailImageView.Alpha = 0f;
                    break;
                case ProfileThumbnailViewModel.State.FALLBACK:
                case ProfileThumbnailViewModel.State.LOADED_FROM_CACHE:
                    thumbnailImageView.SetImage(model.Sprite!);
                    SetLoadingState(false);
                    thumbnailImageView.Alpha = 1f;
                    break;
                case ProfileThumbnailViewModel.State.LOADED_REMOTELY:
                    SetThumbnailImageWithAnimationAsync(model.Sprite!, destroyCancellationToken).Forget();
                    break;
                default:
                    thumbnailImageView.SetImage(defaultEmptyThumbnail);
                    SetLoadingState(false);
                    thumbnailImageView.Alpha = 1f;
                    break;
            }
        }

        [Obsolete("Use" + nameof(Bind) + " instead.")]
        public async UniTask SetupAsync(ProfileRepositoryWrapper profileDataProvider, Color userColor, string faceSnapshotUrl, string _, CancellationToken ct)
        {
            profileRepositoryWrapper = profileDataProvider;
            SetBackgroundColor(userColor);
            await LoadThumbnailAsync(faceSnapshotUrl, ct);
        }

        [Obsolete("Use" + nameof(Bind) + " instead.")]
        public void Setup(ProfileRepositoryWrapper profileDataProvider, Color userColor, string faceSnapshotUrl, string _ = "")
        {
            profileRepositoryWrapper = profileDataProvider;
            SetBackgroundColor(userColor);
            LoadThumbnailAsync(faceSnapshotUrl).Forget();
        }

        [Obsolete("Use" + nameof(Bind) + " instead.")]
        public void SetImage(Sprite image)
        {
            thumbnailImageView.SetImage(image);
            SetLoadingState(false);
        }

        [Obsolete("Use" + nameof(Bind) + " instead.")]
        public void SetBackgroundColor(Color userColor)
        {
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
            thumbnailImageView.SetImage(defaultEmptyThumbnail);
            currentUrl = null;
        }

        private async UniTask SetThumbnailImageWithAnimationAsync(Sprite sprite, CancellationToken ct)
        {
            thumbnailImageView.SetImage(sprite);
            thumbnailImageView.ImageEnabled = true;
            await thumbnailImageView.FadeInAsync(0.5f, ct);
        }

        private async UniTask LoadThumbnailAsync(string faceSnapshotUrl, CancellationToken ct = default)
        {
            if (faceSnapshotUrl.Equals(currentUrl)) return;

            cts = ct != default(CancellationToken) ? cts.SafeRestartLinked(ct) : cts.SafeRestart();
            currentUrl = faceSnapshotUrl;

            try
            {
                ct.ThrowIfCancellationRequested();

                Sprite? sprite = profileRepositoryWrapper.GetProfileThumbnail(faceSnapshotUrl);

                if (sprite != null)
                {
                    thumbnailImageView.SetImage(sprite);
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
            catch (OperationCanceledException) { currentUrl = null; }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.UI, e.Message + e.StackTrace);

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
            if (!initialized)
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
