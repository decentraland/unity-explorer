using Global.AppArgs;

namespace Global.Versioning
{
    public readonly struct DCLVersion
    {
        public readonly string Version;

        private DCLVersion(string version)
        {
            Version = version;
        }

        public static DCLVersion FromAppArgs(IAppArgs args)
        {
            args.TryGetValue(AppArgsFlags.SIMULATE_VERSION, out string? version);

            string? currentVersion =
                version
                ?? inEditorVersion
                ?? UnityEngine.Application.version;

            return new DCLVersion(currentVersion);
        }

        public static DCLVersion Mock() =>
            new ("Mock");

        private static string? inEditorVersion
        {
            get
            {
#if UNITY_EDITOR
                string branch = FromGitCommand("rev-parse --abbrev-ref HEAD");
                string commit = FromGitCommand("rev-parse --short HEAD");

                return $"{branch} {commit}";

                static string FromGitCommand(string arguments)
                {
                    System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using System.Diagnostics.Process process = new System.Diagnostics.Process { StartInfo = startInfo };
                    process.Start();

                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    return output.Trim();
                }
#endif
                return null;
            }
        }
    }
}
