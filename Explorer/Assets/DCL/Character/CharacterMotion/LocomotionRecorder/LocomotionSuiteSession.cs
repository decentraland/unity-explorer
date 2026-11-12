using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using Flags = Global.AppArgs.AppArgsFlags.Locomotion;

namespace DCL.CharacterMotion.LocomotionRecorder
{
    public enum LocomotionSuitePhase
    {
        /// <summary>No suite is running; the recorder drives nothing.</summary>
        Idle,

        /// <summary>The suite waits for the world to finish loading and the player to stand on the ground.</summary>
        WaitingForWorld,

        /// <summary>The player is being teleported to the spawn point.</summary>
        Spawning,

        /// <summary>Idle input at spawn for the scenario's settle time.</summary>
        Settling,

        /// <summary>Settled; the next frame is the scenario's t = 0.</summary>
        Armed,

        /// <summary>The scenario's input is played and every frame recorded.</summary>
        Running,
    }

    /// <summary>Where the latest suite stands.</summary>
    public enum LocomotionSuiteState
    {
        /// <summary>No suite was started in this session.</summary>
        Idle,
        Running,
        Completed,
        Cancelled,
    }

    /// <summary>One trace file a suite wrote.</summary>
    public readonly struct LocomotionTraceSummary
    {
        public readonly string Path;
        public readonly string Scenario;
        public readonly int Repeat;
        public readonly int Frames;
        public readonly int Dropped;
        public readonly int InputMismatchFrames;

        public LocomotionTraceSummary(string path, string scenario, int repeat, int frames, int dropped, int inputMismatchFrames)
        {
            Path = path;
            Scenario = scenario;
            Repeat = repeat;
            Frames = frames;
            Dropped = dropped;
            InputMismatchFrames = inputMismatchFrames;
        }
    }

    /// <summary>A run that did not produce its trace.</summary>
    public readonly struct LocomotionRunError
    {
        public readonly string Scenario;
        public readonly int Repeat;
        public readonly string Message;

        public LocomotionRunError(string scenario, int repeat, string message)
        {
            Scenario = scenario;
            Repeat = repeat;
            Message = message;
        }
    }

    /// <summary>
    ///     State of the locomotion suite shared by the recorder's systems, its input seams, the debug panel and the MCP
    ///     tools. The suite is driven by <c>LocomotionSuiteSystem</c>; this object holds where the suite is, answers what
    ///     input it wants and keeps what the latest suite wrote. Inert until a suite starts: the virtual keyboard is added
    ///     by the first keyboard suite, and the animations toggle starts as the launch flags set it (on when the recorder
    ///     exists only for the MCP tools).
    /// </summary>
    public sealed class LocomotionSuiteSession : IDisposable
    {
        /// <summary>Extra time a run waits for the player to be grounded after the settle before it starts anyway.</summary>
        internal const float MAX_EXTRA_SETTLE = 5f;

        /// <summary>Time the world must have been loaded before the suite captures its origin, so scene colliders are in place.</summary>
        internal const float WORLD_READY_DELAY = 2f;

        public readonly ElementBinding<string> Status = new (string.Empty);

        /// <summary>The debug panel's animations toggle, kept in step with <see cref="AnimationsToggle" />.</summary>
        public readonly ElementBinding<bool> AnimationsToggleBinding;

        internal readonly List<LocomotionFrameSample> Frames = new (4096);

        private readonly List<LocomotionTraceSummary> writtenFiles = new ();
        private readonly List<LocomotionRunError> errors = new ();

        private List<LocomotionScenario> scenarios = new ();
        private LocomotionKeyboardInput? keyboardInput;

        private int savedTargetFrameRate;
        private int savedVSyncCount;
        private float savedCaptureDeltaTime;
        private bool frameRateLocked;

        /// <summary>The latest suite's settings; before any suite, the launch flags' (or the defaults).</summary>
        public LocomotionRecorderOptions Options { get; private set; }

        /// <summary>Own avatar animations now: a running suite's setting, otherwise the toggle.</summary>
        public bool AnimationsEnabled => State == LocomotionSuiteState.Running ? Options.AnimationsEnabled : AnimationsToggle;

        /// <summary>The animations setting outside a suite (debug panel toggle, <c>set_avatar_animations</c>).</summary>
        public bool AnimationsToggle { get; private set; }

        public LocomotionSuitePhase Phase { get; internal set; }

        public LocomotionSuiteState State { get; private set; }

        /// <summary>Increments with every started suite; 0 before the first.</summary>
        public int RunId { get; private set; }

        /// <summary>A keyboard run is settled but paused: the Editor keeps keyboard input while the Game view is unfocused.</summary>
        public bool WaitingForFocus { get; internal set; }

