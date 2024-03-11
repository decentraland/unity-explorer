using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Editor
{
    public static class BuildScript
    {
        private static readonly string[] Secrets =
            {};

        [UsedImplicitly]
        public static void Build()
        {
            Dictionary<string, string> options = GetValidatedOptions();

            var currentMethod = System.Reflection.MethodBase.GetCurrentMethod();
            if (currentMethod != null)
            {
                var fullMethodName = currentMethod.DeclaringType.FullName + "." + currentMethod.Name;
                Console.WriteLine("Invoked " + fullMethodName + " (BuildScript.cs)");
            }

            if (!options.TryGetValue("standaloneBuildSubtarget", out var subtargetValue) || !Enum.TryParse(subtargetValue, out StandaloneBuildSubtarget buildSubtargetValue)) {
                buildSubtargetValue = default;
            }
            var buildSubtarget = (int) buildSubtargetValue;

            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(s => s.path).ToArray();
            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions()
            {
                scenes = scenes,
                locationPathName = options["customBuildPath"],
                subtarget = buildSubtarget,
            };
            

            buildPlayerOptions.options |=  BuildOptions.DetailedBuildReport;
            if (Environment.GetEnvironmentVariable("DEVELOPMENT_BUILD") == "true")
            {
                buildPlayerOptions.options |= BuildOptions.AllowDebugging;
                buildPlayerOptions.options |= BuildOptions.ConnectWithProfiler;
                buildPlayerOptions.options |= BuildOptions.Development;
            }

            BuildSummary buildSummary = BuildPipeline.BuildPlayer(buildPlayerOptions).summary;
            ReportSummary(buildSummary);
            ExitWithResult(buildSummary.result);
        }

        private static Dictionary<string, string> GetValidatedOptions()
        {
            ParseCommandLineArguments(out Dictionary<string, string> validatedOptions);

            if (!validatedOptions.TryGetValue("projectPath", out string _))
            {
                Console.WriteLine("Missing argument -projectPath");
                EditorApplication.Exit(110);
            }

            if (!validatedOptions.TryGetValue("buildTarget", out string buildTarget))
            {
                Console.WriteLine("Missing argument -buildTarget");
                EditorApplication.Exit(120);
            }

            if (!Enum.IsDefined(typeof(BuildTarget), buildTarget ?? string.Empty))
            {
                Console.WriteLine($"{buildTarget} is not a defined {nameof(BuildTarget)}");
                EditorApplication.Exit(121);
            }

            if (!validatedOptions.TryGetValue("customBuildPath", out string _))
            {
                Console.WriteLine("Missing argument -customBuildPath");
                EditorApplication.Exit(130);
            }

            const string defaultCustomBuildName = "TestBuild";
            if (!validatedOptions.TryGetValue("customBuildName", out string customBuildName))
            {
                Console.WriteLine($"Missing argument -customBuildName, defaulting to {defaultCustomBuildName}.");
                validatedOptions.Add("customBuildName", defaultCustomBuildName);
            }
            else if (customBuildName == "")
            {
                Console.WriteLine($"Invalid argument -customBuildName, defaulting to {defaultCustomBuildName}.");
                validatedOptions.Add("customBuildName", defaultCustomBuildName);
            }

            return validatedOptions;
        }

        private static void ParseCommandLineArguments(out Dictionary<string, string> providedArguments)
        {
            providedArguments = new Dictionary<string, string>();
            string[] args = Environment.GetCommandLineArgs();

            Console.WriteLine(
                $"{Environment.NewLine}" +
                $"###########################{Environment.NewLine}" +
                $"#    Parsing settings     #{Environment.NewLine}" +
                $"###########################{Environment.NewLine}" +
                $"{Environment.NewLine}"
            );

            // Extract flags with optional values
            for (int current = 0, next = 1; current < args.Length; current++, next++)
            {
                // Parse flag
                bool isFlag = args[current].StartsWith("-");
                if (!isFlag) continue;
                string flag = args[current].TrimStart('-');

                // Parse optional value
                bool flagHasValue = next < args.Length && !args[next].StartsWith("-");
                string value = flagHasValue ? args[next].TrimStart('-') : "";
                bool secret = Secrets.Contains(flag);
                string displayValue = secret ? "*HIDDEN*" : "\"" + value + "\"";

                // Assign
                Console.WriteLine($"Found flag \"{flag}\" with value {displayValue}.");
                providedArguments.Add(flag, value);
            }
        }

        private static void ReportSummary(BuildSummary summary)
        {
            Console.WriteLine(
                $"{Environment.NewLine}" +
                $"###########################{Environment.NewLine}" +
                $"#      Build results      #{Environment.NewLine}" +
                $"###########################{Environment.NewLine}" +
                $"{Environment.NewLine}" +
                $"Duration: {summary.totalTime.ToString()}{Environment.NewLine}" +
                $"Warnings: {summary.totalWarnings.ToString()}{Environment.NewLine}" +
                $"Errors: {summary.totalErrors.ToString()}{Environment.NewLine}" +
                $"Size: {summary.totalSize.ToString()} bytes{Environment.NewLine}" +
                $"{Environment.NewLine}"
            );
        }
        private static void ExitWithResult(BuildResult result)
        {
            switch (result)
            {
                case BuildResult.Succeeded:
                    Console.WriteLine("Build succeeded!");
                    EditorApplication.Exit(0);
                    break;
                case BuildResult.Failed:
                    Console.WriteLine("Build failed!");
                    EditorApplication.Exit(101);
                    break;
                case BuildResult.Cancelled:
                    Console.WriteLine("Build cancelled!");
                    EditorApplication.Exit(102);
                    break;
                case BuildResult.Unknown:
                default:
                    Console.WriteLine("Build result is unknown!");
                    EditorApplication.Exit(103);
                    break;
            }
        }
    }
}
