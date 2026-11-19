using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace DCL.CharacterMotion.LocomotionRecorder
{
    /// <summary>
    ///     Turns the own avatar's animations on or off outside a locomotion suite (the debug panel's toggle), for manual
    ///     checks and screenshots of the movement without the animation layer.
    /// </summary>
    public class SetAvatarAnimationsTool : McpTool
    {
        private readonly LocomotionSuiteSession session;

        public override string Name => "set_avatar_animations";

        public override string Description =>
            "Turn the own avatar's animations on or off. Off keeps the Animator disabled, so the model holds its pose and slides with the "
            + "character; movement itself does not change. A running locomotion suite keeps its own setting until it ends; this toggle applies outside suites.";

        public override JObject OutputSchema => LocomotionStatusReport.Schema();

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: true);

        public SetAvatarAnimationsTool(LocomotionSuiteSession session)
        {
            this.session = session;
        }

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            if (arguments["enabled"] is not { Type: JTokenType.Boolean } enabled)
                return UniTask.FromResult(McpToolResult.Error("enabled is required: true turns the animations on, false off."));

            session.SetAnimationsToggle(enabled.Value<bool>());

            string? note = session.State == LocomotionSuiteState.Running && session.AnimationsEnabled != session.AnimationsToggle
                ? $"Suite {session.RunId} keeps animations {(session.AnimationsEnabled ? "on" : "off")} until it ends; the toggle applies after it."
                : null;

            return UniTask.FromResult(McpToolResult.JsonWithStructured(LocomotionStatusReport.Build(session, note)));
        }

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Boolean("enabled", "true: animations on; false: off.", isRequired: true);
    }
}
