using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.Web3;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.UI.ProfileElements
{
    public class SimpleProfileView : MonoBehaviour, IViewWithGlobalDependencies
    {
        private static readonly Vector2 CONTEXT_MENU_OFFSET = new (0, -20);

        public Action ProfileContextMenuOpened;
        public Action ProfileContextMenuClosed;

        [SerializeField] private ProfilePictureView profilePictureView;
        [SerializeField] private Button openProfileButton;
        [SerializeField] private SimpleUserNameElement userNameElement;

        private ViewDependencies viewDependencies;
        private Web3Address currentWalledId;
        private CancellationTokenSource cts;
        private UniTaskCompletionSource contextMenuTask = new ();
        private ProfileRepositoryWrapper profileRepositoryWrapper;

        public async UniTaskVoid SetupAsync(Web3Address playerId, ProfileRepositoryWrapper profileDataProvider, CancellationToken ct)
        {
            if (viewDependencies == null) return;

            this.profileRepositoryWrapper = profileDataProvider;
            currentWalledId = new Web3Address("");
            Profile profile = await profileRepositoryWrapper.GetProfileAsync(playerId, ct);

            if (profile == null) return;

            currentWalledId = playerId;
            userNameElement.Setup(profile);
            await profilePictureView.SetupAsync(profileRepositoryWrapper, profile.UserNameColor, profile.Avatar.FaceSnapshotUrl, profile.UserId, ct);
        }

        public void InjectDependencies(ViewDependencies dependencies)
        {
            viewDependencies = dependencies;
        }

        private void Awake()
        {
            openProfileButton.onClick.AddListener(OnOpenProfileClicked);
        }

        private void OnOpenProfileClicked()
        {
            if (currentWalledId == "") return;

            contextMenuTask.TrySetResult();
            contextMenuTask = new UniTaskCompletionSource();
            cts = cts.SafeRestart();
            ProfileContextMenuOpened?.Invoke();
            openProfileButton.OnSelect(null);
            viewDependencies.GlobalUIViews.ShowUserProfileContextMenuFromWalletIdAsync(currentWalledId, openProfileButton.transform.position, CONTEXT_MENU_OFFSET, cts.Token, contextMenuTask.Task, OnProfileContextMenuClosed, MenuAnchorPoint.TOP_LEFT).Forget();
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
    }
}
