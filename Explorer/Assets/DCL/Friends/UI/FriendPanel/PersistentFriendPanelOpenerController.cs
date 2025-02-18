using Cysharp.Threading.Tasks;
using DCL.Friends.UI.FriendPanel.Sections;
using DCL.Friends.UI.Requests;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.UI.Profiles.Helpers;
using DCL.Web3;
using MVC;
using System;
using System.Threading;
using UnityEngine.InputSystem;
using Utility;

namespace DCL.Friends.UI.FriendPanel
{
    public class PersistentFriendPanelOpenerController : ControllerBase<PersistentFriendPanelOpenerView>
    {
        private readonly IMVCManager mvcManager;
        private readonly DCLInput dclInput;
        private readonly IPassportBridge passportBridge;
        private readonly IFriendsService friendsService;
        private readonly IProfileNameColorHelper profileNameColorHelper;

        private FriendsPanelController? friendsPanelController;
        private bool isFriendPanelControllerOpen;
        private CancellationTokenSource? friendRequestReceivedCts;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public PersistentFriendPanelOpenerController(ViewFactoryMethod viewFactory,
            IMVCManager mvcManager,
            DCLInput dclInput,
            INotificationsBusController notificationsBusController,
            IPassportBridge passportBridge,
            IFriendsService friendsService,
            IProfileNameColorHelper profileNameColorHelper)
            : base(viewFactory)
        {
            this.mvcManager = mvcManager;
            this.dclInput = dclInput;
            this.passportBridge = passportBridge;
            this.friendsService = friendsService;
            this.profileNameColorHelper = profileNameColorHelper;

            notificationsBusController.SubscribeToNotificationTypeClick(NotificationType.SOCIAL_SERVICE_FRIENDSHIP_REQUEST, FriendRequestReceived);
            notificationsBusController.SubscribeToNotificationTypeClick(NotificationType.SOCIAL_SERVICE_FRIENDSHIP_ACCEPTED, FriendRequestAccepted);
            mvcManager.OnViewShowed += OnViewShowed;
            mvcManager.OnViewClosed += OnViewClosed;
            RegisterHotkey();
        }

        public override void Dispose()
        {
            base.Dispose();

            mvcManager.OnViewShowed -= OnViewShowed;
            mvcManager.OnViewClosed -= OnViewClosed;
            viewInstance?.OpenFriendPanelButton.onClick.RemoveListener(ToggleFriendsPanel);
            UnregisterHotkey();
            friendRequestReceivedCts.SafeCancelAndDispose();
        }

        private void FriendRequestAccepted(object[] parameters)
        {
            if (parameters.Length == 0 || parameters[0] is not FriendRequestAcceptedNotification)
                return;

            FriendRequestAcceptedNotification friendRequestAcceptedNotification = (FriendRequestAcceptedNotification)parameters[0];

            passportBridge.ShowAsync(new Web3Address(friendRequestAcceptedNotification.Metadata.Sender.Address)).Forget();
        }

        private void FriendRequestReceived(object[] parameters)
        {
            if (parameters.Length == 0 || parameters[0] is not FriendRequestReceivedNotification)
                return;

            friendRequestReceivedCts = friendRequestReceivedCts.SafeRestart();
            ManageFriendRequestReceivedNotificationAsync((FriendRequestReceivedNotification)parameters[0], friendRequestReceivedCts.Token).Forget();

            async UniTaskVoid ManageFriendRequestReceivedNotificationAsync(FriendRequestReceivedNotification notification, CancellationToken ct)
            {
                FriendshipStatus friendshipStatus = await friendsService.GetFriendshipStatusAsync(notification.Metadata.Sender.Address, ct);

                switch (friendshipStatus)
                {
                    case FriendshipStatus.FRIEND:
                        if (isFriendPanelControllerOpen)
                            friendsPanelController?.ToggleTabs(FriendsPanelController.FriendsPanelTab.FRIENDS);
                        else
                            ToggleFriendsPanel();
                        break;
                    case FriendshipStatus.REQUEST_RECEIVED:
                        mvcManager.ShowAsync(FriendRequestController.IssueCommand(new FriendRequestParams
                        {
                            Request = new FriendRequest(
                                friendRequestId: notification.Metadata.RequestId,
                                timestamp: GetDateTimeFromString(notification.Timestamp),
                                from: notification.Metadata.Sender.ToFriendProfile(profileNameColorHelper),
                                to: notification.Metadata.Receiver.ToFriendProfile(profileNameColorHelper),
                                messageBody: notification.Metadata.Message)
                        }), ct).Forget();
                        break;
                    default:
                        passportBridge.ShowAsync(new Web3Address(notification.Metadata.Sender.Address)).Forget();
                        break;
                }
            }
        }

        private DateTime GetDateTimeFromString(string epochString) =>
            !long.TryParse(epochString, out long unixTimestamp) ? new DateTime() : DateTimeOffset.FromUnixTimeMilliseconds(unixTimestamp).ToLocalTime().DateTime;

        private void RegisterHotkey()
        {
            dclInput.Shortcuts.FriendPanel.performed += OpenFriendsPanel;
        }

        private void UnregisterHotkey()
        {
            dclInput.Shortcuts.FriendPanel.performed -= OpenFriendsPanel;
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.CompletedTask;

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance!.OpenFriendPanelButton.onClick.AddListener(ToggleFriendsPanel);
        }

        private void OpenFriendsPanel(InputAction.CallbackContext obj) =>
            ToggleFriendsPanel();

        private void ToggleFriendsPanel()
        {
            if (isFriendPanelControllerOpen)
                friendsPanelController?.CloseFriendsPanel(default(InputAction.CallbackContext));
            else
                mvcManager.ShowAsync(FriendsPanelController.IssueCommand(new FriendsPanelParameter()));
        }

        private void OnViewShowed(IController controller)
        {
            if (controller is not FriendsPanelController friendsController) return;

            friendsPanelController ??= friendsController;
            isFriendPanelControllerOpen = true;
            viewInstance!.SetButtonStatePanelShow(true);
            UnregisterHotkey();
        }

        private void OnViewClosed(IController controller)
        {
            if (controller is not FriendsPanelController) return;

            viewInstance!.SetButtonStatePanelShow(false);
            isFriendPanelControllerOpen = false;
            RegisterHotkey();
        }
    }
}
