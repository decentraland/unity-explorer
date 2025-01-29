using Cysharp.Threading.Tasks;
using DCL.Input;
using DCL.Input.Component;
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

namespace DCL.Friends.UI.Requests
{
    public class FriendRequestController : ControllerBase<FriendRequestView, FriendRequestParams>
    {
        private const int MUTUAL_PAGE_SIZE_BY_DESIGN = 3;
        private const int OPERATION_CONFIRMED_WAIT_TIME_MS = 5000;

        private readonly IWeb3IdentityCache identityCache;
        private readonly IFriendsService friendsService;
        private readonly IProfileRepository profileRepository;
        private readonly IWebRequestController webRequestController;
        private readonly IInputBlock inputBlock;
        private readonly Dictionary<ImageView, ImageController> mutualFriendControllers = new ();
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
            IWebRequestController webRequestController,
            IInputBlock inputBlock) : base(viewFactory)
        {
            this.identityCache = identityCache;
            this.friendsService = friendsService;
            this.profileRepository = profileRepository;
            this.webRequestController = webRequestController;
            this.inputBlock = inputBlock;
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

            InstantiateMutualThumbnailControllers(viewInstance.received.UserAndMutualFriendsConfig);
            InstantiateMutualThumbnailControllers(viewInstance.cancel.UserAndMutualFriendsConfig);
            InstantiateMutualThumbnailControllers(viewInstance.send.UserAndMutualFriendsConfig);
            return;

            void InstantiateMutualThumbnailControllers(FriendRequestView.UserAndMutualFriendsConfig config)
            {
                for (var i = 0; i < config.MutualThumbnails.Length; i++)
                {
                    ImageView view = config.MutualThumbnails[i].Image;
                    var controller = new ImageController(view, webRequestController);
                    mutualFriendControllers[view] = controller;
                }
            }
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
                if (selfAddress == fr.From.Address)
                {
                    Toggle(ViewState.CANCEL);
                    SetUpAsCancel();
                }
                else if (selfAddress == fr.To.Address)
                {
                    Toggle(ViewState.RECEIVED);
                    SetUpAsReceived();
                }
            }

            BlockUnwantedInputs();
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();

            requestOperationCancellationToken?.SafeCancelAndDispose();
            UnblockUnwantedInputs();
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
            viewInstance.cancelledConfirmed.Root.SetActive(state == ViewState.CONFIRMED_CANCELLED);
            viewInstance.acceptedConfirmed.Root.SetActive(state == ViewState.CONFIRMED_ACCEPTED);
            viewInstance.rejectedConfirmed.Root.SetActive(state == ViewState.CONFIRMED_REJECTED);
            viewInstance.sentConfirmed.Root.SetActive(state == ViewState.CONFIRMED_SENT);
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
            FetchUserDataAsync(viewConfig.UserAndMutualFriendsConfig, fr.To, userThumbnailCancel!, fetchUserCancellationToken.Token)
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
            LoadUserAndUpdateMessageWithUserNameAsync(fetchUserCancellationToken.Token).Forget();
            return;

