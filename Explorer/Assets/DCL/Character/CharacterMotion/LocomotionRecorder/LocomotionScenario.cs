using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DCL.CharacterMotion.LocomotionRecorder
{
    /// <summary>One input state of a scenario: it holds from <see cref="T" /> until the next step.</summary>
    public readonly struct LocomotionInputStep
    {
        public static readonly LocomotionInputStep IDLE = new (0f, 0, 0, false, false);

        public readonly float T;

        /// <summary>W = 1, S = -1.</summary>
        public readonly int Fwd;

        /// <summary>D = 1, A = -1.</summary>
        public readonly int Right;

        public readonly bool Walk;
        public readonly bool Sprint;

        /// <summary>The jump key held; a press is the step that sets it after one that did not.</summary>
        public readonly bool Jump;

        public LocomotionInputStep(float t, int fwd, int right, bool walk, bool sprint, bool jump = false)
        {
            T = t;
            Fwd = fwd;
            Right = right;
            Walk = walk;
            Sprint = sprint;
            Jump = jump;
        }

        public bool IsMoving => Fwd != 0 || Right != 0;
    }

    /// <summary>A camera track sample: the camera's world yaw at <see cref="T" />, degrees clockwise from north (+z).</summary>
    public readonly struct LocomotionCameraSample
    {
        public readonly float T;
        public readonly float Yaw;

        public LocomotionCameraSample(float t, float yaw)
        {
            T = t;
            Yaw = yaw;
        }
    }

    /// <summary>
    ///     A locomotion scenario (shared with the Bevy recorder): a step function of digital keys (movement, walk, sprint
    ///     and jump), played at a locked frame rate after an idle settle under a held camera yaw, or, in format 2, a
    ///     camera yaw that follows a track by time.
    /// </summary>
    public sealed class LocomotionScenario
    {
        /// <summary>The trace format, and the scenario format without a camera track.</summary>
        public const int FORMAT = 1;

        /// <summary>The scenario format that may carry a camera track (<c>camera: [{t, yaw, pitch?}]</c>).</summary>
        public const int CAMERA_TRACK_FORMAT = 2;

        /// <summary>
        ///     A frame clock accumulates rounding error, so a frame within this much before an event's time (far less than a
        ///     frame) counts as reaching it; otherwise an event on a frame boundary (t = 2.0 at 60 fps) lands on either frame.
        /// </summary>
        public const float TIME_TOLERANCE = 1e-4f;

        public readonly string Id;
        public readonly long Seed;
        public readonly int Fps;
        public readonly float CameraYaw;
        public readonly float Settle;
        public readonly float Duration;
        public readonly IReadOnlyList<LocomotionInputStep> Events;

        /// <summary>Format 2: the camera's yaw by time, ordered by t; empty when the camera is held at <see cref="CameraYaw" />.</summary>
        public readonly IReadOnlyList<LocomotionCameraSample> CameraTrack;

        private LocomotionScenario(string id, long seed, int fps, float cameraYaw, float settle, float duration, IReadOnlyList<LocomotionInputStep> events,
            IReadOnlyList<LocomotionCameraSample> cameraTrack)
        {
            Id = id;
            Seed = seed;
            Fps = fps;
            CameraYaw = cameraYaw;
            Settle = settle;
            Duration = duration;
            Events = events;
            CameraTrack = cameraTrack;
        }

        public bool HasCameraTrack => CameraTrack.Count > 0;

        /// <summary>
        ///     The camera yaw at run time <paramref name="t" />: <see cref="CameraYaw" /> without a track; with one, in
        ///     (-180, 180], linear between the samples around <paramref name="t" /> the short way round, and held at the
        ///     first sample before it and at the last after it. Pitch is not played: see <c>docs/locomotion.md</c>.
        /// </summary>
        public float CameraYawAt(float t)
        {
            if (CameraTrack.Count == 0)
                return CameraYaw;

            // the first sample after t
            int lo = 0, hi = CameraTrack.Count;

            while (lo < hi)
            {
                int mid = (lo + hi) / 2;

                if (CameraTrack[mid].T > t)
                    hi = mid;
                else
                    lo = mid + 1;
            }

            if (lo == 0)
                return NormalizeYaw(CameraTrack[0].Yaw);

            if (lo == CameraTrack.Count)
                return NormalizeYaw(CameraTrack[lo - 1].Yaw);

            LocomotionCameraSample a = CameraTrack[lo - 1];
            LocomotionCameraSample b = CameraTrack[lo];
            float f = (t - a.T) / (b.T - a.T);
            return NormalizeYaw(a.Yaw + (Mathf.DeltaAngle(a.Yaw, b.Yaw) * f));
        }

        /// <summary>Trace-format yaw range: (-180, 180].</summary>
        internal static float NormalizeYaw(float yaw)
        {
            float r = Mathf.Repeat(yaw, 360f);
            return r > 180f ? r - 360f : r;
        }

        /// <summary>The input held at <paramref name="t" />: the last event at or before it (within <see cref="TIME_TOLERANCE" />), idle before the first one.</summary>
        public LocomotionInputStep SampleAt(float t)
        {
            LocomotionInputStep held = LocomotionInputStep.IDLE;

            for (var i = 0; i < Events.Count; i++)
            {
                if (Events[i].T > t + TIME_TOLERANCE)
                    break;

                held = Events[i];
            }

            return held;
        }

        /// <summary>A suite is a directory of scenario files (ordinal file-name order) or one file; each file holds a scenario or an array of them.</summary>
        public static List<LocomotionScenario> LoadSuite(string path)
        {
            var scenarios = new List<LocomotionScenario>();

            if (Directory.Exists(path))
            {
                string[] files = Directory.GetFiles(path, "*.json");
                Array.Sort(files, StringComparer.Ordinal);

                foreach (string file in files)
                    scenarios.AddRange(Parse(File.ReadAllText(file), file));
            }
            else
                scenarios.AddRange(Parse(File.ReadAllText(path), path));

            return scenarios;
        }

        /// <exception cref="FormatException">The text is not a valid format 1 or 2 scenario or array of scenarios.</exception>
        public static List<LocomotionScenario> Parse(string json, string source)
        {
            JToken root;

            try { root = JToken.Parse(json); }
            catch (JsonException e) { throw new FormatException($"{source}: {e.Message}", e); }

            var scenarios = new List<LocomotionScenario>();

            if (root is JArray array)
                foreach (JToken item in array)
                    scenarios.Add(ParseScenario(item as JObject ?? throw new FormatException($"{source}: an array item is not a scenario object"), source));
            else
                scenarios.Add(ParseScenario(root as JObject ?? throw new FormatException($"{source}: the root is neither a scenario nor an array"), source));

            return scenarios;
        }

        private static LocomotionScenario ParseScenario(JObject json, string source)
        {
            int format = Required<int>(json, "format", source);

            if (format != FORMAT && format != CAMERA_TRACK_FORMAT)
                throw new FormatException($"{source}: format {format} is not supported, expected {FORMAT} or {CAMERA_TRACK_FORMAT}");

            string id = Required<string>(json, "id", source);

            if (string.IsNullOrWhiteSpace(id) || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new FormatException($"{source}: id '{id}' cannot name a trace file");

            int fps = Required<int>(json, "fps", source);
            float settle = Required<float>(json, "settle", source);
            float duration = Required<float>(json, "duration", source);

            if (fps <= 0 || settle < 0 || duration <= 0)
                throw new FormatException($"{source} ({id}): fps and duration must be positive and settle non-negative");

            JArray rawEvents = json["events"] as JArray ?? throw new FormatException($"{source} ({id}): events must be an array");
            var events = new List<LocomotionInputStep>(rawEvents.Count);

            foreach (JToken rawEvent in rawEvents)
            {
                JObject e = rawEvent as JObject ?? throw new FormatException($"{source} ({id}): an event is not an object");
                var step = new LocomotionInputStep(Required<float>(e, "t", source), Axis(e, "fwd", source), Axis(e, "right", source),
                    Key(e, "walk", source), Key(e, "sprint", source), Key(e, "jump", source));

                if (events.Count > 0 && step.T < events[events.Count - 1].T)
                    throw new FormatException($"{source} ({id}): events must be ordered by t");

                events.Add(step);
            }

            return new LocomotionScenario(id, json.Value<long?>("seed") ?? 0, fps, json.Value<float?>("camera_yaw") ?? 0f, settle, duration, events,
                CameraTrackOf(json, format, $"{source} ({id})"));
        }

        /// <summary>
        ///     The optional <c>camera</c> track, as the Bevy recorder validates it: format 2 only, times non-negative and
        ///     ordered, a finite yaw on every sample, and a pitch in [-90, 90] on every sample or on none. The pitch is
        ///     checked, not kept.
        /// </summary>
        private static IReadOnlyList<LocomotionCameraSample> CameraTrackOf(JObject json, int format, string source)
        {
            JToken? token = json["camera"];

            if (token == null || token.Type == JTokenType.Null)
                return Array.Empty<LocomotionCameraSample>();

            JArray rawTrack = token as JArray ?? throw new FormatException($"{source}: camera must be an array");

            if (rawTrack.Count > 0 && format != CAMERA_TRACK_FORMAT)
                throw new FormatException($"{source}: a camera track needs format {CAMERA_TRACK_FORMAT}");

            var track = new List<LocomotionCameraSample>(rawTrack.Count);
            var withPitch = false;

            foreach (JToken rawSample in rawTrack)
            {
                JObject s = rawSample as JObject ?? throw new FormatException($"{source}: a camera sample is not an object");
                float t = Number(s, "t", source) ?? throw new FormatException($"{source}: a camera sample has no t");
                float yaw = Number(s, "yaw", source) ?? throw new FormatException($"{source}: the camera sample at {t} has no yaw");
                float? pitch = Number(s, "pitch", source);

                if (t < 0f || (track.Count > 0 && t < track[track.Count - 1].T))
                    throw new FormatException($"{source}: camera sample times must be non-negative and in order: {t}");

                if (track.Count == 0)
                    withPitch = pitch.HasValue;

                if (pitch.HasValue != withPitch || pitch is < -90f or > 90f)
                    throw new FormatException($"{source}: camera pitch is in [-90, 90] on every sample or on none: sample at {t}");

                track.Add(new LocomotionCameraSample(t, yaw));
            }

            return track;
        }

        /// <summary>A finite JSON number, or null when absent.</summary>
        private static float? Number(JObject json, string key, string source)
        {
            JToken? token = json[key];

            if (token == null || token.Type == JTokenType.Null)
                return null;

            if (token.Type is not (JTokenType.Integer or JTokenType.Float))
                throw new FormatException($"{source}: {key} must be a number, got {token}");

            float value = token.Value<float>();

            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new FormatException($"{source}: {key} must be finite, got {token}");

            return value;
        }

        private static int Axis(JObject json, string key, string source)
        {
            int value = json.Value<int?>(key) ?? 0;

            if (value is < -1 or > 1)
                throw new FormatException($"{source}: {key} must be -1, 0 or 1, got {value}");

            return value;
        }

        /// <summary>A key's held state: a JSON boolean, false when absent.</summary>
        private static bool Key(JObject json, string key, string source)
        {
            JToken? token = json[key];

            if (token == null || token.Type == JTokenType.Null)
                return false;

            if (token.Type != JTokenType.Boolean)
                throw new FormatException($"{source}: {key} must be true or false, got {token}");

            return token.Value<bool>();
        }

        private static T Required<T>(JObject json, string key, string source)
        {
            JToken? token = json[key];

            if (token == null || token.Type == JTokenType.Null)
                throw new FormatException($"{source}: '{key}' is required");

            T? value = token.Value<T>();
            return value ?? throw new FormatException($"{source}: '{key}' is required");
        }
    }
}
