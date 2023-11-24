using Sentry;
using Sentry.Unity;
using UnityEngine;

namespace DCL.Diagnostics.Sentry
{
    [CreateAssetMenu(fileName = "SentryRuntimeConfiguration.asset", menuName = "Sentry/SentryRuntimeConfiguration", order = 999)]
    public class SentryRuntimeConfiguration : SentryRuntimeOptionsConfiguration
    {
        // This file should be never committed since it may contain secrets
        [SerializeField] private string configJsonFilePath = "./.sentryconfig.json";

        /// Called at the player startup by SentryInitialization.
        /// You can alter configuration for the C# error handling and also
        /// native error handling in platforms **other** than iOS, macOS and Android.
        /// Learn more at https://docs.sentry.io/platforms/unity/configuration/options/#programmatic-configuration
        public override void Configure(SentryUnityOptions options)
        {
            // Note that changes to the options here will **not** affect iOS, macOS and Android events. (i.e. environment and release)
            // Take a look at `SentryBuildTimeOptionsConfiguration` instead.

            options.SetBeforeSend(AddUnspecifiedCategory);

#if UNITY_EDITOR
            ApplyFromJsonFile(options);
#endif
        }

        private SentryEvent AddUnspecifiedCategory(SentryEvent @event)
        {
            if (!@event.Tags.ContainsKey("category"))
                @event.SetTag("category", "UNSPECIFIED");

            return @event;
        }

        private void ApplyFromJsonFile(SentryUnityOptions options) =>
            SentryJsonConfigLoader.Apply(configJsonFilePath, options);
    }
}