            async UniTaskVoid LoadUserAndUpdateMessageWithUserNameAsync(CancellationToken ct)
            {
                await FetchUserDataAsync(viewConfig.UserAndMutualFriendsConfig,
                    fr.From,
                    userThumbnailReceived!,
                    ct);

                viewConfig.MessageInput.text = $"<b>{fr.From.Name}:</b> {fr.MessageBody}";
            }
        }

        private async UniTask FetchUserDataAsync(FriendRequestView.UserAndMutualFriendsConfig config,
            Web3Address user, ImageController thumbnailController, CancellationToken ct)
        {
            await UniTask.WhenAll(
                LoadMutualFriendsAsync(config, user, ct),
                LoadUserAsync());

            return;

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

        private async UniTask FetchUserDataAsync(FriendRequestView.UserAndMutualFriendsConfig config,
            FriendProfile user, ImageController thumbnailController, CancellationToken ct)
        {
            config.UserName.text = user.Name;
            config.UserNameVerification.SetActive(user.HasClaimedName);
            config.UserNameHash.gameObject.SetActive(!user.HasClaimedName);
            config.UserNameHash.text = user.Address.ToString()[^4..];
            thumbnailController.RequestImage(user.FacePictureUrl);

            await LoadMutualFriendsAsync(config, user.Address, ct);
        }

        private async UniTask LoadMutualFriendsAsync(FriendRequestView.UserAndMutualFriendsConfig config,
            Web3Address user, CancellationToken ct)
        {
            foreach (FriendRequestView.UserAndMutualFriendsConfig.MutualThumbnail thumbnail in config.MutualThumbnails)
                thumbnail.Root.SetActive(false);

            config.MutualContainer.SetActive(false);

            // We only request the first page so we show a couple of mutual thumbnails. This is by design
            PaginatedFriendsResult mutualFriendsResult = await friendsService.GetMutualFriendsAsync(user, 0, MUTUAL_PAGE_SIZE_BY_DESIGN, ct);

            config.MutualContainer.SetActive(mutualFriendsResult.Friends.Count > 0);
            config.MutalCountText.text = $"{mutualFriendsResult.TotalAmount} Mutual";

            FriendRequestView.UserAndMutualFriendsConfig.MutualThumbnail[] mutualConfig = config.MutualThumbnails;

            for (var i = 0; i < mutualConfig.Length; i++)
            {
                bool friendExists = i < mutualFriendsResult.Friends.Count;
                mutualConfig[i].Root.SetActive(friendExists);
                if (!friendExists) continue;
                FriendProfile mutualFriend = mutualFriendsResult.Friends[i];
                ImageView view = mutualConfig[i].Image;
                mutualFriendControllers[view].RequestImage(mutualFriend.FacePictureUrl);
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

                    Toggle(ViewState.CONFIRMED_SENT);

                    await ShowOperationConfirmationAsync(viewInstance.sentConfirmed, inputData.DestinationUser!.Value,
                        "Friend Request Sent To <color=#73D3D3>{0}</color>",
                        ct);

                    Close();
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
                await friendsService.RejectFriendshipAsync(inputData.Request!.From.Address, ct);

                Toggle(ViewState.CONFIRMED_REJECTED);

                await ShowOperationConfirmationAsync(viewInstance!.rejectedConfirmed, inputData.Request.From.Address,
                    "Friend Request From <color=#FF8362>{0}</color> Rejected",
                    ct);

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
                await friendsService.AcceptFriendshipAsync(inputData.Request!.From.Address, ct);

                Toggle(ViewState.CONFIRMED_ACCEPTED);

                await ShowOperationConfirmationAsync(viewInstance!.rejectedConfirmed, inputData.Request.From.Address,
                    "You And <color=#FF8362>{0}</color> Are Now Friends!",
                    ct);

                Close();
            }
        }

        private void Cancel()
        {
            requestOperationCancellationToken = requestOperationCancellationToken.SafeRestart();
            CancelThenCloseAsync(requestOperationCancellationToken.Token).Forget();
            return;

            async UniTaskVoid CancelThenCloseAsync(CancellationToken ct)
            {
                await friendsService.CancelFriendshipAsync(inputData.Request!.To.Address, ct);

                Toggle(ViewState.CONFIRMED_CANCELLED);

                await ShowOperationConfirmationAsync(viewInstance!.rejectedConfirmed, inputData.Request.From.Address,
                    "Friend Request To <color=#73D3D3>{0}</color> Cancelled",
                    ct);

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

        private void BlockUnwantedInputs() =>
            inputBlock.Disable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.IN_WORLD_CAMERA, InputMapComponent.Kind.CAMERA, InputMapComponent.Kind.PLAYER);

        private void UnblockUnwantedInputs() =>
            inputBlock.Enable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.IN_WORLD_CAMERA, InputMapComponent.Kind.CAMERA, InputMapComponent.Kind.PLAYER);

        private async UniTask ShowOperationConfirmationAsync(FriendRequestView.OperationConfirmedConfig config,
            Web3Address userId, string textWithUserNameParam, CancellationToken ct)
        {
            Profile? profile = await profileRepository.GetAsync(userId, ct);
            if (profile == null) return;

            config.Label.text = string.Format(textWithUserNameParam, profile.Name);

            LoadThumbnail(profile, config.FriendThumbnail, new ImageController(config.FriendThumbnail, webRequestController));

            if (config.MyThumbnail != null)
            {
                Profile? myProfile = await profileRepository.GetAsync(identityCache.EnsuredIdentity().Address, ct);

                if (myProfile != null)
                    LoadThumbnail(myProfile, config.MyThumbnail, new ImageController(config.MyThumbnail, webRequestController));
            }

            await UniTask.WhenAny(config.CloseButton.OnClickAsync(ct), UniTask.Delay(OPERATION_CONFIRMED_WAIT_TIME_MS, cancellationToken: ct));
        }

        private enum ViewState
        {
            SEND_NEW,
            CANCEL,
            RECEIVED,
            CONFIRMED_ACCEPTED,
            CONFIRMED_CANCELLED,
            CONFIRMED_REJECTED,
            CONFIRMED_SENT,
        }
    }
}
