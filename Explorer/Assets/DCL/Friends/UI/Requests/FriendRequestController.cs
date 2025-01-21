using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.Web3;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Threading;
using Utility;

namespace DCL.Friends.UI.Requests
{
    public class FriendRequestController : ControllerBase<FriendRequestView, FriendRequestParams>
    {
        private readonly IWeb3IdentityCache identityCache;
        private readonly IFriendsService friendsService;
        private readonly IProfileRepository profileRepository;
        private CancellationTokenSource? sendCancellationToken;
        private CancellationTokenSource? fetchUserCancellationToken;
        private UniTaskCompletionSource? lifeCycleTask;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        public FriendRequestController(ViewFactoryMethod viewFactory,
            IWeb3IdentityCache identityCache,
            IFriendsService friendsService,
            IProfileRepository profileRepository) : base(viewFactory)
        {
            this.identityCache = identityCache;
            this.friendsService = friendsService;
            this.profileRepository = profileRepository;
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            lifeCycleTask = new UniTaskCompletionSource();
            await lifeCycleTask.Task;
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance!.send.SendButton.onClick.AddListener(Send);
            viewInstance.send.CancelButton.onClick.AddListener(Close);
        }

        protected override void OnViewShow()
        {
            FriendRequest? fr = inputData.Request;

            Web3Address selfAddress = identityCache.EnsuredIdentity().Address;

            if (fr == null)
            {
                if (inputData.DestinationUser == null)
                    throw new Exception("Destination user must be set for new friend request");

                viewInstance!.send.Root.SetActive(true);
                viewInstance.cancel.Root.SetActive(false);
                viewInstance.receiveWithMessage.Config.Root.SetActive(false);
                viewInstance.receiveWithoutMessage.Root.SetActive(false);

                fetchUserCancellationToken = fetchUserCancellationToken.SafeRestart();
                FetchUserDataAsync(viewInstance.send.UserAndMutualFriendsConfig, inputData.DestinationUser.Value, fetchUserCancellationToken.Token)
                   .Forget();
            }
            else
            {
                bool hasMessageBody = string.IsNullOrEmpty(fr.MessageBody);

                viewInstance!.send.Root.SetActive(false);
                viewInstance.cancel.Root.SetActive(selfAddress == fr.From);
                viewInstance.receiveWithMessage.Config.Root.SetActive(selfAddress == fr.To && !hasMessageBody);
                viewInstance.receiveWithoutMessage.Root.SetActive(selfAddress == fr.To && hasMessageBody);
            }
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();

            sendCancellationToken?.SafeCancelAndDispose();
        }

        private void Close()
        {
            lifeCycleTask?.TrySetResult();
        }

        private async UniTaskVoid FetchUserDataAsync(FriendRequestView.UserAndMutualFriendsConfig config, Web3Address user, CancellationToken ct)
        {
            // TODO: fetch mutual friends

            Profile? profile = await profileRepository.GetAsync(user, ct);

            if (profile == null) return;

            config.UserName.text = profile.Name;

            if (profile.HasClaimedName)
            {
                // config.
            }
        }

        private void Send()
        {
            sendCancellationToken = sendCancellationToken.SafeRestart();
            SendAsync(sendCancellationToken.Token).Forget();

            async UniTaskVoid SendAsync(CancellationToken ct)
            {
                viewInstance!.send.SendButton.interactable = false;

                try
                {
                    await friendsService.RequestFriendshipAsync(inputData.DestinationUser!.Value,
                        viewInstance.send.MessageInput.text,
                        ct);

                    // TODO: show animation
                }
                finally
                {
                    viewInstance.send.SendButton.interactable = true;
                }
            }
        }
    }
}
