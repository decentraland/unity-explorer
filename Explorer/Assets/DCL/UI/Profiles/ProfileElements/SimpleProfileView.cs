using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.Web3;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.UI.ProfileElements
{
    public class SimpleProfileView : MonoBehaviour
    {
        private static readonly Vector2 CONTEXT_MENU_OFFSET = new (0, -20);

        public Action ProfileContextMenuOpened;
        public Action ProfileContextMenuClosed;

        [SerializeField] private ProfilePictureView profilePictureView;
        [SerializeField] private Button openProfileButton;
        [SerializeField] private SimpleUserNameElement userNameElement;

        [Header("Connection indicator")]
        [SerializeField] private Image connectionStatusIndicator;
        [SerializeField] private GameObject connectionStatusIndicatorContainer;
        [SerializeField] private OnlineStatusConfiguration onlineStatusConfiguration;

        [Range(0.0f, 1.0f)]
        [SerializeField]
        private float offlineThumbnailGreyOutOpacity = 0.6f;

        private Web3Address currentWalledId;
        private CancellationTokenSource cts;
        private UniTaskCompletionSource contextMenuTask = new ();
        private ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly ReactiveProperty<ProfileThumbnailViewModel> thumbnail = new (ProfileThumbnailViewModel.Default());
        private CancellationTokenSource? thumbnailCts;

        public async UniTaskVoid SetupAsync(Web3Address playerId, ProfileRepositoryWrapper profileDataProvider, CancellationToken ct)
        {
            this.profileRepositoryWrapper = profileDataProvider;
            currentWalledId = new Web3Address("");
            Profile.CompactInfo? profile = await profileRepositoryWrapper.GetProfileAsync(playerId, ct).SuppressAnyExceptionWithFallback(null);

            connectionStatusIndicatorContainer.gameObject.SetActive(profile != null);

            if (profile == null) return;

            currentWalledId = playerId;
            userNameElement.Setup(profile.Value);
            thumbnail.UpdateValue(ProfileThumbnailViewModel.Default(profile.Value.UserNameColor));
            profilePictureView.Bind(thumbnail);
            thumbnailCts = thumbnailCts.SafeRestart();
            await GetProfileThumbnailCommand.Instance.ExecuteAsync(thumbnail, null, profile.Value, thumbnailCts.Token);
        }

        private void Awake()
        {
            openProfileButton.onClick.AddListener(OnOpenProfileClicked);
        }

        /// <summary>
        /// Changes the visual aspect of the connection status indicator.
        /// </summary>
        /// <param name="connectionStatus">The current connection status.</param>
        public void SetConnectionStatus(OnlineStatus connectionStatus)
        {
            connectionStatusIndicator.color = onlineStatusConfiguration.GetConfiguration(connectionStatus).StatusColor;
            connectionStatusIndicatorContainer.gameObject.SetActive(connectionStatus == OnlineStatus.Online);
            profilePictureView.GreyOut(connectionStatus != OnlineStatus.Online ? offlineThumbnailGreyOutOpacity : 0.0f);
        }

        private void OnOpenProfileClicked()
        {
            if (currentWalledId == "") return;

            contextMenuTask.TrySetResult();
            contextMenuTask = new UniTaskCompletionSource();
            cts = cts.SafeRestart();
            ProfileContextMenuOpened?.Invoke();
            openProfileButton.OnSelect(null);
            ViewDependencies.GlobalUIViews.ShowUserProfileContextMenuFromWalletIdAsync(currentWalledId, openProfileButton.transform.position, CONTEXT_MENU_OFFSET, cts.Token, contextMenuTask.Task, OnProfileContextMenuClosed, MenuAnchorPoint.TopLeft).Forget();
        }

        private void OnProfileContextMenuClosed()
        {
            ProfileContextMenuClosed?.Invoke();
            openProfileButton.OnDeselect(null);
        }

        private void OnDisable()
        {
            contextMenuTask?.TrySetResult();
        }

        private void OnDestroy()
        {
            thumbnailCts.SafeCancelAndDispose();
        }
    }
}
