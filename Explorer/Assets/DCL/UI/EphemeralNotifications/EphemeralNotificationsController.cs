using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using MVC;
using System.Threading;
using UnityEngine;
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
        ///
        /// </summary>
        /// <param name="notificationTypeName"></param>
        /// <param name="senderWalletAddress"></param>
        /// <param name="textValues"></param>
        public async UniTask AddNotificationAsync(string notificationTypeName, string senderWalletAddress, string[] textValues)
        {
            Profile sender = await profileRepositoryWrapper.GetProfileAsync(senderWalletAddress, cts.Token);

            viewInstance.AddNotification(new EphemeralNotificationsView.NotificationData{NotificationTypeName = notificationTypeName, TextValues = textValues, Sender = sender});
        }
    }
}
