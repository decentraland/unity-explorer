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

        /// <summary>The jump key as <c>JumpInputComponent.IsPressed</c> holds it this frame.</summary>
        public bool Jump;

        /// <summary>Scene coordinates.</summary>
        public Vector3 Pos;

        /// <summary>The character's movement velocity (<c>CharacterRigidTransform.MoveVelocity.Velocity</c>).</summary>
        public Vector3 Vel;

        public Vector3 ExternalVel;
        public float GravityVelY;
        public bool Grounded;
        public float YawAvatar;
        public float YawCamera;

        /// <summary>The yaw the scenario holds the camera at this frame (<c>extra.cam.yaw</c> when the scenario has a camera track).</summary>
        public float CamYaw;

        public int PhysicsTick;

        /// <summary>A jump (ground or air) started on a fixed tick since the previous frame.</summary>
        public bool JumpTriggered;

        /// <summary><c>JumpState.JumpCount</c>: jumps since the character last stood on the ground.</summary>
        public int JumpCount;

        public GlideStateValue Glide;

        /// <summary>The own avatar's Animator after this frame's animation update; read only on runs with animations on.</summary>
        public LocomotionAnimationSample Anim;

        /// <summary>The scenario's input at <see cref="T" />, kept to count frames whose consumed input disagrees with it.</summary>
        public LocomotionInputStep Expected;

        public bool MatchesExpectedInput =>
            Sign(Fwd) == Expected.Fwd && Sign(Right) == Expected.Right
                                      && Kind == (!Expected.IsMoving ? MovementKind.Idle
                                             : Expected.Sprint ? MovementKind.Run
                                             : Expected.Walk ? MovementKind.Walk
                                             : MovementKind.Jog)
                                      && Jump == Expected.Jump;

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

        /// <summary>The double jump and gliding features (<c>FeatureId.DoubleJump</c>, <c>FeatureId.Gliding</c>): what a jump press does in the air.</summary>
        public readonly bool DoubleJump;
        public readonly bool Gliding;

        public LocomotionRunInfo(int repeat, bool animationsEnabled, string position, string startedUtc, LocomotionInputSeam inputSeam, bool fixedDeltaTime,
            float physicsDeltaTime, string cameraMode, Vector2Int sceneBaseParcel, Vector3 spawn, float spawnYaw, bool doubleJump, bool gliding)
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
            DoubleJump = doubleJump;
            Gliding = gliding;
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

                AppendFrame(sb, i, in frame, scenario.HasCameraTrack, run.AnimationsEnabled);
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
              .Append(",\"double_jump\":").Append(run.DoubleJump ? "true" : "false")
              .Append(",\"gliding\":").Append(run.Gliding ? "true" : "false");

            // as the Bevy recorder: only a run whose camera turns states how
            if (scenario.HasCameraTrack)
                sb.Append(",\"camera_control\":\"track\"");

            // tells a recorded animation from the offline replay of the controller, which writes the same frame keys
            // anim_weights: the clip weights are the Animator's own, already scaled by the transition share
            if (run.AnimationsEnabled)
                sb.Append(",\"anim_source\":\"capture: Animator base layer after the frame's animation update\",\"anim_weights\":\"animator\"");

            sb.Append("}}\n");
        }

        /// <param name="cameraTrack">The run follows a camera track: the frame adds <c>extra.cam.yaw</c>, the yaw the track holds.</param>
        /// <param name="animations">The run has animations on: the frame adds the Animator's state (<see cref="AppendAnimation" />).</param>
        private static void AppendFrame(StringBuilder sb, int index, in LocomotionFrameSample frame, bool cameraTrack, bool animations)
        {
            sb.Append("{\"type\":\"frame\",\"i\":").Append(index)
              .Append(",\"t\":").AppendFloat(frame.T)
              .Append(",\"dt\":").AppendFloat(frame.Dt)
              .Append(",\"in\":{\"fwd\":").AppendFloat(frame.Fwd)
              .Append(",\"right\":").AppendFloat(frame.Right)
              .Append(",\"walk\":").Append(frame.Kind == MovementKind.Walk ? "true" : "false")
              .Append(",\"sprint\":").Append(frame.Kind == MovementKind.Run ? "true" : "false")
              .Append(",\"jump\":").Append(frame.Jump ? "true" : "false")
              .Append("},\"pos\":").AppendVector(frame.Pos)
              .Append(",\"vel\":").AppendVector(frame.Vel)
              .Append(",\"grounded\":").Append(frame.Grounded ? "true" : "false")
              .Append(",\"yaw_avatar\":").AppendFloat(frame.YawAvatar)
              .Append(",\"yaw_camera\":").AppendFloat(frame.YawCamera)
              .Append(",\"extra\":{\"kind\":\"").Append(KindName(frame.Kind)).Append('"')
              .Append(",\"physics_tick\":").Append(frame.PhysicsTick)
              .Append(",\"vel_external\":").AppendVector(frame.ExternalVel)
              .Append(",\"vel_gravity_y\":").AppendFloat(frame.GravityVelY)
              .Append(",\"jump_triggered\":").Append(frame.JumpTriggered ? "true" : "false")
              .Append(",\"jump_count\":").Append(frame.JumpCount)
              .Append(",\"glide\":\"").Append(GlideName(frame.Glide)).Append('"');

            if (cameraTrack)
                sb.Append(",\"cam\":{\"yaw\":").AppendFloat(frame.CamYaw).Append('}');

            if (animations)
                AppendAnimation(sb, in frame.Anim);

            sb.Append("}}\n");
        }

        /// <summary>
        ///     <c>anim</c> [{clip, w, t, speed}] (w and t to 4 decimals, speed to 3), <c>anim_params</c>, <c>anim_state</c>
        ///     {state, next, progress, nt, next_nt, cur_w, next_w, and cur_empty: true when the current state reported no clip
        ///     weight} and <c>anim_layers</c> {point, rotation}; <c>"anim":null</c>
        ///     alone when the Animator did not run this frame.
        /// </summary>
        private static void AppendAnimation(StringBuilder sb, in LocomotionAnimationSample anim)
        {
            if (!anim.Present)
            {
                sb.Append(",\"anim\":null");
                return;
            }

            sb.Append(",\"anim\":[");

            for (var i = 0; i < anim.Clips.Length; i++)
            {
                LocomotionAnimClip clip = anim.Clips[i];

                if (i > 0)
                    sb.Append(',');

                sb.Append("{\"clip\":").Append(JsonConvert.ToString(clip.Clip))
                  .Append(",\"w\":").AppendRounded(clip.W, "0.0###")
                  .Append(",\"t\":").AppendRounded(clip.T, "0.0###")
                  .Append(",\"speed\":").AppendRounded(clip.Speed, "0.0##")
                  .Append('}');
            }

            sb.Append("],\"anim_params\":{\"MovementBlend\":").AppendRounded(anim.MovementBlend, "0.0###")
              .Append(",\"SlideBlend\":").AppendRounded(anim.SlideBlend, "0.0###")
              .Append(",\"JumpCount\":").Append(anim.JumpCount)
              .Append(",\"IsGrounded\":").Append(anim.IsGrounded ? "true" : "false")
              .Append(",\"IsFalling\":").Append(anim.IsFalling ? "true" : "false")
              .Append(",\"IsLongFall\":").Append(anim.IsLongFall ? "true" : "false")
              .Append(",\"IsStunned\":").Append(anim.IsStunned ? "true" : "false")
              .Append(",\"GlideBlend\":").AppendRounded(anim.GlideBlend, "0.0###")
              .Append("},\"anim_state\":{\"state\":").Append(JsonConvert.ToString(LocomotionAnimationSample.StateName(anim.State)))
              .Append(",\"next\":").Append(anim.Next != 0 ? JsonConvert.ToString(LocomotionAnimationSample.StateName(anim.Next)) : "null")
              .Append(",\"progress\":").AppendFloat(anim.Progress)
              .Append(",\"nt\":").AppendFloat(anim.StateTime)
              .Append(",\"next_nt\":").AppendFloat(anim.NextTime)
              .Append(",\"cur_w\":").AppendFloat(anim.CurrentClipWeight)
              .Append(",\"next_w\":").AppendFloat(anim.NextClipWeight);

            if (anim.CurrentClipsMissing)
                sb.Append(",\"cur_empty\":true");

            sb.Append("},\"anim_layers\":{\"point\":").AppendFloat(anim.PointLayerWeight)
              .Append(",\"rotation\":").AppendFloat(anim.RotationLayerWeight)
              .Append('}');
        }

        private static string KindName(MovementKind kind) =>
            kind switch
            {
                MovementKind.Walk => "walk",
                MovementKind.Jog => "jog",
                MovementKind.Run => "run",
                _ => "idle",
            };

        private static string GlideName(GlideStateValue glide) =>
            glide switch
            {
                GlideStateValue.OpeningProp => "opening",
                GlideStateValue.Gliding => "gliding",
                GlideStateValue.ClosingProp => "closing",
                _ => "closed",
            };

        private static StringBuilder AppendVector(this StringBuilder sb, Vector3 v) =>
            sb.Append('[').AppendFloat(v.x).Append(',').AppendFloat(v.y).Append(',').AppendFloat(v.z).Append(']');

        private static StringBuilder AppendFloat(this StringBuilder sb, float value) =>
            float.IsNaN(value) || float.IsInfinity(value)
                ? sb.Append("null")
                : sb.Append(value.ToString("0.0######", CultureInfo.InvariantCulture));

        private static StringBuilder AppendRounded(this StringBuilder sb, float value, string format) =>
            float.IsNaN(value) || float.IsInfinity(value)
                ? sb.Append("null")
                : sb.Append(value.ToString(format, CultureInfo.InvariantCulture));
    }
}
