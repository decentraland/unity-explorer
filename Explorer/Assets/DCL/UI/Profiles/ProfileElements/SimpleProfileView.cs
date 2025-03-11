using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.Web3;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.ProfileElements
{
    public class SimpleProfileView : MonoBehaviour, IViewWithGlobalDependencies
    {
        [SerializeField] private ProfilePictureView profilePictureView;
        [SerializeField] private Button openProfileButton;
        [SerializeField] private SimpleUserNameElement userNameElement;

        private ViewDependencies viewDependencies;

        public async UniTaskVoid SetupAsync(Web3Address playerId, CancellationToken ct)
        {
            if (viewDependencies == null) return;

            Profile profile = await viewDependencies.GetProfileAsync(playerId, ct);

            if (profile == null) return;

            userNameElement.Setup(profile);
            await profilePictureView.SetupWithDependenciesAsync(viewDependencies, profile.UserNameColor, profile.Avatar.FaceSnapshotUrl, profile.UserId, ct);
        }

        public void InjectDependencies(ViewDependencies dependencies)
        {
            viewDependencies = dependencies;
        }
    }
}
