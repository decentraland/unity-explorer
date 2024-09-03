using DCL.BadgesAPIService;
using DCL.Passport.Fields.Badges;
using DCL.Passport.Utils;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace DCL.Passport.Modules.Badges
{
    public class BadgeDetailsCards_PassportModuleSubController
    {
        private const int BADGES_DETAIL_CARDS_POOL_DEFAULT_CAPACITY = 50;
        private const int GRID_ITEMS_PER_ROW = 6;

        public readonly Dictionary<string,List<BadgeDetailCard_PassportFieldView>> InstantiatedBadgeDetailCards = new ();

        private readonly BadgesDetails_PassportModuleView view;
        private readonly BadgesCategories_PassportModuleSubController badgesCategoriesController;
        private readonly BadgeInfo_PassportModuleSubController badgeInfoController;
        private readonly IObjectPool<BadgeDetailCard_PassportFieldView> badgeDetailCardsPool;
        private readonly IObjectPool<BadgeDetailCard_PassportFieldView> emptyItemsPool;
        private readonly List<BadgeDetailCard_PassportFieldView> instantiatedEmptyItems = new ();

        public BadgeDetailsCards_PassportModuleSubController(
            BadgesDetails_PassportModuleView view,
            IWebRequestController webRequestController,
            BadgesCategories_PassportModuleSubController badgesCategoriesController,
            BadgeInfo_PassportModuleSubController badgeInfoController)
        {
            this.view = view;
            this.badgesCategoriesController = badgesCategoriesController;
            this.badgeInfoController = badgeInfoController;

            badgeDetailCardsPool = new ObjectPool<BadgeDetailCard_PassportFieldView>(
                InstantiateBadgeDetailCardPrefab,
                defaultCapacity: BADGES_DETAIL_CARDS_POOL_DEFAULT_CAPACITY,
                actionOnGet: badgeDetailCardView =>
                {
                    badgeDetailCardView.ConfigureImageController(webRequestController);
                    badgeDetailCardView.gameObject.SetActive(true);
                    badgeDetailCardView.SetAsSelected(false);
                },
                actionOnRelease: badgeDetailCardView => badgeDetailCardView.gameObject.SetActive(false));

            emptyItemsPool = new ObjectPool<BadgeDetailCard_PassportFieldView>(
                InstantiateBadgeDetailCardPrefab,
                defaultCapacity: GRID_ITEMS_PER_ROW - 1,
                actionOnGet: emptyItemView =>
                {
                    emptyItemView.gameObject.SetActive(true);
                    emptyItemView.SetInvisible(true);
                },
                actionOnRelease: emptyItemView => emptyItemView.gameObject.SetActive(false));
        }

        public void CreateBadgeDetailCard(BadgeInfo badge, bool isOwnProfile)
        {
            if (isOwnProfile)
                badge.isNew = BadgesUtils.IsBadgeNew(badge.id);

            var badgeDetailCard = badgeDetailCardsPool.Get();
            badgeDetailCard.Setup(badge, isOwnProfile);
            badgeDetailCard.Button.onClick.AddListener(() => { SelectBadgeCard(badgeDetailCard, isOwnProfile); });

            // Place badge into the corresponding category container
            foreach (var badgesCategoryContainer in badgesCategoriesController.InstantiatedBadgesCategoryContainers)
            {
                if (!string.Equals(badgesCategoryContainer.Category, badge.category, StringComparison.CurrentCultureIgnoreCase))
                    continue;

                badgeDetailCard.transform.parent = badgesCategoryContainer.BadgeDetailCardsContainer;
                badgeDetailCard.transform.SetAsFirstSibling();
                break;
            }

            if (!InstantiatedBadgeDetailCards.ContainsKey(badge.category.ToLower()))
                InstantiatedBadgeDetailCards.Add(badge.category.ToLower(), new List<BadgeDetailCard_PassportFieldView>());

            InstantiatedBadgeDetailCards[badge.category.ToLower()].Add(badgeDetailCard);
        }

        public void SelectBadgeCard(BadgeDetailCard_PassportFieldView badgeDetailCard, bool isOwnProfile)
        {
            if (badgeDetailCard.IsSelected)
                return;

            foreach (var instantiatedBadge in InstantiatedBadgeDetailCards)
            foreach (var instantiateBadgeByCategory in instantiatedBadge.Value)
                instantiateBadgeByCategory.SetAsSelected(false);

            badgeDetailCard.SetAsSelected(true);
            badgeInfoController.Setup(badgeDetailCard.Model, isOwnProfile);

            if (!badgeDetailCard.Model.isLocked && isOwnProfile)
            {
                BadgesUtils.SetBadgeAsRead(badgeDetailCard.Model.id);
                badgeDetailCard.SetAsNew(false);
            }
        }

        public void CreateEmptyDetailCards()
        {
            foreach (var badgeDetailCardsByCategory in InstantiatedBadgeDetailCards)
            {
                int missingEmptyItems = CalculateMissingEmptyItems(badgeDetailCardsByCategory.Value.Count);
                for (var i = 0; i < missingEmptyItems; i++)
                {
                    var emptyItem = emptyItemsPool.Get();
                    emptyItem.gameObject.name = "EmptyItem";
                    emptyItem.transform.parent = badgeDetailCardsByCategory.Value[0].transform.parent;
                    emptyItem.transform.SetAsFirstSibling();
                    instantiatedEmptyItems.Add(emptyItem);
                }
            }
        }

        private static int CalculateMissingEmptyItems(int totalItems)
        {
            int remainder = totalItems % GRID_ITEMS_PER_ROW;
            int missingItems = remainder == 0 ? 0 : GRID_ITEMS_PER_ROW - remainder;
            return missingItems;
        }

        public void ClearBadgeDetailCards()
        {
            ClearEmptyItems();

            foreach (var badgeDetailCards in InstantiatedBadgeDetailCards)
            {
                foreach (var badgeDetailCardByCategory in badgeDetailCards.Value)
                {
                    badgeDetailCardByCategory.StopLoadingImage();
                    badgeDetailCardByCategory.Button.onClick.RemoveAllListeners();
                    badgeDetailCardsPool.Release(badgeDetailCardByCategory);
                }
            }

            InstantiatedBadgeDetailCards.Clear();
        }

        private BadgeDetailCard_PassportFieldView InstantiateBadgeDetailCardPrefab()
        {
            BadgeDetailCard_PassportFieldView badgeDetailCareView = Object.Instantiate(view.BadgeDetailCardPrefab, view.MainContainer);
            return badgeDetailCareView;
        }

        private void ClearEmptyItems()
        {
            foreach (BadgeDetailCard_PassportFieldView emptyItem in instantiatedEmptyItems)
                emptyItemsPool.Release(emptyItem);

            instantiatedEmptyItems.Clear();
        }
    }
}
