using Sentry;
using Sentry.Unity;
using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DCL.Diagnostics.Sentry
{
    [CreateAssetMenu(fileName = "SentryBuildTimeConfiguration.asset", menuName = "DCL/Diagnostics/Sentry Build Time Configuration")]
    public class SentryBuildTimeConfiguration : SentryOptionsConfiguration
    {
        // This file should be never committed since it may contain secrets
        [SerializeField] private string configJsonFilePath = "./.sentryconfig.json";

        /// Called during app build. Changes made here will affect build-time processing, symbol upload, etc.
        /// Additionally, because iOS, macOS and Android native error handling is configured at build time,
        /// you can make changes to these options here.
        /// Learn more at https://docs.sentry.io/platforms/unity/configuration/options/#programmatic-configuration
        public override void Configure(SentryUnityOptions options)
        {
#if UNITY_EDITOR
            Debug.Log("Sentry build time configuration loaded");

            // Force options to enabled=true to be able to deploy debug symbols during build-time
            options.Enabled = Environment.GetEnvironmentVariable("SENTRY_ENABLED") == "true";
            if (!options.Enabled) return; // No need to configure sentry

            options.Release = Application.version ?? options.Release;

            try { ApplyFromEnvironmentVars(options); }
            catch (Exception e) { Debug.LogException(e); }

            try { ApplyFromJsonFile(options); }
            catch (Exception e) { Debug.LogException(e); }

            try { ApplyFromProgramArgs(options); }
            catch (Exception e) { Debug.LogException(e); }

            try
            {
                // SentryOptions.asset must be modified so the app is built with the expected information
                PersistIntoAssetFile(GetAssetPath("SentryOptions"), options);
            }
            catch (Exception e) { Debug.LogException(e); }
#endif

            options.SetBeforeSend(AddUnspecifiedCategory);
        }

        private SentryEvent AddUnspecifiedCategory(SentryEvent @event)
        {
            if (!@event.Tags.ContainsKey("category"))
                @event.SetTag("category", "UNSPECIFIED");

            return @event;
        }

#if UNITY_EDITOR
        private static void ApplyFromEnvironmentVars(SentryUnityOptions options)
        {
            options.Environment = Environment.GetEnvironmentVariable("SENTRY_ENVIRONMENT") ?? options.Environment;
            options.Dsn = Environment.GetEnvironmentVariable("SENTRY_DSN") ?? options.Dsn;
            options.Release = Environment.GetEnvironmentVariable("SENTRY_RELEASE") ?? options.Release;
        }

        private static void ApplyFromProgramArgs(SentryUnityOptions options)
        {
            string[] args = Environment.GetCommandLineArgs();

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
                }
            }
        }

        private void ApplyFromJsonFile(SentryUnityOptions options)
        {
            SentryJsonConfigLoader.Apply(configJsonFilePath, options);
        }

        private void PersistIntoAssetFile(string path, SentryUnityOptions options)
        {
            ScriptableSentryUnityOptions asset = AssetDatabase.LoadAssetAtPath<ScriptableSentryUnityOptions>(path);
            if (asset == null) return;
            asset.ReleaseOverride = options.Release;
            asset.Dsn = options.Dsn;
            asset.EnvironmentOverride = options.Environment;
            EditorUtility.SetDirty(asset);
        }
#endif
    }
}