        public IReadOnlyList<LocomotionTraceSummary> WrittenFiles => writtenFiles;

        public IReadOnlyList<LocomotionRunError> Errors => errors;

        /// <summary>Runs (scenario x repeat) of the latest suite that finished, written or failed.</summary>
        public int RunsDone { get; private set; }

        public int RunsTotal => scenarios.Count * Options.Repeats;

        public int ScenarioCount => scenarios.Count;

        /// <summary>0-based; meaningful while <see cref="State" /> is running.</summary>
        public int ScenarioIndex { get; private set; }

        /// <summary>1-based, as in the trace file name.</summary>
        public int Repeat { get; private set; }

        internal LocomotionScenario Scenario => scenarios[ScenarioIndex];

        /// <summary>Scene base the suite's coordinates are relative to; captured once per suite.</summary>
        internal Vector3 SceneOrigin { get; set; }

        internal Vector2Int SceneBaseParcel { get; set; }

        internal string PositionDescription { get; set; } = string.Empty;

        /// <summary>Spawn point in scene coordinates.</summary>
        internal Vector3 Spawn { get; set; }

        internal float? SpawnYaw { get; set; }

        internal double PhaseStartTime { get; set; }

        internal int ArmedFrame { get; set; }

        /// <summary><c>UnityEngine.Time.timeAsDouble</c> of the scenario's t = 0 frame.</summary>
        internal double RunStartTime { get; private set; }

        internal string RunStartedUtc { get; private set; } = string.Empty;

        /// <param name="options">The suite the launch flags name, or the defaults when the recorder exists only for the MCP tools.</param>
        /// <param name="animationsEnabled">The animations toggle's initial state.</param>
        public LocomotionSuiteSession(LocomotionRecorderOptions options, bool animationsEnabled)
        {
            Options = options;
            AnimationsToggle = animationsEnabled;
            AnimationsToggleBinding = new ElementBinding<bool>(animationsEnabled, evt => SetAnimationsToggle(evt.newValue));
        }

        public void Dispose()
        {
            keyboardInput?.Dispose();
            keyboardInput = null;
            RestoreFrameRate();
        }

        /// <summary>Re-runs the latest suite with the debug panel's animations toggle; logs instead of failing.</summary>
        public void RequestStart()
        {
            if (Options.SuitePath == null)
            {
                ReportHub.LogWarning(ReportCategory.MCP, $"No locomotion suite to run: launch with --{Flags.SUITE} <directory or file> or call the run_locomotion_suite MCP tool");
                return;
            }

            if (!TryStart(Options.WithAnimations(AnimationsToggle), out string error))
                SetStatus(error);
        }

        /// <summary>
        ///     Loads the suite and starts it once the world is ready; a running suite is replaced (its partial run is
        ///     discarded). False, with the reason (also logged), when the suite cannot be loaded.
        /// </summary>
        public bool TryStart(LocomotionRecorderOptions options, out string error)
        {
            if (!TryLoad(options, out List<LocomotionScenario>? loaded, out error))
            {
                ReportHub.LogWarning(ReportCategory.MCP, error);
                return false;
            }

            if (options.InputSeam == LocomotionInputSeam.Keyboard)
                keyboardInput ??= new LocomotionKeyboardInput(this);

            Options = options;
            scenarios = loaded;
            RunId++;
            State = LocomotionSuiteState.Running;
            ScenarioIndex = 0;
            Repeat = 1;
            RunsDone = 0;
            writtenFiles.Clear();
            errors.Clear();
            Frames.Clear();
            WaitingForFocus = false;
            Phase = LocomotionSuitePhase.WaitingForWorld;
            PhaseStartTime = -1;
            SetStatus($"waiting for the world ({scenarios.Count} scenarios x {options.Repeats})");

            ReportHub.Log(LogType.Log, ReportCategory.MCP,
                $"Locomotion suite {RunId} started: '{options.SuitePath}', {scenarios.Count} scenarios x {options.Repeats}, input {options.InputSeam}, dt {options.DeltaTime}, output {options.OutputDirectory}");

            return true;
        }

        /// <summary>Stops the running suite: the run in progress is discarded, files already written stay. False when none runs.</summary>
        public bool Cancel()
        {
            if (State != LocomotionSuiteState.Running)
                return false;

            Frames.Clear();
            EndSuite(LocomotionSuiteState.Cancelled);
            SetStatus($"cancelled: {RunsDone} of {RunsTotal} runs done");
            ReportHub.Log(LogType.Log, ReportCategory.MCP, $"Locomotion suite {RunId} cancelled after {RunsDone} of {RunsTotal} runs");
            return true;
        }

