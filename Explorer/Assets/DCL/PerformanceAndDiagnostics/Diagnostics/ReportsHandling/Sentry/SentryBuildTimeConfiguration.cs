using Sentry;
using Sentry.Unity;
using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
using Newtonsoft.Json;
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
            options.SetBeforeSend(AddUnspecifiedCategory);

#if UNITY_EDITOR
            bool isDirty = false;

            if (bool.TryParse(Environment.GetEnvironmentVariable("SENTRY_ENABLED"), out bool isEnabled))
            {
                if (options.Enabled != isEnabled)
                {
                    options.Enabled = isEnabled;
                    isDirty = true;
                }
            }

            string? version = Application.version ?? options.Release;

            if (options.Release != version)
            {
                options.Release = Application.version ?? options.Release;
                isDirty = true;
            }

            try { isDirty |= ApplyFromEnvironmentVars(options); }
            catch (Exception e) { Debug.LogException(e); }

            try { isDirty |= ApplyFromJsonFile(options); }
            catch (Exception e) { Debug.LogException(e); }

            try { isDirty |= ApplyFromProgramArgs(options); }
            catch (Exception e) { Debug.LogException(e); }

            if (isDirty)
            {
                try
                {
                    // SentryOptions.asset must be modified so the app is built with the expected information
                    PersistIntoAssetFile(GetAssetPath("SentryOptions"), options);
                }
                catch (Exception e) { Debug.LogException(e); }
            }
#endif

            ReportHub.LogProductionInfo($"SentryBuildTimeConfiguration.options.enabled: {options.Enabled}");
            ReportHub.LogProductionInfo($"SentryBuildTimeConfiguration.options.dsn: {options.Dsn}");
            ReportHub.LogProductionInfo($"SentryBuildTimeConfiguration.options.EnvironmentOverride: {options.Environment}");
        }

        private SentryEvent AddUnspecifiedCategory(SentryEvent @event)
        {
            if (!@event.Tags.ContainsKey("category"))
                @event.SetTag("category", "UNSPECIFIED");

            return @event;
        }

#if UNITY_EDITOR
        private static bool ApplyFromEnvironmentVars(SentryUnityOptions options)
        {
            string? env = Environment.GetEnvironmentVariable("SENTRY_ENVIRONMENT") ?? options.Environment;
            string? dsn = Environment.GetEnvironmentVariable("SENTRY_DSN") ?? options.Dsn;
            string? release = Environment.GetEnvironmentVariable("SENTRY_RELEASE") ?? options.Release;
            bool changed = env != options.Environment || dsn != options.Dsn || release != options.Release;
            options.Environment = env;
            options.Dsn = dsn;
            options.Release = release;
            return changed;
        }

        private static bool ApplyFromProgramArgs(SentryUnityOptions options)
        {
            var changed = false;
            string[] args = Environment.GetCommandLineArgs();

            for (var i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                switch (arg)
                {
                    case "-sentryEnvironment":
                        options.Environment = args[i + 1];
                        changed = true;
                        break;
                    case "-sentryDsn":
                        options.Dsn = args[i + 1];
                        changed = true;
                        break;
                    case "-sentryRelease":
                        options.Release = args[i + 1];
                        changed = true;
                        break;
                }
            }

            return changed;
        }

        private bool ApplyFromJsonFile(SentryUnityOptions options)
        {
            if (!File.Exists(configJsonFilePath)) return false;

            string fileContent = File.ReadAllText(configJsonFilePath);
            JsonConfigFileScheme scheme = JsonConvert.DeserializeObject<JsonConfigFileScheme>(fileContent);

            string? env = string.IsNullOrEmpty(scheme.environment) ? options.Environment : scheme.environment;
            string? dsn = string.IsNullOrEmpty(scheme.dsn) ? options.Dsn : scheme.dsn;
            string? release = string.IsNullOrEmpty(scheme.release) ? options.Release : scheme.release;

            bool changed = env != options.Environment || dsn != options.Dsn || release != options.Release;

            options.Environment = env;
            options.Dsn = dsn;
            options.Release = release;

            return changed;
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

        [Serializable]
        private struct JsonConfigFileScheme
        {
            public string environment;
            public string dsn;
            public string release;
        }
#endif
    }
}
