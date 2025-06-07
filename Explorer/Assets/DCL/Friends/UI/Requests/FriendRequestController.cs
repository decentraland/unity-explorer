using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Component;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.UI.ProfileElements;
using DCL.Utilities.Extensions;
using DCL.Web3;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Types;

namespace DCL.Friends.UI.Requests
{
    public class FriendRequestController : ControllerBase<FriendRequestView, FriendRequestParams>
    {
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

        private const int MUTUAL_PAGE_SIZE_BY_DESIGN = 3;
        private const int OPERATION_CONFIRMED_WAIT_TIME_MS = 5000;
        private const string DATE_FORMAT = "MMM dd";
        private const string FRIEND_REQUEST_SENT_FORMAT = "Friend Request Sent To <color={0}>{1}</color>";
        private const string FRIEND_REQUEST_ACCEPTED_FORMAT = "You And <color={0}>{1}</color> Are Now Friends!";

        private readonly IWeb3IdentityCache identityCache;
        private readonly IFriendsService friendsService;
        private readonly IProfileRepository profileRepository;
        private readonly IInputBlock inputBlock;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private CancellationTokenSource? requestOperationCancellationToken;
        private CancellationTokenSource? fetchUserCancellationToken;
        private CancellationTokenSource? showPreCancelToastCancellationToken;
        private UniTaskCompletionSource? lifeCycleTask;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public FriendRequestController(ViewFactoryMethod viewFactory,
            IWeb3IdentityCache identityCache,
            IFriendsService friendsService,
            IProfileRepository profileRepository,
            IInputBlock inputBlock,
            ProfileRepositoryWrapper profileDataProvider) : base(viewFactory)
        {
            this.identityCache = identityCache;
            this.friendsService = friendsService;
            this.profileRepository = profileRepository;
            this.inputBlock = inputBlock;
            this.profileRepositoryWrapper = profileDataProvider;
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
            viewInstance.send.CloseButton.onClick.AddListener(Close);

            viewInstance.cancel.PreCancelButton.onClick.AddListener(ShowPreCancelToastAndEnableCancelButton);
            viewInstance.cancel.CancelButton.onClick.AddListener(Cancel);
            viewInstance.cancel.BackButton.onClick.AddListener(Close);
            viewInstance.cancel.CloseButton.onClick.AddListener(Close);

            viewInstance.received.BackButton.onClick.AddListener(Close);
            viewInstance.received.AcceptButton.onClick.AddListener(() => Accept(inputData.Request!.From));
            viewInstance.received.RejectButton.onClick.AddListener(Reject);
            viewInstance.received.CloseButton.onClick.AddListener(Close);
        }

