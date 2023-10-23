using Sentry.Unity;
using System;
using UnityEngine;

namespace Diagnostics.ReportsHandling.Sentry
{
    [CreateAssetMenu(fileName = "SentryBuildTimeConfiguration.asset", menuName = "Sentry/SentryBuildTimeConfiguration", order = 999)]
    public class SentryBuildTimeConfiguration : SentryBuildTimeOptionsConfiguration
    {
        // This file should be never committed since it may contain secrets
        [SerializeField] private string configYamlFilePath = "./.sentryconfig.yml";

        /// Called during app build. Changes made here will affect build-time processing, symbol upload, etc.
        /// Additionally, because iOS, macOS and Android native error handling is configured at build time,
        /// you can make changes to these options here.
        /// Learn more at https://docs.sentry.io/platforms/unity/configuration/options/#programmatic-configuration
        public override void Configure(SentryUnityOptions options, SentryCliOptions cliOptions)
        {
            options.Enabled = true;

            ApplyFromYamlFile(options, cliOptions);
            ApplyFromEnvironmentVars(options, cliOptions);
        }

        private static void ApplyFromEnvironmentVars(SentryUnityOptions options, SentryCliOptions cliOptions)
        {
            options.Environment = Environment.GetEnvironmentVariable("SENTRY_ENVIRONMENT") ?? options.Environment;
            options.Dsn = Environment.GetEnvironmentVariable("SENTRY_DSN") ?? options.Dsn;
            options.Release = Environment.GetEnvironmentVariable("SENTRY_RELEASE_NAME") ?? options.Release;
            cliOptions.Auth = Environment.GetEnvironmentVariable("SENTRY_AUTH_TOKEN") ?? cliOptions.Auth;
        }

        private void ApplyFromYamlFile(SentryUnityOptions options, SentryCliOptions cliOptions)
        {
            SentryYamlConfigLoader.Apply(configYamlFilePath, options);
            SentryYamlConfigLoader.Apply(configYamlFilePath, cliOptions);
        }
    }
}
