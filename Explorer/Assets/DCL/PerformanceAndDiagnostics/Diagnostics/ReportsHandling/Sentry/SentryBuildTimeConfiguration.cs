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
            var enabledExists = bool.TryParse(Environment.GetEnvironmentVariable("SENTRY_ENABLED"), out bool isEnabled);
            string? env = Environment.GetEnvironmentVariable("SENTRY_ENVIRONMENT") ?? options.Environment;
            string? dsn = Environment.GetEnvironmentVariable("SENTRY_DSN") ?? options.Dsn;
            string? release = Environment.GetEnvironmentVariable("SENTRY_RELEASE") ?? options.Release;
            bool isDirty = env != options.Environment || dsn != options.Dsn || release != options.Release;

            options.Environment = env;
            options.Dsn = dsn;
            options.Release = release;

            if (enabledExists)
            {
                if (options.Enabled != isEnabled)
                {
                    isDirty = true;
                    options.Enabled = isEnabled;
                }
            }

            return isDirty;
        }

        private static bool ApplyFromProgramArgs(SentryUnityOptions options)
        {
            var isDirty = false;
            string[] args = Environment.GetCommandLineArgs();

            for (var i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                switch (arg)
                {
                    case "-sentryEnvironment":
                        string env = args[i + 1];

                        if (options.Environment != env)
                        {
                            options.Environment = env;
                            isDirty = true;
                        }

                        break;
                    case "-sentryDsn":
                        string dsn = args[i + 1];

                        if (options.Dsn != dsn)
                        {
                            options.Dsn = dsn;
                            isDirty = true;
                        }

                        break;
                    case "-sentryRelease":
                        string release = args[i + 1];

                        if (options.Release != release)
                        {
                            options.Release = release;
                            isDirty = true;
                        }

                        break;
                }
            }

            if (isDirty)
                options.Enabled = true;

            return isDirty;
        }

        private bool ApplyFromJsonFile(SentryUnityOptions options)
        {
            if (!File.Exists(configJsonFilePath)) return false;

            string fileContent = File.ReadAllText(configJsonFilePath);
            JsonConfigFileScheme scheme = JsonConvert.DeserializeObject<JsonConfigFileScheme>(fileContent);

            string? env = string.IsNullOrEmpty(scheme.environment) ? options.Environment : scheme.environment;
            string? dsn = string.IsNullOrEmpty(scheme.dsn) ? options.Dsn : scheme.dsn;
            string? release = string.IsNullOrEmpty(scheme.release) ? options.Release : scheme.release;

            bool isDirty = env != options.Environment || dsn != options.Dsn || release != options.Release;

            options.Environment = env;
            options.Dsn = dsn;
            options.Release = release;

            if (isDirty)
                options.Enabled = true;

            return isDirty;
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
