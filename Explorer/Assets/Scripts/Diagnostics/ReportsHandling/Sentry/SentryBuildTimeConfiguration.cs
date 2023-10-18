using Sentry.Unity;
using System;
using UnityEngine;

namespace Diagnostics.ReportsHandling.Sentry
{
    [CreateAssetMenu(fileName = "Assets/Resources/Sentry/SentryBuildTimeConfiguration.asset", menuName = "Sentry/SentryBuildTimeConfiguration", order = 999)]
    public class SentryBuildTimeConfiguration : SentryBuildTimeOptionsConfiguration
    {
        /// Called during app build. Changes made here will affect build-time processing, symbol upload, etc.
        /// Additionally, because iOS, macOS and Android native error handling is configured at build time,
        /// you can make changes to these options here.
        /// Learn more at https://docs.sentry.io/platforms/unity/configuration/options/#programmatic-configuration
        public override void Configure(SentryUnityOptions options, SentryCliOptions cliOptions)
        {
            options.Environment = Environment.GetEnvironmentVariable("SENTRY_ENVIRONMENT");
            options.Dsn = Environment.GetEnvironmentVariable("SENTRY_DSN");
            options.Release = Environment.GetEnvironmentVariable("SENTRY_RELEASE_NAME");
            cliOptions.Auth = Environment.GetEnvironmentVariable("SENTRY_AUTH_TOKEN");
        }
    }
}
