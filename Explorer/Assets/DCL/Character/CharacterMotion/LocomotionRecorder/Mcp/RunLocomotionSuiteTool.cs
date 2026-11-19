using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using UnityEngine;

namespace DCL.CharacterMotion.LocomotionRecorder
{
    /// <summary>
    ///     Starts a locomotion suite in the running client and returns at once: a suite outlasts a tool call, so its
    ///     progress is read with <see cref="GetLocomotionStatusTool" /> (which can hold until the suite ends).
    /// </summary>
    public class RunLocomotionSuiteTool : McpTool
    {
        internal const int MAX_REPEATS = 100;

        private readonly LocomotionSuiteSession session;

        public override string Name => "run_locomotion_suite";

        public override string Description =>
            "Start the locomotion recorder on a suite of movement scenarios (trace format v1): each run teleports the player to the spawn, "
            + "settles, plays the scripted keys at the scenario's locked frame rate and writes <out>/unity/<id>__fps<fps>__r<n>.jsonl. "
            + "Returns at once with the run id; follow with get_locomotion_status (waitSec holds until the suite ends). Refused while a suite runs. "
            + "In the Editor input keyboard needs the Game view focused (the run pauses before t = 0 until it is); input component needs no focus.";

        public override JObject OutputSchema => LocomotionStatusReport.Schema();

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: false);

        public RunLocomotionSuiteTool(LocomotionSuiteSession session)
        {
            this.session = session;
        }

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct) =>
            UniTask.FromResult(Run(arguments));

        /// <summary>Reads the arguments into suite options; false, with the message for the agent, when one is unusable.</summary>
        internal static bool TryParse(JObject arguments, [NotNullWhen(true)] out LocomotionRecorderOptions? options, out string error)
        {
            options = null;
            error = string.Empty;

            string? suite = arguments.GetStringOrNull("suite");

            if (string.IsNullOrWhiteSpace(suite) || !Path.IsPathRooted(suite))
            {
                error = "suite is required: the absolute path of a scenario file or a directory of them.";
                return false;
            }

            int repeats = 1;

            if (arguments["repeats"] != null && (!arguments.TryGetInt("repeats", out repeats) || repeats < 1 || repeats > MAX_REPEATS))
            {
                error = $"repeats must be an integer from 1 to {MAX_REPEATS}." + arguments.NonNumericHint("repeats");
                return false;
            }

            Vector3? spawn = null;
            float? spawnYaw = null;

            if (arguments["spawn"] != null && !LocomotionRecorderOptions.TryParseSpawn(arguments.GetStringOrNull("spawn"), out spawn, out spawnYaw))
            {
                error = "spawn must be a string \"x,y,z\" or \"x,y,z,yaw\" of numbers in scene coordinates, e.g. \"8,0,8,90\".";
                return false;
            }

            if (!arguments.TryGetEnum("input", LocomotionInputSeam.Keyboard, out LocomotionInputSeam inputSeam))
            {
                error = arguments.EnumArgumentError<LocomotionInputSeam>("input");
                return false;
            }

            if (!arguments.TryGetEnum("dt", LocomotionDeltaTime.Real, out LocomotionDeltaTime deltaTime))
            {
                error = arguments.EnumArgumentError<LocomotionDeltaTime>("dt");
                return false;
            }

            if (arguments["animations"] is { Type: not JTokenType.Boolean })
            {
                error = "animations must be true or false.";
                return false;
            }

            string? output = arguments.GetStringOrNull("out");

            if (arguments["out"] != null && (string.IsNullOrWhiteSpace(output) || !Path.IsPathRooted(output)))
            {
                error = "out must be an absolute directory path.";
                return false;
            }

            options = new LocomotionRecorderOptions(
                suite,
                repeats,
                output ?? LocomotionRecorderOptions.DefaultOutputDirectory,
                spawn,
                spawnYaw,
                arguments.GetBool("animations", false),
                inputSeam,
                deltaTime,
                quitWhenDone: false);

            return true;
        }

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.String("suite", "Absolute path of a scenario file (object or array) or a directory of them.", isRequired: true)
                  .Integer("repeats", $"Runs per scenario, 1 to {MAX_REPEATS}. Default 1.")
                  .String("spawn", "Spawn point \"x,y,z[,yaw]\" in coordinates of the scene the player stands in when the suite starts "
                                   + "(yaw: degrees, 0 = +z, clockwise). Default: where the player stands, facing the scenario's camera yaw.")
                  .Enum<LocomotionInputSeam>("input", "keyboard: a virtual keyboard read through the production input actions (default). "
                                                      + "component: the movement input component is written after the input system, no focus needed.")
                  .Enum<LocomotionDeltaTime>("dt", "real: measured frame time (default). fixed: every frame advances exactly 1/fps.")
                  .Boolean("animations", "Own avatar animations during the suite. Default false; the toggle applies again after it.")
                  .String("out", "Absolute output directory; traces go to <out>/unity/. Default: the client's persistent data path /locomotion-traces.");

        private McpToolResult Run(JObject arguments)
        {
            if (!TryParse(arguments, out LocomotionRecorderOptions? options, out string error))
                return McpToolResult.Error(error);

            if (session.State == LocomotionSuiteState.Running)
                return McpToolResult.Error($"Locomotion suite {session.RunId} is running ({session.RunsDone} of {session.RunsTotal} runs done); "
                                           + "call cancel_locomotion_suite first or wait for it with get_locomotion_status.");

            if (!session.TryStart(options, out error))
                return McpToolResult.Error(error);

            string? note = options.InputSeam == LocomotionInputSeam.Keyboard && LocomotionKeyboardInput.EditorWithholdsKeyboard()
                ? "The Game view is not focused now; the first run will wait for it before t = 0."
                : null;

            return McpToolResult.JsonWithStructured(LocomotionStatusReport.Build(session, note));
        }
    }
}
