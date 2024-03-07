using JetBrains.Annotations;
using System;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Editor
{
    public static class BuildScript
    {
        [UsedImplicitly]
        public static void Build()
        {
            var currentMethod = System.Reflection.MethodBase.GetCurrentMethod();
            if (currentMethod != null)
            {
                var fullMethodName = currentMethod.DeclaringType.FullName + "." + currentMethod.Name;
                Console.WriteLine("Invoked " + fullMethodName + " (BuildScript.cs)");
            }

            var buildPlayerOptions = new BuildPlayerOptions
                {
                    options = BuildOptions.DetailedBuildReport,
                };

            BuildSummary buildSummary = BuildPipeline.BuildPlayer(buildPlayerOptions).summary;
            ReportSummary(buildSummary);
            ExitWithResult(buildSummary.result);
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
