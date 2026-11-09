using DCL.UI.UpgradeGuestAccountPopup;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
    /// <summary>
    ///     An ephemeral guest upgrades by creating a real account in the authentication screen, which happens after
    ///     the popup that started it is gone. This carries the trigger of that popup across the logout, so the
    ///     completion reports the same one the rest of the funnel did.
    /// </summary>
    public class PendingGuestUpgrade
    {
        private GuestUpgradeTrigger? trigger;

        public void Set(GuestUpgradeTrigger value) =>
            trigger = value;

        public void Clear() =>
            trigger = null;

        public bool TryConsume(out GuestUpgradeTrigger value)
        {
            value = trigger ?? default(GuestUpgradeTrigger);

            if (trigger == null)
                return false;

            trigger = null;
            return true;
        }
    }
}
