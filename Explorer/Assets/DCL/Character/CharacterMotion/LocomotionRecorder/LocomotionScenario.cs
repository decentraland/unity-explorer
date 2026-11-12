using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;

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

        public LocomotionInputStep(float t, int fwd, int right, bool walk, bool sprint)
        {
            T = t;
            Fwd = fwd;
            Right = right;
            Walk = walk;
            Sprint = sprint;
        }

        public bool IsMoving => Fwd != 0 || Right != 0;
    }

    /// <summary>
    ///     A locomotion scenario (trace format v1, shared with the Bevy recorder): a step function of digital keys,
    ///     played under a held camera yaw at a locked frame rate after an idle settle.
    /// </summary>
    public sealed class LocomotionScenario
    {
        public const int FORMAT = 1;

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

        private LocomotionScenario(string id, long seed, int fps, float cameraYaw, float settle, float duration, IReadOnlyList<LocomotionInputStep> events)
        {
            Id = id;
            Seed = seed;
            Fps = fps;
            CameraYaw = cameraYaw;
            Settle = settle;
            Duration = duration;
            Events = events;
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

        /// <exception cref="FormatException">The text is not a valid v1 scenario or array of scenarios.</exception>
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

            if (format != FORMAT)
                throw new FormatException($"{source}: format {format} is not supported, expected {FORMAT}");

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
                    e.Value<bool?>("walk") ?? false, e.Value<bool?>("sprint") ?? false);

                if (events.Count > 0 && step.T < events[events.Count - 1].T)
                    throw new FormatException($"{source} ({id}): events must be ordered by t");

                events.Add(step);
            }

            return new LocomotionScenario(id, json.Value<long?>("seed") ?? 0, fps, json.Value<float?>("camera_yaw") ?? 0f, settle, duration, events);
        }

        private static int Axis(JObject json, string key, string source)
        {
            int value = json.Value<int?>(key) ?? 0;

            if (value is < -1 or > 1)
                throw new FormatException($"{source}: {key} must be -1, 0 or 1, got {value}");

            return value;
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
