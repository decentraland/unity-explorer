#if DCL_RENDER_SERVER
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Server
{
    /// <summary>
    /// One wearable or emote to capture. Only <see cref="Urn"/> is required.
    /// </summary>
    public class RenderJob
    {
        /// <summary>Names the output folder and echoes back in the result. Defaults to the urn.</summary>
        [JsonProperty("id")] public string Id;

        [JsonProperty("urn")] public string Urn;

        /// <summary>Degrees about the vertical axis, one still each. Emotes are shot at every yaw and time.</summary>
        [JsonProperty("yaws")] public float[] Yaws;

        /// <summary>Tilt of the item on its own, in degrees. Clamped like a drag; ignored for emotes.</summary>
        [JsonProperty("pitch")] public float Pitch;

        /// <summary>Emote only: points to capture, as fractions (0 to 1) of the emote's length.</summary>
        [JsonProperty("times")] public float[] Times;

        /// <summary>"male" and/or "female". Left out, each shape whose representation differs is shot.</summary>
        [JsonProperty("bodyShapes")] public string[] BodyShapes;

        /// <summary>Extra preview parameters, as a query string (e.g. "env=dev&amp;glow=on").</summary>
        [JsonProperty("params")] public string Params;
    }

    public class RenderJobResult
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("urn")] public string Urn;
        [JsonProperty("ok")] public bool Ok;
        [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)] public string Type;
        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)] public string Error;
        [JsonProperty("files")] public List<RenderedFile> Files = new();
        [JsonProperty("ms")] public long Milliseconds;

        // Where the time went. Loading (download and parse) and the stills (posing, settling, drawing,
        // encoding) are split so each can be judged on its own. CPU is the whole process, including the
        // rasteriser's threads, so it can exceed the wall time.
        [JsonProperty("loadMs")] public double LoadMs;
        [JsonProperty("loadCpuMs")] public double LoadCpuMs;
        [JsonProperty("stillsMs")] public double StillsMs;
        [JsonProperty("stillsCpuMs")] public double StillsCpuMs;
    }

    public class RenderedFile
    {
        /// <summary>Relative to the output directory.</summary>
        [JsonProperty("path")] public string Path;

        /// <summary>"male", "female", or "unisex" when both representations are the same.</summary>
        [JsonProperty("bodyShape")] public string BodyShape;

        [JsonProperty("yaw")] public float Yaw;
        [JsonProperty("pitch")] public float Pitch;

        [JsonProperty("time", NullValueHandling = NullValueHandling.Ignore)] public float? Time;
        [JsonProperty("seconds", NullValueHandling = NullValueHandling.Ignore)] public float? Seconds;

        /// <summary>Drawing the frame and reading its pixels back.</summary>
        [JsonProperty("renderMs")] public double RenderMs;

        /// <summary>Turning the pixels into a PNG and writing it.</summary>
        [JsonProperty("encodeMs")] public double EncodeMs;
    }

    /// <summary>A job that cannot be rendered: bad input, an unknown urn, or a failed load.</summary>
    public class RenderJobException : System.Exception
    {
        public RenderJobException(string message) : base(message)
        {
        }
    }
}
#endif
