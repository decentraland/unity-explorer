#if UNITY_EDITOR

using System.IO;
using System;
using System.Diagnostics;
using System.Threading;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Assertions;
using System.Reflection;

using DCL.Diagnostics;

namespace DCL.Infrastructure
{
    public static class WebGLEditor
    {
        public const string CHROME_PID_FILE = "chrome_pid.txt";
        public const string SERVER_PID_FILE = "server_pid.txt";
        public const string BUILDS_FOLDER = "Builds/WebGL";

        [MenuItem("Decentraland/WebGL/Build and Start")]
        public static void BuildAndStart()
        {
            // Can be adopted for Windows later
            const string CHROME_PATH = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";
            const string CHROME_ARGS = "--disable-web-security --user-data-dir=/tmp/chrome-no-cors";
            
            const int START_PORT = 8000;
            const string SERVER_CMD = "python3 -m http.server"; // EXAMPLE: python3 -m http.server 8044
            const string INDEX_BUILD_PATH = "webgl_build_increment_index.txt";

            ReportHub.Log(ReportCategory.UNSPECIFIED, $"Build and Start: Begin");

            ReadAndIncrementBuildIndex(INDEX_BUILD_PATH, out int incrementedIndex);

            int targetPort = START_PORT + incrementedIndex;
            string buildFolder = $"{BUILDS_FOLDER}/{incrementedIndex}";

            Build(buildFolder);
            StartServer(buildFolder, targetPort, SERVER_PID_FILE);
            StartChrome(CHROME_PATH, CHROME_ARGS, targetPort, CHROME_PID_FILE);

            ReportHub.Log(ReportCategory.UNSPECIFIED, $"Build and Start: Finished");
        }

        [MenuItem("Decentraland/WebGL/Stop Server and Browser")]
        public static void StopServerAndBrowser()
        {
            Process_Kill(SERVER_PID_FILE);
            Process_Kill(CHROME_PID_FILE);
        }

        [MenuItem("Decentraland/WebGL/Clear Builds Directory")]
        public static void ClearBuildsDirectory()
        {
            Directory.Delete(BUILDS_FOLDER, true);
        }

        private static void Build(string buildFolder)
        {
            Directory.CreateDirectory(buildFolder);

            EditorUtility.DisplayDialog(
                    "WebGL Build",
                    "You will be promted by Unity to select the build location. Select any since it will be ignored.",
                    "OK"
                    );

            BuildPlayerOptions buildOptions = BuildPlayerWindow.DefaultBuildMethods.GetBuildPlayerOptions(new BuildPlayerOptions());
            buildOptions.locationPathName = buildFolder;

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);

            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("WebGL build failed");

            ReportHub.Log(ReportCategory.UNSPECIFIED, $"WebGL build completed at the path: {buildFolder}");
        }

        private static BuildPlayerOptions GetBuildPlayerOptions(
                bool askForLocation = false,
                BuildPlayerOptions defaultOptions = new BuildPlayerOptions())
        {
            // Get static internal "GetBuildPlayerOptionsInternal" method
            MethodInfo method = typeof(BuildPlayerWindow).GetMethod(
                    "GetBuildPlayerOptionsInternal",
                    BindingFlags.NonPublic | BindingFlags.Static);

            // TODO it won't work for some reason. Seems Unity changed their internal API.
            // https://discussions.unity.com/t/getting-the-current-buildoptions/224799/2
            Assert.IsNotNull(method, "Cannot get method GetBuildPlayerOptionsInternal");

            // invoke internal method
            object result = method.Invoke(
                    null, 
                    new object[] { askForLocation, defaultOptions});

            Assert.IsNotNull(result);

            return (BuildPlayerOptions)result;
        }

        private static void StartChrome(string chromePath, string chromeArgs, int port, string pidSavePath)
        {
            Process_Kill(pidSavePath);

            string url = $"http://localhost:{port}";

            Process ChromeProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = chromePath,
                    Arguments = $"{chromeArgs} {url}",
                    UseShellExecute = false
                }
            };

            ChromeProcess.Start();
            Process_WritePid(ChromeProcess.Id, pidSavePath);
        }

        private static void StartServer(string buildFolder, int port, string pidSavePath)
        {
            Process_Kill(pidSavePath);

            Process ServerProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/usr/bin/env",
                    Arguments = $"python3 -m http.server {port}",
                    WorkingDirectory = Path.GetFullPath(buildFolder),
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    UseShellExecute = false
                }
            };

            ServerProcess.Start();
            Process_WritePid(ServerProcess.Id, pidSavePath);
        }

        // Domain reload drops static fields
        private static void Process_WritePid(int pid, string pidSavePath)
        {
            string directory = Path.GetDirectoryName(pidSavePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(pidSavePath, pid.ToString());
        }

        private static void Process_Kill(string pidSavePath)
        {
            if (!File.Exists(pidSavePath))
                return;

            string content = File.ReadAllText(pidSavePath);

            if (!int.TryParse(content, out int pid))
            {
                File.Delete(pidSavePath);
                return;
            }

            try
            {
                Process process = Process.GetProcessById(pid);

                if (!process.HasExited)
                {
                    process.Kill();      // kill entire process tree
                    process.WaitForExit(2000);
                }
            }
            catch (ArgumentException)
            {
                // Process already exited
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Failed to kill PID {pid}: {ex.Message}");
            }

            File.Delete(pidSavePath);
        }

        private static string[] GetEnabledScenes()
        {
            return Array.ConvertAll(EditorBuildSettings.scenes,s => s.path);
        }

        private static void ReadAndIncrementBuildIndex(string path, out int incrementedIndex)
        {
            incrementedIndex = 0;

            if (File.Exists(path))
                int.TryParse(File.ReadAllText(path), out incrementedIndex);

            incrementedIndex++;
            File.WriteAllText(path, incrementedIndex.ToString());
        }
    }
}

#endif
