using DCL.McpServer.Core;
using Newtonsoft.Json.Linq;

namespace DCL.CharacterMotion.LocomotionRecorder
{
    /// <summary>
    ///     The structured result every locomotion MCP tool returns: where the latest suite stands, what it wrote and
    ///     what went wrong. One shape, so an agent reads a run's start, progress, cancellation and toggles alike.
    /// </summary>
    internal static class LocomotionStatusReport
    {
        private const string WAITING_FOR_FOCUS_NOTE = "Paused before t = 0: the Game view is not focused, so the Editor keeps the keyboard. "
                                                      + "Click into the Game view, or call cancel_locomotion_suite and run again with input: component.";

        private const string FOCUS_LOST_NOTE = "The Game view is not focused during a keyboard run: the Editor keeps the keyboard, so this run's "
                                               + "inputMismatchFrames will count the frames that missed their keys.";

        public static JObject Schema() =>
            McpJsonSchema.Object()
                         .Integer("runId", "Id of the latest suite, incremented by every start; null before the first.", nullable: true)
                         .Enum<LocomotionSuiteState>("state", "The latest suite: running, completed, cancelled; idle before the first.")
                         .Enum<LocomotionSuitePhase>("phase", "Where the running suite is; idle when none runs.")
                         .Boolean("waitingForFocus", "A keyboard run is settled and paused until the Game view has focus (Editor only).")
                         .String("status", "The debug panel's status line.")
                         .String("suite", "The suite path.", nullable: true)
                         .String("outputDirectory", "Directory the traces go to, under <out>/unity/.")
                         .Enum<LocomotionInputSeam>("input")
                         .Enum<LocomotionDeltaTime>("dt")
                         .Boolean("animations", "Own avatar animations now: the running suite's setting, otherwise the toggle.")
                         .Boolean("animationsToggle", "The animations setting outside a suite.")
                         .String("scenario", "Id of the scenario being run.", nullable: true)
                         .Integer("scenarioIndex", "1-based index of the scenario being run.", nullable: true)
                         .Integer("scenarioCount")
                         .Integer("repeat", "1-based repeat of the scenario being run.", nullable: true)
                         .Integer("repeats")
                         .Integer("runsDone", "Runs (scenario x repeat) finished, written or failed.")
                         .Integer("runsTotal")
                         .ObjectArray("files", McpJsonSchema.Object()
                                                            .String("path")
                                                            .String("scenario")
                                                            .Integer("repeat")
                                                            .Integer("frames")
                                                            .Integer("dropped", "Frames longer than 1.5 target frame intervals.")
                                                            .Integer("inputMismatchFrames", "Frames whose consumed input differs from the scenario's; 0 is expected."),
                              "Trace files the latest suite wrote.")
                         .ObjectArray("errors", McpJsonSchema.Object()
                                                             .String("scenario")
                                                             .Integer("repeat")
                                                             .String("message"),
                              "Runs of the latest suite that wrote no trace.")
                         .String("note", "What needs attention, if anything.", nullable: true)
                         .Build();

        public static JObject Build(LocomotionSuiteSession session, string? note = null)
        {
            bool running = session.State == LocomotionSuiteState.Running;
            LocomotionRecorderOptions options = session.Options;

            var files = new JArray();

            foreach (LocomotionTraceSummary file in session.WrittenFiles)
                files.Add(new JObject
                {
                    ["path"] = file.Path,
                    ["scenario"] = file.Scenario,
                    ["repeat"] = file.Repeat,
                    ["frames"] = file.Frames,
                    ["dropped"] = file.Dropped,
                    ["inputMismatchFrames"] = file.InputMismatchFrames,
                });

            var errors = new JArray();

            foreach (LocomotionRunError error in session.Errors)
                errors.Add(new JObject
                {
                    ["scenario"] = error.Scenario,
                    ["repeat"] = error.Repeat,
                    ["message"] = error.Message,
                });

            return new JObject
            {
                ["runId"] = session.RunId > 0 ? session.RunId : JValue.CreateNull(),
                ["state"] = McpWireEnum<LocomotionSuiteState>.ToWire(session.State),
                ["phase"] = McpWireEnum<LocomotionSuitePhase>.ToWire(session.Phase),
                ["waitingForFocus"] = session.WaitingForFocus,
                ["status"] = session.Status.Value ?? string.Empty,
                ["suite"] = options.SuitePath != null ? options.SuitePath : JValue.CreateNull(),
                ["outputDirectory"] = options.OutputDirectory,
                ["input"] = McpWireEnum<LocomotionInputSeam>.ToWire(options.InputSeam),
                ["dt"] = McpWireEnum<LocomotionDeltaTime>.ToWire(options.DeltaTime),
                ["animations"] = session.AnimationsEnabled,
                ["animationsToggle"] = session.AnimationsToggle,
                ["scenario"] = running ? session.Scenario.Id : JValue.CreateNull(),
                ["scenarioIndex"] = running ? session.ScenarioIndex + 1 : JValue.CreateNull(),
                ["scenarioCount"] = session.ScenarioCount,
                ["repeat"] = running ? session.Repeat : JValue.CreateNull(),
                ["repeats"] = options.Repeats,
                ["runsDone"] = session.RunsDone,
                ["runsTotal"] = session.RunsTotal,
                ["files"] = files,
                ["errors"] = errors,
                ["note"] = Note(session, note) is { } text ? text : JValue.CreateNull(),
            };
        }

        private static string? Note(LocomotionSuiteSession session, string? note)
        {
            string? focus = session.WaitingForFocus ? WAITING_FOR_FOCUS_NOTE
                : session.Phase == LocomotionSuitePhase.Running && session.Options.InputSeam == LocomotionInputSeam.Keyboard
                                                                && LocomotionKeyboardInput.EditorWithholdsKeyboard() ? FOCUS_LOST_NOTE
                : null;

            if (focus == null) return note;
            if (note == null) return focus;
            return $"{note} {focus}";
        }
    }
}
