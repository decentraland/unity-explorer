using Newtonsoft.Json;
using Sentry.Unity;
using System;
using System.IO;

namespace DCL.Diagnostics.Sentry
{
    public static class SentryJsonConfigLoader
    {
        [Serializable]
        private struct CliFileScheme
        {
            public string auth;
        }

        [Serializable]
        private struct FileScheme
        {
            public string environment;
            public string dsn;
            public string release;
            public CliFileScheme cli;
        }

        public static void Apply(string filePath, SentryCliOptions cliOptions)
        {
            if (!File.Exists(filePath)) return;

            string fileContent = File.ReadAllText(filePath);
            FileScheme scheme = JsonConvert.DeserializeObject<FileScheme>(fileContent);

            cliOptions.Auth = string.IsNullOrEmpty(scheme.cli.auth) ? cliOptions.Auth : scheme.cli.auth;
        }

        public static void Apply(string filePath, SentryUnityOptions options)
        {
            if (!File.Exists(filePath)) return;

            string fileContent = File.ReadAllText(filePath);
            FileScheme scheme = JsonConvert.DeserializeObject<FileScheme>(fileContent);

            options.Environment = string.IsNullOrEmpty(scheme.environment) ? options.Environment : scheme.environment;
            options.Dsn = string.IsNullOrEmpty(scheme.dsn) ? options.Dsn : scheme.dsn;
            options.Release = string.IsNullOrEmpty(scheme.release) ? options.Release : scheme.release;
        }
    }
}
