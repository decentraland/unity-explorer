using DCL.Profiles;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.Passport.Modules
{
    public class BadgesDetails_PassportModuleController : IPassportModuleController
    {
        private const int BADGES_DETAILS_ITEMS_POOL_DEFAULT_CAPACITY = 50;

        private readonly BadgesDetails_PassportModuleView view;

        private readonly IObjectPool<BadgeDetailCard_PassportFieldView> badgeDetailCardsPool;
        private readonly List<BadgeDetailCard_PassportFieldView> instantiatedBadgeDetailCards = new ();

        private Profile? currentProfile;

        public BadgesDetails_PassportModuleController(BadgesDetails_PassportModuleView view)
        {
            this.view = view;

            badgeDetailCardsPool = new ObjectPool<BadgeDetailCard_PassportFieldView>(
                InstantiateBadgeDetailCardPrefab,
                defaultCapacity: BADGES_DETAILS_ITEMS_POOL_DEFAULT_CAPACITY,
                actionOnGet: badgeDetailCardView =>
                {
                    badgeDetailCardView.gameObject.SetActive(true);
                    badgeDetailCardView.gameObject.transform.SetAsFirstSibling();
                },
                actionOnRelease: badgeDetailCardView => badgeDetailCardView.gameObject.SetActive(false));
        }

        public void Setup(Profile profile)
        {
            currentProfile = profile;
            LoadBadgeDetailCards();
        }

        public void Clear() =>
            ClearBadgeDetailCards();

        public void Dispose() =>
            Clear();

        private BadgeDetailCard_PassportFieldView InstantiateBadgeDetailCardPrefab()
        {
            BadgeDetailCard_PassportFieldView badgeDetailCareView = Object.Instantiate(view.BadgeDetailCardPrefab, view.BadgeDetailCardsContainer);
            return badgeDetailCareView;
        }

        private void LoadBadgeDetailCards()
        {
            Clear();

            // TODO (Santi): Request badges for the currentProfile
            int randomBadgesCount = Random.Range(0, BADGES_DETAILS_ITEMS_POOL_DEFAULT_CAPACITY + 1);
            for (var i = 0; i < randomBadgesCount; i++)
            {
                var badgeDetailCard = badgeDetailCardsPool.Get();
                badgeDetailCard.BadgeNameText.text = $"Badge {currentProfile?.UserId?[..5]} {i + 1}";
                badgeDetailCard.BadgeImage.sprite = null;
                instantiatedBadgeDetailCards.Add(badgeDetailCard);
            }
        }

        private void ClearBadgeDetailCards()
        {
            foreach (BadgeDetailCard_PassportFieldView badgeOverviewItem in instantiatedBadgeDetailCards)
                badgeDetailCardsPool.Release(badgeOverviewItem);

            instantiatedBadgeDetailCards.Clear();
        }
    }
}
