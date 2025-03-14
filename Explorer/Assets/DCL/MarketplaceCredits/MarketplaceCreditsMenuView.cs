using Cysharp.Threading.Tasks;
using DCL.MarketplaceCredits.Fields;
using DCL.MarketplaceCredits.Sections;
using DCL.UI;
using MVC;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits
{
    public enum MarketplaceCreditsSection
    {
        WELCOME,
        GOALS_OF_THE_WEEK,
        WEEK_GOALS_COMPLETED,
        PROGRAM_ENDED,
    }

    public class MarketplaceCreditsMenuView : ViewBaseWithAnimationElement
    {
        [field: SerializeField]
        public MarketplaceCreditsTotalCreditsWidgetView TotalCreditsWidget { get; private set; }

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

        [field: SerializeField]
        public MarketplaceCreditsProgramEndedView ProgramEndedView { get; private set; }

        [field: SerializeField]
        public WarningNotificationView ErrorNotification { get; private set; }

        [field: SerializeField]
        public float ErrorNotificationDuration { get; private set; } = 3f;

        private async UniTask ShowErrorNotificationAsync(string message, CancellationToken ct)
        {
            ErrorNotification.SetText(message);
            ErrorNotification.Show(ct);
            await UniTask.Delay((int) ErrorNotificationDuration * 1000, cancellationToken: ct);
            ErrorNotification.Hide(false, ct);
        }
    }
}
