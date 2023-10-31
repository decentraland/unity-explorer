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

            ApplyFromEnvironmentVars(options, cliOptions);
            ApplyFromJsonFile(options, cliOptions);

            // SentryOptions.asset must be modified so the app is built with the expected information
            PersistIntoAssetFile(SENTRY_ASSET_PATH, options);
        }

        private static void ApplyFromEnvironmentVars(SentryUnityOptions options, SentryCliOptions cliOptions)
        {
            options.Environment = Environment.GetEnvironmentVariable("SENTRY_ENVIRONMENT") ?? options.Environment;
            options.Dsn = Environment.GetEnvironmentVariable("SENTRY_DSN") ?? options.Dsn;
            options.Release = Environment.GetEnvironmentVariable("SENTRY_RELEASE") ?? options.Release;
            cliOptions.Auth = Environment.GetEnvironmentVariable("SENTRY_CLI_AUTH_TOKEN") ?? cliOptions.Auth;
        }

        private void ApplyFromJsonFile(SentryUnityOptions options, SentryCliOptions cliOptions)
        {
            SentryJsonConfigLoader.Apply(configJsonFilePath, options);
            SentryJsonConfigLoader.Apply(configJsonFilePath, cliOptions);
        }

        private void PersistIntoAssetFile(string path, SentryUnityOptions options)
        {
            if (!File.Exists(path)) return;

            string fileContent = File.ReadAllText(path);

            fileContent = fileContent.Replace("<REPLACE_RELEASE>", options.Release)
                                     .Replace("<REPLACE_ENVIRONMENT>", options.Environment)
                                     .Replace("<REPLACE_DSN>", options.Dsn);

            File.WriteAllText(path, fileContent);
        }
    }
}
