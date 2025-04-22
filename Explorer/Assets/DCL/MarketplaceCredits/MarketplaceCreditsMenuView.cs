using DCL.MarketplaceCredits.Fields;
using DCL.MarketplaceCredits.Sections;
using DCL.UI;
using MVC;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits
{
    public enum MarketplaceCreditsSection
    {
        WELCOME,
        VERIFY_EMAIL,
        GOALS_OF_THE_WEEK,
        WEEK_GOALS_COMPLETED,
        PROGRAM_ENDED,
    }

    public class MarketplaceCreditsMenuView : ViewBaseWithAnimationElement, IView, IPointerClickHandler
    {
        public event Action OnAnyPlaceClick;

        [field: SerializeField]
        public MarketplaceCreditsTotalCreditsWidgetView TotalCreditsWidget { get; private set; }

        [field: SerializeField]
        public Button InfoLinkButton { get; private set; }

        [field: SerializeField]
        public Button CloseButton { get; private set; }

        [field: SerializeField]
        public MarketplaceCreditsWelcomeSubView WelcomeSubView { get; private set; }

        [field: SerializeField]
        public MarketplaceCreditsVerifyEmailSubView VerifyEmailSubView { get; private set; }

        [field: SerializeField]
        public MarketplaceCreditsGoalsOfTheWeekSubView GoalsOfTheWeekSubView { get; private set; }

        [field: SerializeField]
        public MarketplaceCreditsWeekGoalsCompletedSubView WeekGoalsCompletedSubView { get; private set; }

        [field: SerializeField]
        public MarketplaceCreditsProgramEndedSubView ProgramEndedSubView { get; private set; }

        [field: SerializeField]
        public WarningNotificationView ErrorNotification { get; private set; }

        public void OnPointerClick(PointerEventData eventData) =>
            OnAnyPlaceClick?.Invoke();

        public void SetInfoLinkButtonActive(bool isActive) =>
            InfoLinkButton.gameObject.SetActive(isActive);
    }
}
