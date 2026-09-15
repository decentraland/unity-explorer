using DCL.UI.UpgradeGuestAccountPopup;
using Newtonsoft.Json.Linq;
using System;
using static DCL.PerformanceAndDiagnostics.Analytics.AnalyticsEvents;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
    public class UpgradeGuestAccountAnalytics : IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly UpgradeGuestAccountPopupController controller;

        public UpgradeGuestAccountAnalytics(IAnalyticsController analytics, UpgradeGuestAccountPopupController controller)
        {
            this.analytics = analytics;
            this.controller = controller;

            controller.PromptShown += OnPromptShown;
            controller.UpgradeStarted += OnUpgradeStarted;
            controller.UpgradeCompleted += OnUpgradeCompleted;
            controller.UpgradeFailed += OnUpgradeFailed;
        }

        public void Dispose()
        {
            controller.PromptShown -= OnPromptShown;
            controller.UpgradeStarted -= OnUpgradeStarted;
            controller.UpgradeCompleted -= OnUpgradeCompleted;
            controller.UpgradeFailed -= OnUpgradeFailed;
        }

        private void OnPromptShown(GuestUpgradeTrigger trigger) =>
            analytics.Track(Authentication.GUEST_UPGRADE_PROMPT, TriggerPayload(trigger));

        private void OnUpgradeStarted(GuestUpgradeTrigger trigger) =>
            analytics.Track(Authentication.GUEST_UPGRADE_STARTED, TriggerPayload(trigger));

        private void OnUpgradeCompleted(GuestUpgradeTrigger trigger) =>
            // isInstant: true — the end of the upgrade funnel, and the identity is replaced right
            // after, so the event must not sit in the buffer
            analytics.Track(Authentication.GUEST_UPGRADE_COMPLETED, TriggerPayload(trigger), isInstant: true);

        private void OnUpgradeFailed(GuestUpgradeTrigger trigger, string reason)
        {
            JObject payload = TriggerPayload(trigger);
            payload.Add("reason", reason);

            analytics.Track(Authentication.GUEST_UPGRADE_FAILED, payload);
        }

        private static JObject TriggerPayload(GuestUpgradeTrigger trigger) =>
            new () { { "trigger", trigger.ToString() } };
    }
}
