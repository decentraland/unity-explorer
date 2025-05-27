using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Friends.UI.FriendPanel.Sections;
using DCL.Friends.UI.Requests;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.UI.SharedSpaceManager;
using DCL.Utilities.Extensions;
using DCL.Web3;
using MVC;
using System;
using System.Threading;
using Utility;
using Utility.Types;

namespace DCL.Friends.UI.FriendPanel
{
    public class PersistentFriendPanelOpenerController : ControllerBase<PersistentFriendPanelOpenerView>
    {
        private readonly IMVCManager mvcManager;
        private readonly IPassportBridge passportBridge;
        private readonly IFriendsService friendsService;
        private readonly ISharedSpaceManager sharedSpaceManager;

        private FriendsPanelController? friendsPanelController;
        private CancellationTokenSource? friendRequestReceivedCts;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public event Action? FriendshipNotificationClicked;

        public PersistentFriendPanelOpenerController(ViewFactoryMethod viewFactory,
            IMVCManager mvcManager,
            INotificationsBusController notificationsBusController,
            IPassportBridge passportBridge,
            IFriendsService friendsService,
            ISharedSpaceManager sharedSpaceManager,
            FriendsPanelController friendsPanelController)
            : base(viewFactory)
        {
            this.mvcManager = mvcManager;
            this.passportBridge = passportBridge;
            this.friendsService = friendsService;
            this.sharedSpaceManager = sharedSpaceManager;
            this.friendsPanelController = friendsPanelController;

            notificationsBusController.SubscribeToNotificationTypeClick(NotificationType.SOCIAL_SERVICE_FRIENDSHIP_REQUEST, FriendRequestReceived);
            notificationsBusController.SubscribeToNotificationTypeClick(NotificationType.SOCIAL_SERVICE_FRIENDSHIP_ACCEPTED, FriendRequestAccepted);
        }

        public override void Dispose()
        {
            base.Dispose();

            friendRequestReceivedCts.SafeCancelAndDispose();
        }

        private void FriendRequestAccepted(object[] parameters)
        {
            if (parameters.Length == 0 || parameters[0] is not FriendRequestAcceptedNotification)
                return;

            FriendshipNotificationClicked?.Invoke();

            FriendRequestAcceptedNotification friendRequestAcceptedNotification = (FriendRequestAcceptedNotification)parameters[0];

            passportBridge.ShowAsync(new Web3Address(friendRequestAcceptedNotification.Metadata.Sender.Address)).Forget();
        }

        private void FriendRequestReceived(object[] parameters)
        {
            if (parameters.Length == 0 || parameters[0] is not FriendRequestReceivedNotification)
                return;

            FriendshipNotificationClicked?.Invoke();

            friendRequestReceivedCts = friendRequestReceivedCts.SafeRestart();
            ManageFriendRequestReceivedNotificationAsync((FriendRequestReceivedNotification)parameters[0], friendRequestReceivedCts.Token).Forget();

            async UniTaskVoid ManageFriendRequestReceivedNotificationAsync(FriendRequestReceivedNotification notification, CancellationToken ct)
            {
                Result<FriendshipStatus> result = await friendsService.GetFriendshipStatusAsync(notification.Metadata.Sender.Address, ct).SuppressToResultAsync(ReportCategory.FRIENDS);

                if (!result.Success)
                    return;

                FriendshipStatus friendshipStatus = result.Value;

                switch (friendshipStatus)
                {
                    case FriendshipStatus.FRIEND:
                        if (friendsPanelController!.State != ControllerState.ViewHidden)
                            friendsPanelController?.ToggleTabs(FriendsPanelController.FriendsPanelTab.FRIENDS);
                        else
                            sharedSpaceManager.ShowAsync(PanelsSharingSpace.Friends, new FriendsPanelParameter(FriendsPanelController.FriendsPanelTab.FRIENDS)).Forget();

                        break;
                    case FriendshipStatus.REQUEST_RECEIVED:
                        mvcManager.ShowAsync(FriendRequestController.IssueCommand(new FriendRequestParams
                        {
                            Request = new FriendRequest(
                                friendRequestId: notification.Metadata.RequestId,
                                timestamp: GetDateTimeFromString(notification.Timestamp),
                                from: notification.Metadata.Sender.ToFriendProfile(),
                                to: notification.Metadata.Receiver.ToFriendProfile(),
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

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.CompletedTask;
    }
}
