using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using System.Threading;
using UnityEngine;

namespace DCL.CharacterMotion.LocomotionRecorder
{
    /// <summary>
    ///     Reports the latest locomotion suite; optionally holds until it stops running or pauses for focus, within
    ///     the tool-call time limit, so an agent waits on a suite without polling every second.
    /// </summary>
    public class GetLocomotionStatusTool : McpTool
    {
        /// <summary>Longest hold, kept under the dispatcher's 30 s tool-call timeout.</summary>
        internal const float MAX_WAIT_SEC = 25f;

        private const int POLL_INTERVAL_MS = 100;

        private readonly LocomotionSuiteSession session;

        public override string Name => "get_locomotion_status";

        public override string Description =>
            "Report the latest locomotion suite: state, phase, the scenario and repeat being run, the trace files written with their dropped and "
            + "inputMismatchFrames counts, failed runs, and waitingForFocus when a keyboard run is paused for the Editor's Game view focus. "
            + $"waitSec (max {MAX_WAIT_SEC:0}) holds the call until the suite stops running or pauses for focus.";

        public override JObject OutputSchema => LocomotionStatusReport.Schema();

        public override McpToolAnnotations Annotations => McpToolAnnotations.ReadOnly();

        public GetLocomotionStatusTool(LocomotionSuiteSession session)
        {
            this.session = session;
        }

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            if (arguments["waitSec"] != null && !arguments.TryGetFloat("waitSec", out _))
                return McpToolResult.Error("waitSec must be a number of seconds." + arguments.NonNumericHint("waitSec"));

            float waitSec = Mathf.Clamp(arguments.GetFloat("waitSec", 0f), 0f, MAX_WAIT_SEC);
            float deadline = UnityEngine.Time.realtimeSinceStartup + waitSec;

            // Real time: a fixed-dt suite drives Time.captureDeltaTime, so scaled delays would not track the wall clock.
            while (IsWorthWaitingFor() && UnityEngine.Time.realtimeSinceStartup < deadline)
                await UniTask.Delay(POLL_INTERVAL_MS, DelayType.Realtime, PlayerLoopTiming.Update, ct);

            string? note = waitSec > 0f && IsWorthWaitingFor() ? $"Still running after {waitSec:0.#} s." : null;
            return McpToolResult.JsonWithStructured(LocomotionStatusReport.Build(session, note));
        }

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Number("waitSec", $"Seconds to hold until the suite stops running or pauses for focus, 0 to {MAX_WAIT_SEC:0}. Default 0 (answer now).");

        private bool IsWorthWaitingFor() =>
            session.State == LocomotionSuiteState.Running && !session.WaitingForFocus;
    }
}
