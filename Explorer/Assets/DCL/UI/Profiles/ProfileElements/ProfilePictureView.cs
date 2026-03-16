using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utility;

namespace DCL.UI.ProfileElements
{
    public class ProfilePictureView : MonoBehaviour, IDisposable, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private ImageView thumbnailImageView;
        [SerializeField] private Image thumbnailBackground;
        [SerializeField] private Sprite defaultEmptyThumbnail;
        [SerializeField] private Image thumbnailFrame;

        private IDisposable? binding;
        private CancellationTokenSource? cts;
        private string? currentUrl;

        private float greyOutOpacity;

        private bool originalColorsInitialized;
        private Color originalThumbnailBackgroundColor;
        private Color originalThumbnailFrameColor;

        private Color originalThumbnailImageColor;
        private Action? contextMenuAction;
        private string? userAddress;

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

            viewModelProp.UpdateValue(viewModelProp.Value.SetProfile(viewModelProp.Value.Thumbnail.TryBind()));

            OnThumbnailWithColorUpdated(viewModelProp.Value);
            binding = viewModelProp.Subscribe(OnThumbnailWithColorUpdated);
        }

        public void Bind(IReactiveProperty<ProfileThumbnailViewModel> viewModelProp, Color userNameColor)
        {
            // Unbind previous binding if exists
            binding?.Dispose();

            viewModelProp.UpdateValue(viewModelProp.Value.TryBind());

            SetBaseBackgroundColor(userNameColor);

            OnThumbnailUpdated(viewModelProp.Value);
            binding = viewModelProp.Subscribe(OnThumbnailUpdated);
        }

        private void OnThumbnailWithColorUpdated(ProfileThumbnailViewModel.WithColor model)
        {
            SetBaseBackgroundColor(model.ProfileColor);
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
                    thumbnailImageView.SetImage(model.Sprite!, model.FitAndCenterImage);
                    SetLoadingState(false);
                    thumbnailImageView.Alpha = 1f;
                    break;
                case ProfileThumbnailViewModel.State.LOADED_REMOTELY:
                    SetThumbnailImageWithAnimationAsync(model.Sprite!, destroyCancellationToken, model.FitAndCenterImage).Forget();
                    break;
                default:
                    thumbnailImageView.SetImage(defaultEmptyThumbnail);
                    SetLoadingState(false);
                    thumbnailImageView.Alpha = 1f;
                    break;
            }
        }

        [Obsolete("Use " + nameof(Bind) + " instead.")]
        public async UniTask SetupAsync(ProfileRepositoryWrapper profileDataProvider, Color userColor, string? faceSnapshotUrl, string _, CancellationToken ct,
            bool rethrowError = false)
        {
            profileRepositoryWrapper = profileDataProvider;
            SetBackgroundColor(userColor);
            await LoadThumbnailAsync(faceSnapshotUrl, rethrowError, ct);
        }

        [Obsolete("Use " + nameof(Bind) + " instead.")]
        public void Setup(ProfileRepositoryWrapper profileDataProvider, Color userColor, string? faceSnapshotUrl, string _ = "")
        {
            profileRepositoryWrapper = profileDataProvider;
            SetBackgroundColor(userColor);
            LoadThumbnailAsync(faceSnapshotUrl, false).Forget();
        }

        [Obsolete("Use " + nameof(Bind) + " instead.")]
        public void SetImage(Sprite image)
        {
            thumbnailImageView.SetImage(image);
            SetLoadingState(false);
        }

        [Obsolete("Use " + nameof(Bind) + " instead.")]
        public void SetBackgroundColor(Color userColor)
        {
            SetBaseBackgroundColor(userColor);
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

        private async UniTask SetThumbnailImageWithAnimationAsync(Sprite sprite, CancellationToken ct, bool fitAndCenterImage = false)
        {
            thumbnailImageView.SetImage(sprite, fitAndCenterImage);
            thumbnailImageView.ImageEnabled = true;
            await thumbnailImageView.FadeInAsync(0.5f, ct);
        }

        private async UniTask LoadThumbnailAsync(string? faceSnapshotUrl, bool rethrowError, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(faceSnapshotUrl)) return;
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
                currentUrl = null;
                await SetThumbnailImageWithAnimationAsync(defaultEmptyThumbnail, cts.Token);

                if (rethrowError)
                    throw;

                ReportHub.LogError(ReportCategory.UI, e.Message + e.StackTrace);
            }
        }

        private void SetBaseBackgroundColor(Color newBaseColor)
        {
            originalThumbnailBackgroundColor = newBaseColor;
            GreyOut(greyOutOpacity);
        }

        public void GreyOut(float opacity)
        {
            greyOutOpacity = opacity;

            InitializeOriginalColors();

            if (thumbnailImageView != null)
                thumbnailImageView.ImageColor = Color.Lerp(originalThumbnailImageColor, new Color(0.0f, 0.0f, 0.0f, originalThumbnailImageColor.a), opacity);

            if (thumbnailBackground != null)
                thumbnailBackground.color = Color.Lerp(originalThumbnailBackgroundColor, new Color(0.0f, 0.0f, 0.0f, originalThumbnailBackgroundColor.a), opacity);

            if (thumbnailFrame != null)
                thumbnailFrame.color = Color.Lerp(originalThumbnailFrameColor, new Color(0.0f, 0.0f, 0.0f, originalThumbnailFrameColor.a), opacity);
        }

        private void InitializeOriginalColors()
        {
            if (originalColorsInitialized)
                return;

            if (thumbnailImageView != null)
                originalThumbnailImageColor = thumbnailImageView.ImageColor;

            if (thumbnailFrame != null)
                originalThumbnailFrameColor = thumbnailFrame.color;

            originalColorsInitialized = true;
        }

        /// <summary>
        ///    Configure the data needed to handle clicks on the profile picture thumbnail.
        ///    Left click will open the passport of the user with the provided address.
        ///    Right click will invoke the provided context menu action.
        ///
        ///    IMPORTANT: enable raycast target on all ProfilePictureView prefab Image chain (from root to loaded thumbnail) to receive the click events.
        ///               It is disabled by default to prevent the click event consumption where ProfilePictureView is used as a non-interactive element.
        /// </summary>
        public void ConfigureThumbnailClickData(Action? contextMenuAction = null,
            string? userAddress = null)
        {
            this.contextMenuAction = contextMenuAction;
            this.userAddress = userAddress;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && userAddress != null)
                ViewDependencies.GlobalUIViews.OpenPassportAsync(userAddress!).Forget();
            else if (eventData.button == PointerEventData.InputButton.Right)
                contextMenuAction?.Invoke();
        }
    }
}
