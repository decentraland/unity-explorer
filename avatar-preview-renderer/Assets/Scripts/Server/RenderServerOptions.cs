#if DCL_RENDER_SERVER
using System;
using System.Globalization;

namespace Server
{
    /// <summary>
    /// Command line of the render server. Unity's own player arguments (-logFile, -screen-width, ...)
    /// sit alongside these and are ignored here.
    /// </summary>
    public class RenderServerOptions
    {
        public const string USAGE =
            "Usage: renderer (--serve | --jobs <file>) --out <dir> [--results /dev/fd/3] [--size 1024]\n" +
            "       [--render-scale 1] [--timeout 90] [--max-jobs 0] [--settle-frames 15]\n" +
            "       [--male-profile default2] [--female-profile default1]";

        /// <summary>Read jobs from stdin, one JSON object per line, until stdin closes.</summary>
        public bool Serve { get; private set; }

        /// <summary>A JSON array of jobs, or one job per line.</summary>
        public string JobsFile { get; private set; }

        public string OutputDirectory { get; private set; }

        /// <summary>
        /// Where the JSON result lines go. The player redirects its own stdout into the log once it
        /// starts, so the default is file descriptor 3, which the caller opens (e.g. <c>3&gt;&amp;1</c>).
        /// </summary>
        public string ResultsPath { get; private set; } = "/dev/fd/3";

        /// <summary>Width and height of every PNG, in pixels.</summary>
        public int Size { get; private set; } = 1024;

        /// <summary>URP render scale: 1 draws at the still's size, 2 supersamples it from a render twice as large.</summary>
        public float RenderScale { get; private set; } = 1f;

        /// <summary>Seconds a single load may take before the process gives up and exits.</summary>
        public float TimeoutSeconds { get; private set; } = 90f;

        /// <summary>Exit cleanly after this many jobs so a supervisor can recycle the process. 0 never does.</summary>
        public int MaxJobs { get; private set; }

        /// <summary>Frames an emote pose is held before capture, so spring bones come to rest.</summary>
        public int EmoteSettleFrames { get; private set; } = 15;

        // default2 wears BaseMale and default1 BaseFemale. The marketplace view takes its body shape from
        // the profile, so choosing the profile is what chooses the representation.
        public string MaleProfile { get; private set; } = "default2";
        public string FemaleProfile { get; private set; } = "default1";

        public static bool IsRequested(string[] args) =>
            Array.IndexOf(args, "--serve") >= 0 || Array.IndexOf(args, "--jobs") >= 0;

        public static bool TryParse(string[] args, out RenderServerOptions options, out string error)
        {
            options = new RenderServerOptions();
            error = null;

            try
            {
                for (var i = 1; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "--serve":
                            options.Serve = true;
                            break;
                        case "--jobs":
                            options.JobsFile = ValueAt(args, ++i);
                            break;
                        case "--out":
                            options.OutputDirectory = ValueAt(args, ++i);
                            break;
                        case "--results":
                            options.ResultsPath = ValueAt(args, ++i);
                            break;
                        case "--size":
                            options.Size = int.Parse(ValueAt(args, ++i), CultureInfo.InvariantCulture);
                            break;
                        case "--render-scale":
                            options.RenderScale = float.Parse(ValueAt(args, ++i), CultureInfo.InvariantCulture);
                            break;
                        case "--timeout":
                            options.TimeoutSeconds = float.Parse(ValueAt(args, ++i), CultureInfo.InvariantCulture);
                            break;
                        case "--max-jobs":
                            options.MaxJobs = int.Parse(ValueAt(args, ++i), CultureInfo.InvariantCulture);
                            break;
                        case "--settle-frames":
                            options.EmoteSettleFrames = int.Parse(ValueAt(args, ++i), CultureInfo.InvariantCulture);
                            break;
                        case "--male-profile":
                            options.MaleProfile = ValueAt(args, ++i);
                            break;
                        case "--female-profile":
                            options.FemaleProfile = ValueAt(args, ++i);
                            break;
                    }
                }
            }
            catch (FormatException e)
            {
                error = e.Message;
                return false;
            }

            if (options.Serve == (options.JobsFile != null))
                error = "Pass exactly one of --serve or --jobs";
            else if (string.IsNullOrEmpty(options.OutputDirectory))
                error = "--out is required";
            else if (options.Size is < 16 or > 4096)
                error = "--size must be between 16 and 4096";
            else if (options.RenderScale is < 0.1f or > 2f)
                error = "--render-scale must be between 0.1 and 2";
            else if (options.TimeoutSeconds <= 0f)
                error = "--timeout must be positive";
            else if (options.MaxJobs < 0 || options.EmoteSettleFrames < 1)
                error = "--max-jobs must be 0 or more and --settle-frames 1 or more";

            return error == null;
        }

        private static string ValueAt(string[] args, int index) =>
            index < args.Length && !args[index].StartsWith("--", StringComparison.Ordinal)
                ? args[index]
                : throw new FormatException($"{args[index - 1]} needs a value");
    }
}
#endif
