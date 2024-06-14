using DCL.Diagnostics.Sentry;
using Sentry.Unity;
using System;
using UnityEditor;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Diagnostics.ReportsHandling.Sentry.Editor
{
    [CreateAssetMenu(fileName = "SentryBuildTimeConfiguration.asset", menuName = "Sentry/SentryBuildTimeConfiguration", order = 999)]
    public class SentryBuildTimeConfiguration : SentryBuildTimeOptionsConfiguration
    {
        private const string SENTRY_ASSET_PATH = "Assets/Resources/Sentry/SentryOptions.asset";
        private const string CLI_ASSET_PATH = "Assets/Plugins/Sentry/SentryCliOptions.asset";

        // This file should be never committed since it may contain secrets
        [SerializeField] private string configJsonFilePath = "./.sentryconfig.json";

        /// Called during app build. Changes made here will affect build-time processing, symbol upload, etc.
        /// Additionally, because iOS, macOS and Android native error handling is configured at build time,
        /// you can make changes to these options here.
        /// Learn more at https://docs.sentry.io/platforms/unity/configuration/options/#programmatic-configuration
        public override void Configure(SentryUnityOptions options, SentryCliOptions cliOptions)
        {
            // Force options to enabled=true to be able to deploy debug symbols during build-time
            options.Enabled = true;
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
                PersistIntoCliAssetFile(CLI_ASSET_PATH, cliOptions);
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        private static void ApplyFromEnvironmentVars(SentryUnityOptions options, SentryCliOptions cliOptions)
        {
            options.Environment = Environment.GetEnvironmentVariable("SENTRY_ENVIRONMENT") ?? options.Environment;
            options.Dsn = Environment.GetEnvironmentVariable("SENTRY_DSN") ?? options.Dsn;
            options.Release = Environment.GetEnvironmentVariable("SENTRY_RELEASE") ?? options.Release;
            cliOptions.Auth = Environment.GetEnvironmentVariable("SENTRY_CLI_AUTH_TOKEN") ?? cliOptions.Auth;

            string envUploadSymbols = Environment.GetEnvironmentVariable("SENTRY_UPLOAD_DEBUG_SYMBOLS");
            cliOptions.UploadSymbols = envUploadSymbols != null ? bool.Parse(envUploadSymbols) : cliOptions.UploadSymbols;
        }

        private static void ApplyFromProgramArgs(SentryUnityOptions options, SentryCliOptions cliOptions)
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
                    case "-sentryCliAuthToken":
                        cliOptions.Auth = args[i + 1];
                        break;
                    case "-sentryUploadDebugSymbols":
                        cliOptions.UploadSymbols = bool.Parse(args[i + 1]);
                        break;
                }
            }
        }

        private void ApplyFromJsonFile(SentryUnityOptions options, SentryCliOptions cliOptions)
        {
            SentryJsonConfigLoader.Apply(configJsonFilePath, options);
            SentryJsonConfigLoader.Apply(configJsonFilePath, cliOptions);
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

        private void PersistIntoCliAssetFile(string path, SentryCliOptions cliOptions)
        {
            SentryCliOptions asset = AssetDatabase.LoadAssetAtPath<SentryCliOptions>(path);
            if (asset == null) return;
            asset.Auth = cliOptions.Auth;
            asset.UploadSymbols = cliOptions.UploadSymbols;
            EditorUtility.SetDirty(asset);
        }
    }
}
