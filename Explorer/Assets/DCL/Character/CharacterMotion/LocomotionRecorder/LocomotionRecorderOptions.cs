using DCL.Diagnostics;
using Global.AppArgs;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using UnityEngine;
using Flags = Global.AppArgs.AppArgsFlags.Locomotion;

namespace DCL.CharacterMotion.LocomotionRecorder
{
    public enum LocomotionInputSeam
    {
        /// <summary>Key states queued on a virtual keyboard device, read through the production DCLInput actions.</summary>
        Keyboard,

        /// <summary><see cref="Components.MovementInputComponent" /> overwritten after the production input system.</summary>
        Component,
    }

    public enum LocomotionDeltaTime
    {
        /// <summary>Frames advance by the measured frame time.</summary>
        Real,

        /// <summary>Every frame advances exactly 1/fps (<c>Time.captureDeltaTime</c>).</summary>
        Fixed,
    }

    /// <summary>
    ///     How one suite is run: from the launch flags (<see cref="AppArgsFlags.Locomotion" />) or from the
    ///     <c>run_locomotion_suite</c> MCP tool.
    /// </summary>
    public sealed class LocomotionRecorderOptions
    {
        public readonly string? SuitePath;
        public readonly int Repeats;
        public readonly string OutputDirectory;

        /// <summary>Spawn point in scene coordinates (relative to the base parcel of the scene the suite starts in); null keeps where the player stands.</summary>
        public readonly Vector3? Spawn;

        /// <summary>Avatar facing at spawn; null faces the scenario's camera yaw.</summary>
        public readonly float? SpawnYaw;

        /// <summary>Own avatar animations while the suite runs.</summary>
        public readonly bool AnimationsEnabled;

        public readonly LocomotionInputSeam InputSeam;
        public readonly LocomotionDeltaTime DeltaTime;
        public readonly bool QuitWhenDone;

        public bool FixedDeltaTime => DeltaTime == LocomotionDeltaTime.Fixed;

        public static string DefaultOutputDirectory => Path.Combine(Application.persistentDataPath, "locomotion-traces");

        internal LocomotionRecorderOptions(string? suitePath, int repeats, string outputDirectory, Vector3? spawn, float? spawnYaw, bool animationsEnabled,
            LocomotionInputSeam inputSeam, LocomotionDeltaTime deltaTime, bool quitWhenDone)
        {
            SuitePath = suitePath;
            Repeats = repeats;
            OutputDirectory = outputDirectory;
            Spawn = spawn;
            SpawnYaw = spawnYaw;
            AnimationsEnabled = animationsEnabled;
            InputSeam = inputSeam;
            DeltaTime = deltaTime;
            QuitWhenDone = quitWhenDone;
        }

        /// <summary>The same suite with another animations setting (the debug panel re-runs a suite with its own toggle).</summary>
        public LocomotionRecorderOptions WithAnimations(bool animationsEnabled) =>
            animationsEnabled == AnimationsEnabled
                ? this
                : new LocomotionRecorderOptions(SuitePath, Repeats, OutputDirectory, Spawn, SpawnYaw, animationsEnabled, InputSeam, DeltaTime, QuitWhenDone);

        /// <summary>The recorder is wanted when a suite is named or the animations toggle is set (manual checks need no suite).</summary>
        public static bool TryCreate(IAppArgs appArgs, [NotNullWhen(true)] out LocomotionRecorderOptions? options)
        {
            options = null;

            if (!appArgs.HasFlag(Flags.SUITE) && !appArgs.HasFlag(Flags.ANIMATIONS))
                return false;

            string? suitePath = appArgs.TryGetValue(Flags.SUITE, out string? suite) && !string.IsNullOrWhiteSpace(suite) ? suite : null;

            int repeats = appArgs.TryGetValue(Flags.REPEATS, out string? rawRepeats)
                          && int.TryParse(rawRepeats, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedRepeats) && parsedRepeats > 0
                ? parsedRepeats
                : 1;

            string outputDirectory = appArgs.TryGetValue(Flags.OUTPUT, out string? output) && !string.IsNullOrWhiteSpace(output)
                ? output
                : DefaultOutputDirectory;

            Vector3? spawn = null;
            float? spawnYaw = null;

            if (appArgs.TryGetValue(Flags.SPAWN, out string? rawSpawn) && !TryParseSpawn(rawSpawn, out spawn, out spawnYaw))
                ReportHub.LogError(ReportCategory.MCP, $"--{Flags.SPAWN} '{rawSpawn}' is not x,y,z[,yaw]; the suite starts where the player stands");

            options = new LocomotionRecorderOptions(
                suitePath,
                repeats,
                outputDirectory,
                spawn,
                spawnYaw,
                appArgs.HasFlagWithValueTrue(Flags.ANIMATIONS),
                appArgs.TryGetValue(Flags.INPUT, out string? seam) && seam == "component" ? LocomotionInputSeam.Component : LocomotionInputSeam.Keyboard,
                appArgs.TryGetValue(Flags.DELTA_TIME, out string? dt) && dt == "fixed" ? LocomotionDeltaTime.Fixed : LocomotionDeltaTime.Real,
                appArgs.HasFlag(Flags.QUIT));

            return true;
        }

        internal static bool TryParseSpawn(string? raw, out Vector3? spawn, out float? yaw)
        {
            spawn = null;
            yaw = null;

            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string[] parts = raw.Split(',');

            if (parts.Length is not (3 or 4))
                return false;

            var values = new float[parts.Length];

            for (var i = 0; i < parts.Length; i++)
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                    return false;

            spawn = new Vector3(values[0], values[1], values[2]);

            if (parts.Length == 4)
                yaw = values[3];

            return true;
        }
    }
}
