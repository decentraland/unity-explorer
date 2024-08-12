using DCL.Passport.Fields;
using DCL.Profiles;
using System.Collections.Generic;
using UnityEngine.Pool;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace DCL.Passport.Modules
{
    public class BadgesOverview_PassportModuleController : IPassportModuleController
    {
        private const int BADGES_OVERVIEW_MAX_COUNT = 5;

        private readonly BadgesOverview_PassportModuleView view;

        private readonly IObjectPool<BadgeOverviewItem_PassportFieldView> badgesOverviewItemsPool;
        private readonly List<BadgeOverviewItem_PassportFieldView> instantiatedBadgesOverviewItems = new ();

        private Profile? currentProfile;

        public BadgesOverview_PassportModuleController(BadgesOverview_PassportModuleView view)
        {
            this.view = view;

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
            Clear();

            // TODO (Santi): Request badges for the currentProfile
            int randomBadgesCount = Random.Range(0, BADGES_OVERVIEW_MAX_COUNT + 1);
            for (var i = 0; i < randomBadgesCount; i++)
            {
                var badgeOverviewItem = badgesOverviewItemsPool.Get();
                badgeOverviewItem.BadgeNameText.text = $"Badge {currentProfile?.UserId?[..5]} {i + 1}";
                badgeOverviewItem.BadgeImage.sprite = null;
                instantiatedBadgesOverviewItems.Add(badgeOverviewItem);
            }

            view.BadgeOverviewItemsContainer.gameObject.SetActive(instantiatedBadgesOverviewItems.Count > 0);
            view.NoBadgesLabel.SetActive(instantiatedBadgesOverviewItems.Count == 0);
        }

        private void ClearBadgesOverviewItems()
        {
            foreach (BadgeOverviewItem_PassportFieldView badgeOverviewItem in instantiatedBadgesOverviewItems)
                badgesOverviewItemsPool.Release(badgeOverviewItem);

            instantiatedBadgesOverviewItems.Clear();
        }
    }
}
