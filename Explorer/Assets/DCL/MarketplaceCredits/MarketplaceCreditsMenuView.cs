using DCL.MarketplaceCredits.Fields;
using DCL.MarketplaceCredits.Sections;
using DCL.UI;
using MVC;
using System;
using System.Collections.Generic;
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

    public class MarketplaceCreditsMenuView : ViewBaseWithAnimationElement, IPointerClickHandler
    {
        public event Action OnAnyPlaceClick;

        [field: SerializeField]
        public MarketplaceCreditsTotalCreditsWidgetView TotalCreditsWidget { get; private set; }

        [field: SerializeField]
        public Button InfoLinkButton { get; private set; }

        [field: SerializeField]
        public List<Button> CloseButtons { get; private set; }

        [field: SerializeField]
        public MarketplaceCreditsWelcomeView WelcomeView { get; private set; }

        [field: SerializeField]
        public MarketplaceCreditsVerifyEmailView VerifyEmailView { get; private set; }

        [field: SerializeField]
        public MarketplaceCreditsGoalsOfTheWeekView GoalsOfTheWeekView { get; private set; }

        [field: SerializeField]
        public MarketplaceCreditsWeekGoalsCompletedView WeekGoalsCompletedView { get; private set; }

        [field: SerializeField]
        public MarketplaceCreditsProgramEndedView ProgramEndedView { get; private set; }

        [field: SerializeField]
        public WarningNotificationView ErrorNotification { get; private set; }

        [field: SerializeField]
        public float ErrorNotificationDuration { get; private set; } = 3f;

        public void OnPointerClick(PointerEventData eventData) =>
            OnAnyPlaceClick?.Invoke();
    }
}
