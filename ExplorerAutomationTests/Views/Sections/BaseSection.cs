
namespace ExplorerAutomationTests.Views.Sections
{
    public abstract class BaseSection : BaseView
    {
        private readonly (By, string) _sectionLocator;

        protected BaseSection(DriverContainer drivers, (By, string) sectionLocator) : base(drivers)
        {
            _sectionLocator = sectionLocator;
        }

        [AllureStep("Check if section is visible")]
        public bool IsSectionVisible()
        {
            return IsObjectPresent(_sectionLocator);
        }

        [AllureStep("Wait for section to be visible")]
        public void WaitForSectionVisible(int timeoutSeconds = 10)
        {
            WaitForObject(_sectionLocator, timeoutSeconds);
        }
    }
}
