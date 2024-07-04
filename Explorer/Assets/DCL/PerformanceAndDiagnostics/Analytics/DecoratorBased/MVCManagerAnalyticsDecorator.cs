using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.MessageBus;
using DCL.ExplorePanel;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class MVCManagerAnalyticsDecorator : IMVCManager
    {
        private readonly MVCManager core;
        private readonly IAnalyticsController analytics;

        private readonly Dictionary<Type, IDisposable> registeredAnalytics = new();
        private Dictionary<Type, Func<IController, IDisposable>> controllerAnalyticsFactory;

        public event Action<IController>? OnViewShowed;
        public event Action<IController>? OnViewClosed;

        public MVCManagerAnalyticsDecorator(MVCManager core, IAnalyticsController analytics)
        {
            this.core = core;
            this.analytics = analytics;
            core.OnViewShowed += c => OnViewShowed?.Invoke(c);
            core.OnViewClosed += c => OnViewClosed?.Invoke(c);
        }

        public void Initialize(IChatMessagesBus chatMessagesBus)
        {
            controllerAnalyticsFactory = new Dictionary<Type, Func<IController, IDisposable>>
            {
                { typeof(ChatController), CreateAnalytics<ChatController>(c => new ChatAnalytics(analytics, c, chatMessagesBus)) },
                { typeof(ExplorePanelController), CreateAnalytics<ExplorePanelController>(c => new MapAnalytics(analytics, c.NavmapController)) },
            };

            Func<IController, IDisposable> CreateAnalytics<T>(Func<T, IDisposable> factory) where T : IController =>
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
    }
}