        protected override void OnViewShow()
        {
            FriendRequest? fr = inputData.Request;

            Web3Address selfAddress = identityCache.EnsuredIdentity().Address;

            if (inputData.OneShotFriendAccepted != null)
                Accept(inputData.OneShotFriendAccepted);
            else if (fr == null)
            {
                if (inputData.DestinationUser == null)
                    throw new Exception("Destination user must be set for new friend request");

                Toggle(ViewState.SEND_NEW);
                SetUpAsNew();
            }
            else
            {
                if (selfAddress.ToString() == fr.From.Address)
                {
                    Toggle(ViewState.CANCEL);
                    SetUpAsCancel();
                }
                else if (selfAddress.ToString() == fr.To.Address)
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

            UniTask.WhenAll(LoadMutualFriendsAsync(viewInstance.send.UserAndMutualFriendsConfig,
                            inputData.DestinationUser!.Value,
                            fetchUserCancellationToken.Token),
                        LoadUserAsync(viewInstance.send.UserAndMutualFriendsConfig,
                            inputData.DestinationUser!.Value,
                            fetchUserCancellationToken.Token))
                   .SuppressToResultAsync(ReportCategory.FRIENDS)
                   .Forget();

            return;

            async UniTask LoadUserAsync(FriendRequestView.UserAndMutualFriendsConfig config, Web3Address user, CancellationToken ct)
            {
                Profile? profile = await profileRepository.GetAsync(user, ct);

                if (profile == null) return;

                config.UserName.text = profile.Name;
                config.UserName.color = profile.UserNameColor;
                config.UserNameVerification.SetActive(profile.HasClaimedName);
                config.UserNameHash.gameObject.SetActive(!profile.HasClaimedName);
                config.UserNameHash.text = $"#{user.ToString()[^4..]}";

                await config.UserThumbnail.SetupAsync(profileRepositoryWrapper, profile.UserNameColor, profile.Avatar.FaceSnapshotUrl, user, ct);
            }
        }

        private void SetUpAsCancel()
        {
            FriendRequest fr = inputData.Request!;
            FriendRequestView.CancelConfig viewConfig = viewInstance!.cancel;
            viewConfig.PreCancelToastContainer.SetActive(false);
            viewConfig.PreCancelButton.gameObject.SetActive(true);
            viewConfig.CancelButton.gameObject.SetActive(false);
            viewConfig.TimestampText.text = fr.Timestamp.ToString(DATE_FORMAT).ToUpper();
            viewConfig.MessageInputContainer.SetActive(!string.IsNullOrEmpty(fr.MessageBody));
            viewConfig.MessageInput.text = $"<b>You:</b> {fr.MessageBody}";
            viewConfig.MessageInput.interactable = false;

            fetchUserCancellationToken = fetchUserCancellationToken.SafeRestart();

            FetchUserDataAsync(viewConfig.UserAndMutualFriendsConfig, fr.To, fetchUserCancellationToken.Token)
               .Forget();
        }

        private void SetUpAsReceived()
        {
            FriendRequest fr = inputData.Request!;
            FriendRequestView.ReceivedConfig viewConfig = viewInstance!.received;

            viewConfig.MessageInputContainer.SetActive(!string.IsNullOrEmpty(fr.MessageBody));
            viewConfig.MessageInput.text = fr.MessageBody;
            viewConfig.TimestampText.text = fr.Timestamp.ToString(DATE_FORMAT).ToUpper();

            fetchUserCancellationToken = fetchUserCancellationToken.SafeRestart();
            LoadUserAndUpdateMessageWithUserNameAsync(fetchUserCancellationToken.Token).Forget();
            return;

            async UniTaskVoid LoadUserAndUpdateMessageWithUserNameAsync(CancellationToken ct)
            {
                await FetchUserDataAsync(viewConfig.UserAndMutualFriendsConfig,
                    fr.From,
                    ct);

                viewConfig.MessageInput.text = $"<b>{fr.From.Name}:</b> {fr.MessageBody}";
                viewConfig.MessageInput.interactable = false;
            }
        }

        private async UniTask FetchUserDataAsync(FriendRequestView.UserAndMutualFriendsConfig config,
            FriendProfile user, CancellationToken ct)
        {
            config.UserName.text = user.Name;
            config.UserName.color = user.UserNameColor;
            config.UserNameVerification.SetActive(user.HasClaimedName);
            config.UserNameHash.gameObject.SetActive(!user.HasClaimedName);
            config.UserNameHash.text = $"#{user.Address.ToString()[^4..]}";

            await UniTask.WhenAll(config.UserThumbnail.SetupAsync(profileRepositoryWrapper, user.UserNameColor, user.FacePictureUrl, user.Address, ct),
                LoadMutualFriendsAsync(config, user.Address, ct));
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
                ProfilePictureView view = mutualConfig[i].Image;
                view.SetupAsync(profileRepositoryWrapper, mutualFriend.UserNameColor, mutualFriend.FacePictureUrl, mutualFriend.Address, ct).Forget();
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
                    var result = await friendsService.RequestFriendshipAsync(inputData.DestinationUser!.Value,
                        viewInstance.send.MessageInput.text,
                        ct)
                                                     .SuppressToResultAsync(ReportCategory.FRIENDS);

                    if (result.Success)
                    {
                        await ShowOperationConfirmationAsync(
                            ViewState.CONFIRMED_SENT,
                            viewInstance.sentConfirmed, result.Value.To,
                            FRIEND_REQUEST_SENT_FORMAT,
                            ct);

                        Close();
                    }
                }
                finally { viewInstance.send.SendButton.interactable = true; }
            }
        }

