using Cysharp.Threading.Tasks;
using DCL.Profiles;
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
        public Action ProfileContextMenuOpened;
        public Action ProfileContextMenuClosed;

        [SerializeField] private ProfilePictureView profilePictureView;
        [SerializeField] private Button openProfileButton;
        [SerializeField] private SimpleUserNameElement userNameElement;

        private ViewDependencies viewDependencies;
        private Web3Address currentWalledId;
        private CancellationTokenSource cts;

        public async UniTaskVoid SetupAsync(Web3Address playerId, CancellationToken ct)
        {
            if (viewDependencies == null) return;

            currentWalledId = new Web3Address("");
            Profile profile = await viewDependencies.GetProfileAsync(playerId, ct);

            if (profile == null) return;

            currentWalledId = playerId;
            userNameElement.Setup(profile);
            await profilePictureView.SetupWithDependenciesAsync(viewDependencies, profile.UserNameColor, profile.Avatar.FaceSnapshotUrl, profile.UserId, ct);
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
            cts = cts.SafeRestart();
            ProfileContextMenuOpened?.Invoke();
            openProfileButton.OnSelect(null);
            viewDependencies.GlobalUIViews.ShowUserProfileContextMenuFromWalletIdAsync(currentWalledId, openProfileButton.transform.position, cts.Token, OnProfileContextMenuClosed).Forget();
        }

        private void OnProfileContextMenuClosed()
        {
            ProfileContextMenuClosed?.Invoke();
            openProfileButton.OnDeselect(null);
        }


    }
}
