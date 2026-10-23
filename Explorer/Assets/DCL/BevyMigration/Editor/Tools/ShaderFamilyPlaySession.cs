using System;

namespace DCL.BevyMigration.Editor
{
    /// <summary>
    ///     Unattended play-session recording for the shader inventory, on top of <see cref="EditorPlaySession" />:
    ///     a guest lands on Genesis Plaza with the MCP server on, the recorder runs, the report is written when the
    ///     deadline passes (0 keeps Play mode running until stopped by hand).
    ///     <code>
    ///     Unity -projectPath Explorer -executeMethod DCL.BevyMigration.Editor.ShaderFamilyPlaySession.RecordBatch
    ///           [-shaderInventorySeconds 240] [-shaderInventoryAppParams "--position 0,0 ..."]
    ///     </code>
    /// </summary>
    public static class ShaderFamilyPlaySession
    {
        private const string SECONDS_ARG = "-shaderInventorySeconds";
        private const string APP_PARAMS_ARG = "-shaderInventoryAppParams";
        private const int DEFAULT_SECONDS = 240;
        private const string DEFAULT_APP_PARAMS = "--skip-auth-screen --ephemeral-guest-account true --position 0,0 --skip-version-check --skip-minimum-specs-screen --mcp";

        public static void RecordBatch()
        {
            string[] args = Environment.GetCommandLineArgs();
            int seconds = int.TryParse(ArgumentAfter(args, SECONDS_ARG), out int parsed) ? parsed : DEFAULT_SECONDS;
            string appParams = ArgumentAfter(args, APP_PARAMS_ARG) ?? DEFAULT_APP_PARAMS;
            EditorPlaySession.Start(appParams, seconds, 1, true);
        }

        private static string? ArgumentAfter(string[] args, string name)
        {
            for (var i = 0; i + 1 < args.Length; i++)
                if (args[i] == name)
                    return args[i + 1];

            return null;
        }
    }
}
