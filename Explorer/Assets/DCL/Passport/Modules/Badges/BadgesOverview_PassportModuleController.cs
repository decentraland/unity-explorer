using Cysharp.Threading.Tasks;
using DCL.BadgesAPIService;
using DCL.Diagnostics;
using DCL.Passport.Fields.Badges;
using DCL.Profiles;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.Passport.Modules.Badges
{
    public class BadgesOverview_PassportModuleController : IPassportModuleController
    {
        private const int BADGES_OVERVIEW_MAX_COUNT = 5;

        private readonly BadgesOverview_PassportModuleView view;
        private readonly BadgesAPIClient badgesAPIClient;
        private readonly PassportErrorsController passportErrorsController;
        private readonly IObjectPool<BadgeOverviewItem_PassportFieldView> badgesOverviewItemsPool;
        private readonly List<BadgeOverviewItem_PassportFieldView> instantiatedBadgesOverviewItems = new ();

        private Profile currentProfile;
        private CancellationTokenSource fetchBadgesCts;

        public BadgesOverview_PassportModuleController(
            BadgesOverview_PassportModuleView view,
            BadgesAPIClient badgesAPIClient,
            PassportErrorsController passportErrorsController,
            IWebRequestController webRequestController
        )
        {
            this.view = view;
            this.badgesAPIClient = badgesAPIClient;
            this.passportErrorsController = passportErrorsController;

            badgesOverviewItemsPool = new ObjectPool<BadgeOverviewItem_PassportFieldView>(
                InstantiateBadgeOverviewItemPrefab,
                defaultCapacity: BADGES_OVERVIEW_MAX_COUNT,
                actionOnGet: badgeOverviewItemView =>
                {
                    badgeOverviewItemView.ConfigureImageController(webRequestController);
                    badgeOverviewItemView.gameObject.SetActive(true);
                    badgeOverviewItemView.gameObject.transform.SetAsLastSibling();
                },
                actionOnRelease: badgeOverviewItemView => badgeOverviewItemView.gameObject.SetActive(false));
        }

        public void Setup(Profile profile)
        {
            currentProfile = profile;
            LoadBadgesOverviewItems();
        }

        public void Clear()
        {
            fetchBadgesCts.SafeCancelAndDispose();

            foreach (BadgeOverviewItem_PassportFieldView badgeOverviewItem in instantiatedBadgesOverviewItems)
            {
                badgeOverviewItem.StopLoadingImage();
                badgesOverviewItemsPool.Release(badgeOverviewItem);
            }

            instantiatedBadgesOverviewItems.Clear();
        }

        public void Dispose() =>
            Clear();

        private BadgeOverviewItem_PassportFieldView InstantiateBadgeOverviewItemPrefab()
        {
            BadgeOverviewItem_PassportFieldView badgeOverviewItemView = Object.Instantiate(view.BadgeOverviewItemPrefab, view.BadgeOverviewItemsContainer);
            return badgeOverviewItemView;
        }

        private void LoadBadgesOverviewItems()
        {
            Clear();

            if (string.IsNullOrEmpty(currentProfile.UserId))
                return;

            fetchBadgesCts = fetchBadgesCts.SafeRestart();
            LoadBadgesOverviewAsync(currentProfile.UserId, fetchBadgesCts.Token).Forget();
        }

        private async UniTaskVoid LoadBadgesOverviewAsync(string walletId, CancellationToken ct)
        {
            try
            {
                var badges = await badgesAPIClient.FetchLatestAchievedBadgesAsync(walletId, ct);

                foreach (var badgeInfo in badges)
                {
                    var badgeOverviewItem = badgesOverviewItemsPool.Get();
                    badgeOverviewItem.Setup(badgeInfo);
                    instantiatedBadgesOverviewItems.Add(badgeOverviewItem);
                }

                view.BadgeOverviewItemsContainer.gameObject.SetActive(instantiatedBadgesOverviewItems.Count > 0);
                view.NoBadgesLabel.SetActive(instantiatedBadgesOverviewItems.Count == 0);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error loading badges. Please try again!";
                passportErrorsController.Show(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.BADGES, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }
    }
}
