using Sentry.Unity;
using System.IO;
using Unity.VisualScripting.YamlDotNet.RepresentationModel;

namespace Diagnostics.ReportsHandling.Sentry
{
    public static class SentryYamlConfigLoader
    {
        public static void Apply(string filePath, SentryCliOptions cliOptions)
        {
            if (!File.Exists(filePath)) return;
            string fileContent = File.ReadAllText(filePath);
            var stringReader = new StringReader(fileContent);
            var yaml = new YamlStream();
            yaml.Load(stringReader);
            YamlNode root = yaml.Documents[0].RootNode["cli"];

            cliOptions.Auth = GetYamlValue(in root, "auth") ?? cliOptions.Auth;
        }

        public static void Apply(string filePath, SentryUnityOptions options)
        {
            if (!File.Exists(filePath)) return;
            string fileContent = File.ReadAllText(filePath);
            var stringReader = new StringReader(fileContent);
            var yaml = new YamlStream();
            yaml.Load(stringReader);
            YamlNode root = yaml.Documents[0].RootNode;

            options.Environment = GetYamlValue(in root, "environment") ?? options.Environment;
            options.Dsn = GetYamlValue(in root, "dsn") ?? options.Dsn;
            options.Release = GetYamlValue(in root, "release") ?? options.Release;
        }

        private static string GetYamlValue(in YamlNode root, string key)
        {
            if (root == null) return null;
            var value = root[key]?.ToString();
            if (string.IsNullOrEmpty(value)) return null;
            return value;
        }
    }
}
