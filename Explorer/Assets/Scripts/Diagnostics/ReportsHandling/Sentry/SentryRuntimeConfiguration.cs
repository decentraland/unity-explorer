using Sentry.Unity;
using UnityEngine;

namespace Diagnostics.ReportsHandling.Sentry
{
    [CreateAssetMenu(fileName = "SentryRuntimeConfiguration.asset", menuName = "Sentry/SentryRuntimeConfiguration", order = 999)]
    public class SentryRuntimeConfiguration : SentryRuntimeOptionsConfiguration
    {
        [SerializeField] private string configYamlFilePath = "./.sentryconfig.yml";

        /// Called at the player startup by SentryInitialization.
        /// You can alter configuration for the C# error handling and also
        /// native error handling in platforms **other** than iOS, macOS and Android.
        /// Learn more at https://docs.sentry.io/platforms/unity/configuration/options/#programmatic-configuration
        public override void Configure(SentryUnityOptions options)
        {
            // Note that changes to the options here will **not** affect iOS, macOS and Android events. (i.e. environment and release)
            // Take a look at `SentryBuildTimeOptionsConfiguration` instead.
#if UNITY_EDITOR
            ApplyFromYamlFile(options);
#endif
        }

        private void ApplyFromYamlFile(SentryUnityOptions options) =>
            SentryYamlConfigLoader.Apply(configYamlFilePath, options);
    }
}
