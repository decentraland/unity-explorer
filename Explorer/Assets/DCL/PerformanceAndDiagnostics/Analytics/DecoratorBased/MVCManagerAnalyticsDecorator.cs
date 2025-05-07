using Cysharp.Threading.Tasks;
using DCL.AuthenticationScreenFlow;
using DCL.Chat;
using DCL.ExplorePanel;
using DCL.Friends.UI.FriendPanel;
using DCL.InWorldCamera.PhotoDetail;
using DCL.MarketplaceCredits;
using DCL.Passport;
using DCL.PerformanceAndDiagnostics.Analytics.EventBased;
using DCL.UI.ProfileNames;
using DCL.UI.Sidebar;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class MVCManagerAnalyticsDecorator : IMVCManager
    {
        private readonly MVCManager core;

        private readonly Dictionary<Type, IDisposable> registeredAnalytics = new ();
        private readonly Dictionary<Type, Func<IController, IDisposable>> controllerAnalyticsFactory;

        public event Action<IController>? OnViewShowed;
        public event Action<IController>? OnViewClosed;

        public MVCManagerAnalyticsDecorator(MVCManager core, IAnalyticsController analytics)
        {
            this.core = core;
            core.OnViewShowed += c => OnViewShowed?.Invoke(c);
            core.OnViewClosed += c => OnViewClosed?.Invoke(c);

            controllerAnalyticsFactory = new Dictionary<Type, Func<IController, IDisposable>>
            {
                { typeof(ChatController), CreateAnalytics<ChatController>(c => new ChatEventsAnalytics(analytics, c)) },
                { typeof(PhotoDetailController), CreateAnalytics<PhotoDetailController>(c => new PhotoDetailAnalytics(analytics, c)) },
                { typeof(PassportController), CreateAnalytics<PassportController>(c => new PassportAnalytics(analytics, c)) },
                { typeof(AuthenticationScreenController), CreateAnalytics<AuthenticationScreenController>(c => new AuthenticationScreenAnalytics(analytics, c)) },
                { typeof(SidebarController), CreateAnalytics<SidebarController>(c => new SupportAnalytics(analytics, c)) },
                { typeof(FriendsPanelController), CreateAnalytics<FriendsPanelController>(c => new FriendsPanelAnalytics(analytics, c)) },
                { typeof(PersistentFriendPanelOpenerController), CreateAnalytics<PersistentFriendPanelOpenerController>(c => new PersistentFriendPanelOpenerAnalytics(analytics, c)) },
                { typeof(ExplorePanelController), CreateAnalytics<ExplorePanelController>(c => new ExplorePanelAnalytics(analytics, c)) },
                { typeof(ProfileNameEditorController), CreateAnalytics<ProfileNameEditorController>(c => new ProfileNameEditorAnalytics(analytics, c)) },
                { typeof(MarketplaceCreditsMenuController), CreateAnalytics<MarketplaceCreditsMenuController>(c => new MarketplaceCreditsAnalytics(analytics, c)) },
            };

            Func<IController, IDisposable> CreateAnalytics<T>(Func<T, IDisposable> factory) where T: IController =>
                controller => factory((T)controller);
        }

        public void Dispose()
        {
            core.Dispose();

            foreach (IDisposable analytics in registeredAnalytics.Values)
                analytics.Dispose();
        }

        public void RegisterController<TView, TInputData>(IController<TView, TInputData> controller) where TView: IView
        {
            core.RegisterController(controller);

            Type cType = controller.GetType();

            if (controllerAnalyticsFactory.ContainsKey(cType) && !registeredAnalytics.ContainsKey(cType))
                registeredAnalytics.Add(cType, controllerAnalyticsFactory[cType].Invoke(controller));
        }

        public UniTask ShowAsync<TView, TInputData>(ShowCommand<TView, TInputData> command, CancellationToken ct = default) where TView: IView =>
            core.ShowAsync(command, ct);

        public void SetAllViewsCanvasActive(bool isActive) =>
            core.SetAllViewsCanvasActive(isActive);

        public void SetAllViewsCanvasActive(IController except, bool isActive) =>
            core.SetAllViewsCanvasActive(except, isActive);
    }
}
