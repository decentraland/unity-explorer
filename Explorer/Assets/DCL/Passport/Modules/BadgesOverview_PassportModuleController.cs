using Cysharp.Threading.Tasks;
using DCL.BadgesAPIService;
using DCL.Diagnostics;
using DCL.Passport.Fields;
using DCL.Profiles;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.Passport.Modules
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
            PassportErrorsController passportErrorsController)
        {
            this.view = view;
            this.badgesAPIClient = badgesAPIClient;
            this.passportErrorsController = passportErrorsController;

            badgesOverviewItemsPool = new ObjectPool<BadgeOverviewItem_PassportFieldView>(
                InstantiateBadgeOverviewItemPrefab,
                defaultCapacity: BADGES_OVERVIEW_MAX_COUNT,
                actionOnGet: badgeOverviewItemView =>
                {
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

        public void Clear() =>
            ClearBadgesOverviewItems();

        public void Dispose() =>
            Clear();

        private BadgeOverviewItem_PassportFieldView InstantiateBadgeOverviewItemPrefab()
        {
            BadgeOverviewItem_PassportFieldView badgeOverviewItemView = Object.Instantiate(view.BadgeOverviewItemPrefab, view.BadgeOverviewItemsContainer);
            return badgeOverviewItemView;
        }

        private void LoadBadgesOverviewItems()
        {
            ClearBadgesOverviewItems();

            if (string.IsNullOrEmpty(currentProfile.UserId))
                return;

            fetchBadgesCts = fetchBadgesCts.SafeRestart();
            LoadBadgesOverviewAsync(currentProfile.UserId, fetchBadgesCts.Token).Forget();
        }

        private async UniTaskVoid LoadBadgesOverviewAsync(string walletId, CancellationToken ct)
        {
            try
            {
                var badges = await badgesAPIClient.FetchBadgesAsync(walletId, false, 5, 0, ct);
                foreach (BadgeInfo badgeInfo in badges.unlocked)
                {
                    var badgeOverviewItem = badgesOverviewItemsPool.Get();
                    badgeOverviewItem.BadgeNameText.text = badgeInfo.name;
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
                ReportHub.LogError(ReportCategory.PROFILE, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        private void ClearBadgesOverviewItems()
        {
            fetchBadgesCts.SafeCancelAndDispose();

            foreach (BadgeOverviewItem_PassportFieldView badgeOverviewItem in instantiatedBadgesOverviewItems)
                badgesOverviewItemsPool.Release(badgeOverviewItem);

            instantiatedBadgesOverviewItems.Clear();
        }
    }
}
