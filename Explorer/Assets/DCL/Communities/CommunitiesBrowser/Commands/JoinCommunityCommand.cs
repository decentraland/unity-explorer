using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.Utilities.Extensions;
using System.Threading;
using Utility.Types;
using Notifications = DCL.NotificationsBusController.NotificationsBus;

namespace DCL.Communities.CommunitiesBrowser.Commands
{
    public class JoinCommunityCommand
    {
        private const string JOIN_COMMUNITY_ERROR_MESSAGE = "There was an error joining community. Please try again.";

        private readonly CommunitiesDataProvider.CommunitiesDataProvider dataProvider;

        public JoinCommunityCommand(CommunitiesDataProvider.CommunitiesDataProvider dataProvider)
        {
            this.dataProvider = dataProvider;
        }

        public void Execute(string communityId, CancellationToken ct)
        {
            JoinCommunityAsync().Forget();
            return;

            async UniTaskVoid JoinCommunityAsync()
            {
                Result<bool> result = await dataProvider.JoinCommunityAsync(communityId, ct).SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested)
                    return;

                if (!result.Success || !result.Value)
                    Notifications.NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(JOIN_COMMUNITY_ERROR_MESSAGE));
            }
        }
    }
}