        private void Reject()
        {
            requestOperationCancellationToken = requestOperationCancellationToken.SafeRestart();
            RejectThenCloseAsync(requestOperationCancellationToken.Token).Forget();
            return;

            async UniTaskVoid RejectThenCloseAsync(CancellationToken ct)
            {
                await friendsService.RejectFriendshipAsync(inputData.Request!.From.Address, ct).SuppressToResultAsync(ReportCategory.FRIENDS);

                // Dont show confirmation on negative actions
                // await ShowOperationConfirmationAsync(
                //     ViewState.CONFIRMED_REJECTED,
                //     viewInstance!.rejectedConfirmed, inputData.Request.From,
                //     "Friend Request From <color=#FF8362>{0}</color> Rejected",
                //     ct);

                Close();
            }
        }

        private void Accept(FriendProfile target)
        {
            requestOperationCancellationToken = requestOperationCancellationToken.SafeRestart();
            AcceptThenCloseAsync(requestOperationCancellationToken.Token).Forget();
            return;

            async UniTaskVoid AcceptThenCloseAsync(CancellationToken ct)
            {
                EnumResult<TaskError> result = await friendsService.AcceptFriendshipAsync(target.Address, ct).SuppressToResultAsync(ReportCategory.FRIENDS);

                if (result.Success)
                {
                    await ShowOperationConfirmationAsync(
                        ViewState.CONFIRMED_ACCEPTED,
                        viewInstance!.acceptedConfirmed, target,
                        FRIEND_REQUEST_ACCEPTED_FORMAT,
                        ct);

                    Close();
                }
            }
        }

        private void Cancel()
        {
            requestOperationCancellationToken = requestOperationCancellationToken.SafeRestart();
            CancelThenCloseAsync(requestOperationCancellationToken.Token).Forget();
            return;

            async UniTaskVoid CancelThenCloseAsync(CancellationToken ct)
            {
                await friendsService.CancelFriendshipAsync(inputData.Request!.To.Address, ct).SuppressToResultAsync(ReportCategory.FRIENDS);

                // Dont show confirmation on negative actions
                // await ShowOperationConfirmationAsync(
                //     ViewState.CONFIRMED_CANCELLED,
                //     viewInstance!.cancelledConfirmed, inputData.Request.To,
                //     "Friend Request To <color=#73D3D3>{0}</color> Cancelled",
                //     ct);

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

        private void BlockUnwantedInputs() =>
            inputBlock.Disable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.IN_WORLD_CAMERA, InputMapComponent.Kind.CAMERA, InputMapComponent.Kind.PLAYER);

        private void UnblockUnwantedInputs() =>
            inputBlock.Enable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.IN_WORLD_CAMERA, InputMapComponent.Kind.CAMERA, InputMapComponent.Kind.PLAYER);

        private async UniTask ShowOperationConfirmationAsync(
            ViewState state,
            FriendRequestView.OperationConfirmedConfig config,
            FriendProfile profile, string textWithUserNameParam, CancellationToken ct)
        {
            config.Label.text = string.Format(textWithUserNameParam, ToHexStr(profile.UserNameColor), profile.Name);
            config.FriendThumbnail.SetupAsync(profileRepositoryWrapper, profile.UserNameColor, profile.FacePictureUrl, profile.Address, ct).Forget();

            if (config.MyThumbnail != null)
            {
                Profile? myProfile = await profileRepository.GetAsync(identityCache.EnsuredIdentity().Address, ct);

                if (myProfile != null)
                    config.MyThumbnail.SetupAsync(profileRepositoryWrapper, myProfile.UserNameColor, myProfile.Avatar.FaceSnapshotUrl, myProfile.UserId, ct).Forget();
            }

            Toggle(state);

            await viewInstance!.PlayShowAnimationAsync(config, ct);
            await UniTask.WhenAny(config.CloseButton.OnClickAsync(ct), UniTask.Delay(OPERATION_CONFIRMED_WAIT_TIME_MS, cancellationToken: ct));
            await viewInstance!.PlayHideAnimationAsync(config, ct);
            return;

            string ToHexStr(Color color) =>
                $"#{ColorUtility.ToHtmlStringRGB(color)}";
        }
    }
}
