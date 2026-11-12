using DCL.CharacterMotion.Components;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace DCL.CharacterMotion.LocomotionRecorder
{
    /// <summary>One rendered frame, captured after the character moved and rotated and the camera updated.</summary>
    public struct LocomotionFrameSample
    {
        public float T;
        public float Dt;

        /// <summary>The <see cref="MovementInputComponent" /> this frame's input systems produced: Axes.y (W/S) and Axes.x (D/A).</summary>
        public float Fwd;
        public float Right;
        public MovementKind Kind;

        /// <summary>Scene coordinates.</summary>
        public Vector3 Pos;

        /// <summary>The character's movement velocity (<c>CharacterRigidTransform.MoveVelocity.Velocity</c>).</summary>
        public Vector3 Vel;

        public Vector3 ExternalVel;
        public float GravityVelY;
        public bool Grounded;
        public float YawAvatar;
        public float YawCamera;
        public int PhysicsTick;

        /// <summary>The scenario's input at <see cref="T" />, kept to count frames whose consumed input disagrees with it.</summary>
        public LocomotionInputStep Expected;

        public bool MatchesExpectedInput =>
            Sign(Fwd) == Expected.Fwd && Sign(Right) == Expected.Right
                                      && Kind == (!Expected.IsMoving ? MovementKind.Idle
                                             : Expected.Sprint ? MovementKind.Run
                                             : Expected.Walk ? MovementKind.Walk
                                             : MovementKind.Jog);

        private static int Sign(float axis) =>
            axis > 0.5f ? 1 : axis < -0.5f ? -1 : 0;
    }

    /// <summary>What the trace header states about a run beyond the scenario itself.</summary>
    public readonly struct LocomotionRunInfo
    {
        public readonly int Repeat;
        public readonly bool AnimationsEnabled;
        public readonly string Position;
        public readonly string StartedUtc;
        public readonly LocomotionInputSeam InputSeam;
        public readonly bool FixedDeltaTime;
        public readonly float PhysicsDeltaTime;
        public readonly string CameraMode;
        public readonly Vector2Int SceneBaseParcel;
        public readonly Vector3 Spawn;
        public readonly float SpawnYaw;

        public LocomotionRunInfo(int repeat, bool animationsEnabled, string position, string startedUtc, LocomotionInputSeam inputSeam, bool fixedDeltaTime,
            float physicsDeltaTime, string cameraMode, Vector2Int sceneBaseParcel, Vector3 spawn, float spawnYaw)
        {
            Repeat = repeat;
            AnimationsEnabled = animationsEnabled;
            Position = position;
            StartedUtc = startedUtc;
            InputSeam = inputSeam;
            FixedDeltaTime = fixedDeltaTime;
            PhysicsDeltaTime = physicsDeltaTime;
            CameraMode = cameraMode;
            SceneBaseParcel = sceneBaseParcel;
            Spawn = spawn;
            SpawnYaw = spawnYaw;
        }
    }

    /// <summary>Writes a run as trace format v1 JSON Lines: header, one line per frame, footer.</summary>
    public static class LocomotionTraceWriter
    {
        public const string CLIENT = "unity";

        /// <summary>A frame counts as dropped when it took longer than this many target frame intervals.</summary>
        private const float DROPPED_FRAME_FACTOR = 1.5f;

        public static string FileName(LocomotionScenario scenario, int repeat) =>
            $"{scenario.Id}__fps{scenario.Fps}__r{repeat}.jsonl";

        /// <returns>The written file: its path and the footer's counts.</returns>
        public static LocomotionTraceSummary Write(string outputDirectory, LocomotionScenario scenario, in LocomotionRunInfo run, IReadOnlyList<LocomotionFrameSample> frames)
        {
            string directory = Path.Combine(outputDirectory, CLIENT);
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, FileName(scenario, run.Repeat));

            var sb = new StringBuilder(256 * (frames.Count + 2));
            AppendHeader(sb, scenario, in run);

            var inputMismatchFrames = 0;
            var dropped = 0;
            float droppedThreshold = DROPPED_FRAME_FACTOR / scenario.Fps;

            for (var i = 0; i < frames.Count; i++)
            {
                LocomotionFrameSample frame = frames[i];

                if (!frame.MatchesExpectedInput)
                    inputMismatchFrames++;

                if (i > 0 && frame.Dt > droppedThreshold)
                    dropped++;

                AppendFrame(sb, i, in frame);
            }

            sb.Append("{\"type\":\"footer\",\"frames\":").Append(frames.Count)
              .Append(",\"dropped\":").Append(dropped)
              .Append(",\"extra\":{\"input_mismatch_frames\":").Append(inputMismatchFrames)
              .Append("}}\n");

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            return new LocomotionTraceSummary(path, scenario.Id, run.Repeat, frames.Count, dropped, inputMismatchFrames);
        }

        private static void AppendHeader(StringBuilder sb, LocomotionScenario scenario, in LocomotionRunInfo run)
        {
            sb.Append("{\"type\":\"header\",\"format\":").Append(LocomotionScenario.FORMAT)
              .Append(",\"client\":\"").Append(CLIENT).Append('"')
              .Append(",\"client_version\":").Append(JsonConvert.ToString($"{Application.version} ({(Application.isEditor ? "editor" : "player")}, Unity {Application.unityVersion})"))
              .Append(",\"scenario\":").Append(JsonConvert.ToString(scenario.Id))
              .Append(",\"seed\":").Append(scenario.Seed)
              .Append(",\"fps_target\":").Append(scenario.Fps)
              .Append(",\"animations\":").Append(run.AnimationsEnabled ? "true" : "false")
              .Append(",\"position\":").Append(JsonConvert.ToString(run.Position))
              .Append(",\"started_utc\":").Append(JsonConvert.ToString(run.StartedUtc))
              .Append(",\"extra\":{\"repeat\":").Append(run.Repeat)
              .Append(",\"camera_yaw\":").AppendFloat(scenario.CameraYaw)
              .Append(",\"settle\":").AppendFloat(scenario.Settle)
              .Append(",\"duration\":").AppendFloat(scenario.Duration)
              .Append(",\"input_seam\":\"").Append(run.InputSeam == LocomotionInputSeam.Keyboard ? "keyboard" : "component").Append('"')
              .Append(",\"dt_mode\":\"").Append(run.FixedDeltaTime ? "fixed" : "real").Append('"')
              .Append(",\"physics_dt\":").AppendFloat(run.PhysicsDeltaTime)
              .Append(",\"camera_mode\":\"").Append(run.CameraMode).Append('"')
              .Append(",\"scene_base_parcel\":[").Append(run.SceneBaseParcel.x).Append(',').Append(run.SceneBaseParcel.y).Append(']')
              .Append(",\"spawn\":").AppendVector(run.Spawn)
              .Append(",\"spawn_yaw\":").AppendFloat(run.SpawnYaw)
              .Append("}}\n");
        }

        private static void AppendFrame(StringBuilder sb, int index, in LocomotionFrameSample frame)
        {
            sb.Append("{\"type\":\"frame\",\"i\":").Append(index)
              .Append(",\"t\":").AppendFloat(frame.T)
              .Append(",\"dt\":").AppendFloat(frame.Dt)
              .Append(",\"in\":{\"fwd\":").AppendFloat(frame.Fwd)
              .Append(",\"right\":").AppendFloat(frame.Right)
              .Append(",\"walk\":").Append(frame.Kind == MovementKind.Walk ? "true" : "false")
              .Append(",\"sprint\":").Append(frame.Kind == MovementKind.Run ? "true" : "false")
              .Append("},\"pos\":").AppendVector(frame.Pos)
              .Append(",\"vel\":").AppendVector(frame.Vel)
              .Append(",\"grounded\":").Append(frame.Grounded ? "true" : "false")
              .Append(",\"yaw_avatar\":").AppendFloat(frame.YawAvatar)
              .Append(",\"yaw_camera\":").AppendFloat(frame.YawCamera)
              .Append(",\"extra\":{\"kind\":\"").Append(KindName(frame.Kind)).Append('"')
              .Append(",\"physics_tick\":").Append(frame.PhysicsTick)
              .Append(",\"vel_external\":").AppendVector(frame.ExternalVel)
              .Append(",\"vel_gravity_y\":").AppendFloat(frame.GravityVelY)
              .Append("}}\n");
        }

        private static string KindName(MovementKind kind) =>
            kind switch
            {
                MovementKind.Walk => "walk",
                MovementKind.Jog => "jog",
                MovementKind.Run => "run",
                _ => "idle",
            };

        private static StringBuilder AppendVector(this StringBuilder sb, Vector3 v) =>
            sb.Append('[').AppendFloat(v.x).Append(',').AppendFloat(v.y).Append(',').AppendFloat(v.z).Append(']');

        private static StringBuilder AppendFloat(this StringBuilder sb, float value) =>
            float.IsNaN(value) || float.IsInfinity(value)
                ? sb.Append("null")
                : sb.Append(value.ToString("0.0######", CultureInfo.InvariantCulture));
    }
}
