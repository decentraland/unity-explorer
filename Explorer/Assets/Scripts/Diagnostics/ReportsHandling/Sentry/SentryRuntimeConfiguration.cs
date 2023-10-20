using Sentry.Unity;
using Sentry.Unity.Integrations;
using System;
using UnityEngine;

namespace Diagnostics.ReportsHandling.Sentry
{
    [CreateAssetMenu(fileName = "SentryRuntimeConfiguration.asset", menuName = "Sentry/SentryRuntimeConfiguration", order = 999)]
    public class SentryRuntimeConfiguration : SentryRuntimeOptionsConfiguration
    {
        // This file should be never committed since it may contain secrets
        [SerializeField] private string configYamlFilePath = "./.sentryconfig.yml";

        /// Called at the player startup by SentryInitialization.
        /// You can alter configuration for the C# error handling and also
        /// native error handling in platforms **other** than iOS, macOS and Android.
        /// Learn more at https://docs.sentry.io/platforms/unity/configuration/options/#programmatic-configuration
        public override void Configure(SentryUnityOptions options)
        {
            // Note that changes to the options here will **not** affect iOS, macOS and Android events. (i.e. environment and release)
            // Take a look at `SentryBuildTimeOptionsConfiguration` instead.

            UnhookSentryReportingFromUnityLogs();

#if UNITY_EDITOR
            ApplyFromYamlFile(options);
#endif
        }

        private void UnhookSentryReportingFromUnityLogs()
        {
            var onLogMessageReceived = (Application.LogCallback)Delegate.CreateDelegate(typeof(Application.LogCallback),
                ApplicationAdapter.Instance,
                "OnLogMessageReceived");

            Application.logMessageReceivedThreaded -= onLogMessageReceived;
        }

        private void ApplyFromYamlFile(SentryUnityOptions options) =>
            SentryYamlConfigLoader.Apply(configYamlFilePath, options);
    }
}