        /// <summary>Sets the animations toggle; a running suite keeps its own setting and the toggle applies after it.</summary>
        public void SetAnimationsToggle(bool enabled)
        {
            if (AnimationsToggle == enabled)
                return;

            AnimationsToggle = enabled;
            AnimationsToggleBinding.Value = enabled;
        }

        /// <summary>
        ///     The keys the suite holds this frame, or false when it drives nothing. Called by the input seam in the
        ///     frame's input phase; the first call after arming makes the current frame the scenario's t = 0.
        /// </summary>
        public bool TryGetDrivenInput(out LocomotionInputStep step)
        {
            TryStartRun();

            switch (Phase)
            {
                case LocomotionSuitePhase.Running:
                    step = Scenario.SampleAt(RunTime());
                    return true;
                case LocomotionSuitePhase.Spawning:
                case LocomotionSuitePhase.Settling:
                case LocomotionSuitePhase.Armed:
                    step = LocomotionInputStep.IDLE;
                    return true;
                default:
                    step = default(LocomotionInputStep);
                    return false;
            }
        }

        public bool TryGetHeldCameraYaw(out float yaw)
        {
            bool held = Phase is LocomotionSuitePhase.Spawning or LocomotionSuitePhase.Settling or LocomotionSuitePhase.Armed or LocomotionSuitePhase.Running;
            yaw = held ? Scenario.CameraYaw : 0f;
            return held;
        }

        /// <summary>Moves to the next run; false when the suite is complete.</summary>
        internal bool AdvanceRun()
        {
            RunsDone++;

            if (Repeat < Options.Repeats)
            {
                Repeat++;
                return true;
            }

            Repeat = 1;
            ScenarioIndex++;
            return ScenarioIndex < scenarios.Count;
        }

        internal void AddWrittenFile(in LocomotionTraceSummary file)
        {
            writtenFiles.Add(file);
        }

        internal void AddError(string message)
        {
            errors.Add(new LocomotionRunError(Scenario.Id, Repeat, message));
        }

        internal void Complete()
        {
            EndSuite(LocomotionSuiteState.Completed);
        }

        /// <summary>Starts the run on the first frame after the one that armed it; idempotent within the frame.</summary>
        internal void TryStartRun()
        {
            if (Phase != LocomotionSuitePhase.Armed || UnityEngine.Time.frameCount <= ArmedFrame)
                return;

            RunStartTime = UnityEngine.Time.timeAsDouble;
            RunStartedUtc = DateTime.UtcNow.ToString("o");
            Frames.Clear();
            Phase = LocomotionSuitePhase.Running;
        }

        internal float RunTime() =>
            (float)(UnityEngine.Time.timeAsDouble - RunStartTime);

        /// <summary>Locks the frame rate to the scenario (vsync off); re-asserted every frame, since quality settings may re-apply theirs.</summary>
        internal void LockFrameRate(int fps)
        {
            if (!frameRateLocked)
            {
                savedTargetFrameRate = Application.targetFrameRate;
                savedVSyncCount = QualitySettings.vSyncCount;
                savedCaptureDeltaTime = UnityEngine.Time.captureDeltaTime;
                frameRateLocked = true;
            }

            if (QualitySettings.vSyncCount != 0)
                QualitySettings.vSyncCount = 0;

            if (Application.targetFrameRate != fps)
                Application.targetFrameRate = fps;

            float captureDeltaTime = Options.FixedDeltaTime ? 1f / fps : 0f;

            if (!Mathf.Approximately(UnityEngine.Time.captureDeltaTime, captureDeltaTime))
                UnityEngine.Time.captureDeltaTime = captureDeltaTime;
        }

        internal void RestoreFrameRate()
        {
            if (!frameRateLocked)
                return;

            QualitySettings.vSyncCount = savedVSyncCount;
            Application.targetFrameRate = savedTargetFrameRate;
            UnityEngine.Time.captureDeltaTime = savedCaptureDeltaTime;
            frameRateLocked = false;
        }

        internal void SetStatus(string status)
        {
            Status.Value = status;
        }

        private static bool TryLoad(LocomotionRecorderOptions options, [NotNullWhen(true)] out List<LocomotionScenario>? scenarios, out string error)
        {
            scenarios = null;

            if (options.SuitePath == null)
            {
                error = "No suite path given";
                return false;
            }

            try { scenarios = LocomotionScenario.LoadSuite(options.SuitePath); }
            catch (Exception e)
            {
                error = $"The locomotion suite '{options.SuitePath}' failed to load: {e.Message}";
                return false;
            }

            if (scenarios.Count == 0)
            {
                error = $"The locomotion suite '{options.SuitePath}' holds no scenarios";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void EndSuite(LocomotionSuiteState state)
        {
            State = state;
            Phase = LocomotionSuitePhase.Idle;
            WaitingForFocus = false;
            RestoreFrameRate();
        }
    }
}
