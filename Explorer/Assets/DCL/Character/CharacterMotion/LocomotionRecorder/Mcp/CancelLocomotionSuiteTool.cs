using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace DCL.CharacterMotion.LocomotionRecorder
{
    /// <summary>Stops the running locomotion suite: the run in progress is discarded, written traces stay, the frame rate is restored.</summary>
    public class CancelLocomotionSuiteTool : McpTool
    {
        private readonly LocomotionSuiteSession session;

        public override string Name => "cancel_locomotion_suite";

        public override string Description =>
            "Stop the running locomotion suite. The run in progress is discarded; trace files already written stay and are listed. "
            + "The frame rate, the camera and the input return to normal. Does nothing when no suite runs.";

        public override JObject OutputSchema => LocomotionStatusReport.Schema();

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: true);

        public CancelLocomotionSuiteTool(LocomotionSuiteSession session)
        {
            this.session = session;
        }

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            string? note = session.Cancel() ? null : "No suite was running.";
            return UniTask.FromResult(McpToolResult.JsonWithStructured(LocomotionStatusReport.Build(session, note)));
        }
    }
}
