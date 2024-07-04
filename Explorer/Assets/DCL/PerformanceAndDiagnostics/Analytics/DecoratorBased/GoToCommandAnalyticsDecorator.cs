using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using System.Text.RegularExpressions;
using System.Threading;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class GoToCommandAnalyticsDecorator : IChatCommand
    {
        private readonly IAnalyticsController analytics;
        private readonly IChatCommand core;

        public GoToCommandAnalyticsDecorator(IChatCommand core, IAnalyticsController analytics)
        {
            this.core = core;
            this.analytics = analytics;
        }

        public UniTask<string> ExecuteAsync(Match match, CancellationToken ct)
        {
            analytics.Track(AnalyticsEvents.Chat.GOTO_TELEPORT);
            return core.ExecuteAsync(match, ct);
        }
    }
}
