using DCL.BadgesAPIService;
using DCL.WebRequests;

namespace DCL.Passport.Modules.Badges
{
    public class BadgeInfo_PassportModuleSubController
    {
        private readonly BadgeInfo_PassportModuleView badgeInfoModuleView;
        private readonly IWebRequestController webRequestController;

        public BadgeInfo_PassportModuleSubController(
            BadgeInfo_PassportModuleView badgeInfoModuleView,
            IWebRequestController webRequestController)
        {
            this.badgeInfoModuleView = badgeInfoModuleView;
            this.webRequestController = webRequestController;

            badgeInfoModuleView.ConfigureImageController(webRequestController);
        }

        public void Setup(BadgeInfo badgeInfo) =>
            badgeInfoModuleView.Setup(badgeInfo);

        public void SetAsLoading(bool isLoading) =>
            badgeInfoModuleView.SetAsLoading(isLoading);

        public void Clear() =>
            badgeInfoModuleView.StopLoadingImage();
    }
}
