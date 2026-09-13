using Cysharp.Threading.Tasks;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Utility.Types;
using DCL.Diagnostics;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Utility;

namespace DCL.ExplorePanel.Lobby
{
    /// <summary>Correlates a Home visit with its actions and asynchronous travel results.</summary>
    public sealed class LobbyVisit : IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly HashSet<(string section, bool success)> reportedSections = new ();
        private readonly string visitId = Guid.NewGuid().ToString("N");
        private readonly CancellationTokenSource lifetime = new ();
        private readonly Stopwatch focusedTime = new ();

        public CancellationToken Token { get; }

        public LobbyVisit(IAnalyticsController analytics, string entryPoint, bool worldReady)
        {
            this.analytics = analytics;
            Token = lifetime.Token;
            Track("lobby_opened", new JObject
            {
                ["entry_point"] = entryPoint,
                ["environment_id"] = "genesis-plaza",
                ["dynamic"] = true,
                ["world_ready"] = worldReady,
            });
        }

        public void Dispose()
        {
            focusedTime.Stop();
            lifetime.SafeCancelAndDispose();
            Track("lobby_closed", new JObject { ["duration_seconds"] = focusedTime.Elapsed.TotalSeconds, ["environment_id"] = "genesis-plaza" });
        }

        public void SetFocused(bool focused)
        {
            if (focused) focusedTime.Start();
            else focusedTime.Stop();
        }

        public void Action(string action, string section, string? targetId = null, int? position = null)
        {
            var properties = new JObject { ["action"] = action, ["section"] = section };
            if (targetId != null) properties["target_id"] = targetId;
            if (position.HasValue) properties["position"] = position.Value;
            Track("lobby_action", properties);
        }

        public void Content(string section, int count, bool success)
        {
            if (!reportedSections.Add((section, success))) return;
            Track("lobby_content_loaded", new JObject { ["section"] = section, ["item_count"] = count, ["success"] = success });
        }

        public async UniTask<bool> TravelAsync(string section, string targetType, string? targetId, int? position,
            Func<CancellationToken, UniTask<EnumResult<TaskError>>> operation, CancellationToken ct)
        {
            Action("join", section, targetId, position);
            long started = Stopwatch.GetTimestamp();
            var travel = new JObject
            {
                ["travel_id"] = Guid.NewGuid().ToString("N"),
                ["section"] = section, ["target_type"] = targetType,
            };
            if (targetId != null) travel["target_id"] = targetId;
            Track("lobby_travel_started", (JObject)travel.DeepClone());

            EnumResult<TaskError> result;
            try { result = await operation(ct); }
            catch (OperationCanceledException) { result = EnumResult<TaskError>.CancelledResult(TaskError.Cancelled); }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.UI);
                result = EnumResult<TaskError>.ErrorResult(TaskError.UnexpectedException, e.Message, e);
            }

            bool cancelled = ct.IsCancellationRequested || result.Error is { State: TaskError.Cancelled } or { Exception: OperationCanceledException };
            travel["outcome"] = cancelled ? "cancelled" : result.Success ? "success" : "failed";
            travel["duration_seconds"] = (Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency;
            Track("lobby_travel_finished", travel);
            return result.Success && !cancelled;
        }

        private void Track(string name, JObject properties)
        {
            properties["schema_version"] = 1;
            properties["lobby_enabled"] = true;
            properties["visit_id"] = visitId;
            analytics.Track(name, properties);
        }

    }
}
