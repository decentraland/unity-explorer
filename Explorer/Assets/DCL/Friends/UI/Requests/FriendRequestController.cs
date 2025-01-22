using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.UI;
using DCL.Web3;
using DCL.Web3.Identities;
using DCL.WebRequests;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.Friends.UI.Requests
{
    public class FriendRequestController : ControllerBase<FriendRequestView, FriendRequestParams>
    {
        private readonly IWeb3IdentityCache identityCache;
        private readonly IFriendsService friendsService;
        private readonly IProfileRepository profileRepository;
        private readonly IWebRequestController webRequestController;
        private readonly List<GameObject> mutualFriendThumbnailObjects = new ();
        private CancellationTokenSource? requestOperationCancellationToken;
        private CancellationTokenSource? fetchUserCancellationToken;
        private CancellationTokenSource? showPreCancelToastCancellationToken;
        private UniTaskCompletionSource? lifeCycleTask;
        private ImageController? userThumbnailSendNew;
        private ImageController? userThumbnailCancel;
        private ImageController? userThumbnailReceived;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        public FriendRequestController(ViewFactoryMethod viewFactory,
            IWeb3IdentityCache identityCache,
            IFriendsService friendsService,
            IProfileRepository profileRepository,
            IWebRequestController webRequestController) : base(viewFactory)
        {
            this.identityCache = identityCache;
            this.friendsService = friendsService;
            this.profileRepository = profileRepository;
            this.webRequestController = webRequestController;
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
            viewInstance.send.MessageInput.onValueChanged.AddListener(UpdateBodyMessageCharacterCount);
            userThumbnailSendNew = new ImageController(viewInstance.send.UserAndMutualFriendsConfig.UserThumbnail, webRequestController);

            viewInstance.cancel.PreCancelButton.onClick.AddListener(ShowPreCancelToastAndEnableCancelButton);
            viewInstance.cancel.CancelButton.onClick.AddListener(Cancel);
            viewInstance.cancel.BackButton.onClick.AddListener(Close);
            userThumbnailCancel = new ImageController(viewInstance.cancel.UserAndMutualFriendsConfig.UserThumbnail, webRequestController);

            viewInstance.received.BackButton.onClick.AddListener(Close);
            viewInstance.received.AcceptButton.onClick.AddListener(Accept);
            viewInstance.received.RejectButton.onClick.AddListener(Reject);
            userThumbnailReceived = new ImageController(viewInstance.received.UserAndMutualFriendsConfig.UserThumbnail, webRequestController);
        }

        protected override void OnViewShow()
        {
            FriendRequest? fr = inputData.Request;

            Web3Address selfAddress = identityCache.EnsuredIdentity().Address;

            if (fr == null)
            {
                if (inputData.DestinationUser == null)
                    throw new Exception("Destination user must be set for new friend request");

                Toggle(ViewState.SEND_NEW);
                SetUpAsNew();
            }
            else
            {
                if (selfAddress == fr.From)
                {
                    Toggle(ViewState.CANCEL);
                    SetUpAsCancel();
                }
                else if (selfAddress == fr.To)
                {
                    Toggle(ViewState.RECEIVED);
                    SetUpAsReceived();
                }
            }
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();

            requestOperationCancellationToken?.SafeCancelAndDispose();
        }

        private void Close()
        {
            lifeCycleTask?.TrySetResult();
        }

        private void Toggle(ViewState state)
        {
            viewInstance!.send.Root.SetActive(state == ViewState.SEND_NEW);
            viewInstance.cancel.Root.SetActive(state == ViewState.CANCEL);
            viewInstance.received.Root.SetActive(state == ViewState.RECEIVED);
        }

        private void SetUpAsNew()
        {
            viewInstance!.send.MessageInput.text = "";

            fetchUserCancellationToken = fetchUserCancellationToken.SafeRestart();
            FetchUserDataAsync(viewInstance.send.UserAndMutualFriendsConfig,
                    inputData.DestinationUser!.Value,
                    userThumbnailSendNew!,
                    fetchUserCancellationToken.Token)
               .Forget();
        }

        private void SetUpAsCancel()
        {
            var fr = inputData.Request!;
            FriendRequestView.CancelConfig viewConfig = viewInstance!.cancel;
            viewConfig.PreCancelToastContainer.SetActive(false);
            viewConfig.PreCancelButton.gameObject.SetActive(true);
            viewConfig.CancelButton.gameObject.SetActive(false);
            viewConfig.TimestampText.text = fr.Timestamp.ToString("M");
            viewConfig.MessageInputContainer.SetActive(!string.IsNullOrEmpty(fr.MessageBody));
            viewConfig.MessageInput.text = $"<b>You:</b> {fr.MessageBody}";

            fetchUserCancellationToken = fetchUserCancellationToken.SafeRestart();
            FetchUserDataAsync(viewConfig.UserAndMutualFriendsConfig, new Web3Address(fr.To), userThumbnailCancel!, fetchUserCancellationToken.Token)
               .Forget();
        }

        private void SetUpAsReceived()
        {
            var fr = inputData.Request!;
            FriendRequestView.ReceivedConfig viewConfig = viewInstance!.received;

            viewConfig.MessageInputContainer.SetActive(!string.IsNullOrEmpty(fr.MessageBody));
            viewConfig.MessageInput.text = fr.MessageBody;
            viewConfig.TimestampText.text = fr.Timestamp.ToString("M");

            fetchUserCancellationToken = fetchUserCancellationToken.SafeRestart();
            FetchUserDataAsync(viewConfig.UserAndMutualFriendsConfig, new Web3Address(fr.From), userThumbnailReceived!, fetchUserCancellationToken.Token)
               .Forget();
        }

        private async UniTaskVoid FetchUserDataAsync(FriendRequestView.UserAndMutualFriendsConfig config, Web3Address user,
            ImageController thumbnailController, CancellationToken ct)
        {
            await UniTask.WhenAll(
                LoadMutualFriendsAsync(),
                LoadUserAsync());

            return;

            async UniTask LoadMutualFriendsAsync()
            {
                config.MutualThumbnailTemplate.SetActive(false);

                PaginatedFriendsResult mutualFriendsResult = await friendsService.GetMutualFriendsAsync(user, 1, 3, ct);

                config.MutualContainer.SetActive(mutualFriendsResult.Friends.Count > 0);
                config.MutalCountText.text = $"{mutualFriendsResult.TotalAmount} Mutual";

                foreach (GameObject thumbnail in mutualFriendThumbnailObjects)
                    Object.Destroy(thumbnail);

                mutualFriendThumbnailObjects.Clear();

                foreach (Profile mutualFriend in mutualFriendsResult.Friends)
                {
                    GameObject go = Object.Instantiate(config.MutualThumbnailTemplate, config.MutualThumbnailTemplate.transform.parent);
                    mutualFriendThumbnailObjects.Add(go);
                    go.SetActive(true);

                    ImageView view = go.GetComponentInChildren<ImageView>();
                    var controller = new ImageController(view, webRequestController);
                    LoadThumbnail(mutualFriend, view, controller);
                }
            }

            async UniTask LoadUserAsync()
            {
                Profile? profile = await profileRepository.GetAsync(user, ct);

                if (profile == null) return;

                config.UserName.text = profile.Name;
                config.UserNameVerification.SetActive(profile.HasClaimedName);
                config.UserNameHash.gameObject.SetActive(!profile.HasClaimedName);
                config.UserNameHash.text = user.ToString()[^4..];

                LoadThumbnail(profile, config.UserThumbnail, thumbnailController);
            }
        }

        private void Send()
        {
            requestOperationCancellationToken = requestOperationCancellationToken.SafeRestart();
            SendAsync(requestOperationCancellationToken.Token).Forget();

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

        private void Reject()
        {
            requestOperationCancellationToken = requestOperationCancellationToken.SafeRestart();
            RejectThenCloseAsync(requestOperationCancellationToken.Token).Forget();
            return;

            async UniTaskVoid RejectThenCloseAsync(CancellationToken ct)
            {
                await friendsService.RejectFriendshipAsync(inputData.Request!.From, ct);

                Close();
            }
        }

        private void Accept()
        {
            requestOperationCancellationToken = requestOperationCancellationToken.SafeRestart();
            AcceptThenCloseAsync(requestOperationCancellationToken.Token).Forget();
            return;

            async UniTaskVoid AcceptThenCloseAsync(CancellationToken ct)
            {
                await friendsService.AcceptFriendshipAsync(inputData.Request!.From, ct);

                Close();
            }
        }

        private void Cancel()
        {
            requestOperationCancellationToken = requestOperationCancellationToken.SafeRestart();
            CancelThenCloseAsync(requestOperationCancellationToken.Token).Forget();

            async UniTaskVoid CancelThenCloseAsync(CancellationToken ct)
            {
                await friendsService.CancelFriendshipAsync(inputData.Request!.To, ct);

                Close();
            }
        }

        private void ShowPreCancelToastAndEnableCancelButton()
        {
            viewInstance!.cancel.PreCancelButton.gameObject.SetActive(false);
            viewInstance.cancel.CancelButton.gameObject.SetActive(true);

            showPreCancelToastCancellationToken = showPreCancelToastCancellationToken.SafeRestart();
            ShowToastThenHideAsync(showPreCancelToastCancellationToken.Token).Forget();
            return;

            async UniTaskVoid ShowToastThenHideAsync(CancellationToken ct)
            {
                viewInstance!.cancel.PreCancelToastContainer.SetActive(true);
                await UniTask.Delay(3000, cancellationToken: ct);
                viewInstance.cancel.PreCancelToastContainer.SetActive(false);
            }
        }

        private void UpdateBodyMessageCharacterCount(string text) =>
            viewInstance!.send.MessageCharacterCountText.text = $"{text.Length}/140";

        private void LoadThumbnail(Profile profile, ImageView imageView, ImageController controller)
        {
            if (profile.ProfilePicture != null)
            {
                Sprite? sprite = profile.ProfilePicture?.Asset.Sprite;

                if (sprite != null)
                    imageView.SetImage(sprite);
            }
            else
                controller.RequestImage(profile.Avatar.FaceSnapshotUrl);
        }

        private enum ViewState
        {
            SEND_NEW,
            CANCEL,
            RECEIVED,
        }
    }
}
