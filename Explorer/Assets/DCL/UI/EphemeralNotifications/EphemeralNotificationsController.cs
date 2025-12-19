using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using MVC;
using System.Threading;
using Utility;

namespace DCL.UI.EphemeralNotifications
{
    public class EphemeralNotificationsController : ControllerBase<EphemeralNotificationsView>
    {
        private ProfileRepositoryWrapper profileRepositoryWrapper;
        private CancellationTokenSource cts = new CancellationTokenSource();

        public EphemeralNotificationsController(ViewFactoryMethod viewFactory, ProfileRepositoryWrapper profileRepositoryWrapper) : base(viewFactory)
        {
            this.profileRepositoryWrapper = profileRepositoryWrapper;
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);

        public override void Dispose()
        {
            base.Dispose();
            cts.SafeCancelAndDispose();
        }

        /// <summary>
        /// Enqueues a notification that will be processed and shown when possible.
        /// </summary>
        /// <param name="notificationTypeName">The name of the type of notification (the name of the prefab).</param>
        /// <param name="senderWalletAddress">The wallet address of the user that sent the notification.</param>
        /// <param name="textValues">A list of values to be used to compose the label of the notification.</param>
        public async UniTask AddNotificationAsync(string notificationTypeName, string senderWalletAddress, string[] textValues)
        {
            Profile sender = await profileRepositoryWrapper.GetProfileAsync(senderWalletAddress, cts.Token);

            viewInstance.AddNotification(new EphemeralNotificationsView.NotificationData{NotificationTypeName = notificationTypeName, TextValues = textValues, Sender = sender});
        }
    }
}
