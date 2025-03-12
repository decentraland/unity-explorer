using MVC;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits
{
    public enum MarketplaceCreditsSection
    {
        WELCOME,
        GOALS_OF_THE_WEEK,
        WEEK_GOALS_COMPLETED,
    }

    public class MarketplaceCreditsMenuView : ViewBaseWithAnimationElement
    {
        [field: SerializeField]
        public Button InfoLinkButton { get; private set; }

        [field: SerializeField]
        public List<Button> CloseButtons { get; private set; }

        [field: SerializeField]
        public MarketplaceCreditsWelcomeView WelcomeView { get; private set; }

        [field: SerializeField]
        public MarketplaceCreditsGoalsOfTheWeekView GoalsOfTheWeekView { get; private set; }

        [field: SerializeField]
        public MarketplaceCreditsWeekGoalsCompletedView WeekGoalsCompletedView { get; private set; }
    }
}
