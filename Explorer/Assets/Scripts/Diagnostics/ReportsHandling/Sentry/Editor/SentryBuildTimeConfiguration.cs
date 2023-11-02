using Sentry.Unity;
using System;
using System.IO;
using UnityEngine;

namespace Diagnostics.ReportsHandling.Sentry.Editor
{
    [CreateAssetMenu(fileName = "SentryBuildTimeConfiguration.asset", menuName = "Sentry/SentryBuildTimeConfiguration", order = 999)]
    public class SentryBuildTimeConfiguration : SentryBuildTimeOptionsConfiguration
    {
        private const string SENTRY_ASSET_PATH = "./Assets/Resources/Sentry/SentryOptions.asset";

        // This file should be never committed since it may contain secrets
        [SerializeField] private string configJsonFilePath = "./.sentryconfig.json";

        /// Called during app build. Changes made here will affect build-time processing, symbol upload, etc.
        /// Additionally, because iOS, macOS and Android native error handling is configured at build time,
        /// you can make changes to these options here.
        /// Learn more at https://docs.sentry.io/platforms/unity/configuration/options/#programmatic-configuration
        public override void Configure(SentryUnityOptions options, SentryCliOptions cliOptions)
        {
            options.Release = Application.version ?? options.Release;

            try { ApplyFromEnvironmentVars(options, cliOptions); }
            catch (Exception e) { Debug.LogException(e); }

            try { ApplyFromJsonFile(options, cliOptions); }
            catch (Exception e) { Debug.LogException(e); }

            try { ApplyFromProgramArgs(options, cliOptions); }
            catch (Exception e) { Debug.LogException(e); }

            try
            {
                // SentryOptions.asset must be modified so the app is built with the expected information
                PersistIntoAssetFile(SENTRY_ASSET_PATH, options);
            }
            catch (Exception e) { Debug.LogException(e); }

            Debug.Log($"SentryBuildTimeConfiguration.options.Release: {options.Release}");
            Debug.Log($"SentryBuildTimeConfiguration.options.Dsn: {options.Dsn}");
            Debug.Log($"SentryBuildTimeConfiguration.options.Environment: {options.Environment}");
        }

        private static void ApplyFromEnvironmentVars(SentryUnityOptions options, SentryCliOptions cliOptions)
        {
            options.Environment = Environment.GetEnvironmentVariable("SENTRY_ENVIRONMENT") ?? options.Environment;
            options.Dsn = Environment.GetEnvironmentVariable("SENTRY_DSN") ?? options.Dsn;
            options.Release = Environment.GetEnvironmentVariable("SENTRY_RELEASE") ?? options.Release;
            cliOptions.Auth = Environment.GetEnvironmentVariable("SENTRY_CLI_AUTH_TOKEN") ?? cliOptions.Auth;

            Debug.Log($"SentryBuildTimeConfiguration.ApplyFromEnvironmentVars.SENTRY_RELEASE: {Environment.GetEnvironmentVariable("SENTRY_RELEASE")}");
            Debug.Log($"SentryBuildTimeConfiguration.ApplyFromEnvironmentVars.SENTRY_DSN: {Environment.GetEnvironmentVariable("SENTRY_DSN")}");
            Debug.Log($"SentryBuildTimeConfiguration.ApplyFromEnvironmentVars.SENTRY_ENVIRONMENT: {Environment.GetEnvironmentVariable("SENTRY_ENVIRONMENT")}");
        }

        private static void ApplyFromProgramArgs(SentryUnityOptions options, SentryCliOptions cliOptions)
        {
            string[] args = Environment.GetCommandLineArgs();

            Debug.Log($"SentryBuildTimeConfiguration.ApplyFromProgramArgs.args: {string.Join(',', args)}");

            for (var i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                switch (arg)
                {
                    case "-sentryEnvironment":
                        options.Environment = args[i + 1];
                        break;
                    case "-sentryDsn":
                        options.Dsn = args[i + 1];
                        break;
                    case "-sentryRelease":
                        options.Release = args[i + 1];
                        break;
                    case "-sentryCliAuthToken":
                        cliOptions.Auth = args[i + 1];
                        break;
                }
            }
        }

        private void ApplyFromJsonFile(SentryUnityOptions options, SentryCliOptions cliOptions)
        {
            Debug.Log($"SentryBuildTimeConfiguration.ApplyFromJsonFile.Exists: {File.Exists(configJsonFilePath)}");
            SentryJsonConfigLoader.Apply(configJsonFilePath, options);
            SentryJsonConfigLoader.Apply(configJsonFilePath, cliOptions);
        }

        private void PersistIntoAssetFile(string path, SentryUnityOptions options)
        {
            Debug.Log($"SentryBuildTimeConfiguration.PersistIntoAssetFile.Exists: {File.Exists(path)}");
            if (!File.Exists(path)) return;

            string fileContent = File.ReadAllText(path);

            fileContent = fileContent.Replace("<REPLACE_RELEASE>", options.Release)
                                     .Replace("<REPLACE_ENVIRONMENT>", options.Environment)
                                     .Replace("<REPLACE_DSN>", options.Dsn);

            File.WriteAllText(path, fileContent);
        }
    }
}
