using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PluginSystem;
using Global.AppArgs;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Global.MapCapture
{
    /// <summary>
    ///     Entry point of the MapCapture scene. Boots the slim capture runtime, renders the requested region and
    ///     exits: 0 when every block rendered with all of its scenes loaded, 1 when some did not, 2 on bad arguments.
    /// </summary>
    public class MapCaptureSceneLoader : MonoBehaviour
    {
        private const int EXIT_OK = 0;
        private const int EXIT_INCOMPLETE = 1;
        private const int EXIT_BAD_ARGUMENTS = 2;

        [SerializeField] private PluginSettingsContainer pluginSettingsContainer = null!;
        [SerializeField] private Light directionalLight = null!;
        [SerializeField] private DecentralandEnvironment environment = DecentralandEnvironment.Org;

        [Header("Editor run (used when the command line carries no map-capture-region)")]
        [SerializeField] private bool editorClientMap;
        [SerializeField] private Vector2Int editorRegionMin = new (-2, 72);
        [SerializeField] private Vector2Int editorRegionMax = new (5, 79);
        [SerializeField] private string editorOutputDir = string.Empty;
        [SerializeField] private int editorBlockSize = 8;
        [SerializeField] private int editorPixelsPerParcel = 128;
        [SerializeField] private float editorHour = 10f;
        [SerializeField] private float editorCameraHeight = 200f;
        [SerializeField] private float editorLoadTimeoutSec = 180f;

        private MapCaptureRuntime? runtime;

        private async void Start()
        {
            int exitCode;

            try { exitCode = await RunAsync(destroyCancellationToken); }
            catch (OperationCanceledException) { return; }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.ENGINE);
                exitCode = EXIT_INCOMPLETE;
            }

            Quit(exitCode);
        }

        private void OnDestroy()
        {
            runtime?.Dispose();
        }

        private async UniTask<int> RunAsync(CancellationToken ct)
        {
            ApplicationParametersParser appArgs = CreateAppArgs();

            if (!MapCaptureArgs.TryParse(appArgs, out MapCaptureArgs args, out string error))
            {
                ReportHub.LogError(ReportCategory.ENGINE, $"[MapCapture] {error}");
                return EXIT_BAD_ARGUMENTS;
            }

            runtime = await MapCaptureBootstrap.CreateAsync(appArgs, pluginSettingsContainer, directionalLight, environment, this, ct);

            MapCaptureJob.Summary summary = await new MapCaptureJob(runtime, args).RunAsync(ct);
            ReportHub.Log(ReportCategory.ENGINE, $"[MapCapture] {summary}");

            return summary.AllComplete ? EXIT_OK : EXIT_INCOMPLETE;
        }

        private ApplicationParametersParser CreateAppArgs()
        {
            var fromCommandLine = new ApplicationParametersParser();

            if (!Application.isEditor || fromCommandLine.HasFlag(AppArgsFlags.MapCapture.REGION))
                return fromCommandLine;

            var arguments = new List<string>
            {
                $"--{AppArgsFlags.MapCapture.REGION}", $"{editorRegionMin.x},{editorRegionMin.y},{editorRegionMax.x},{editorRegionMax.y}",
                $"--{AppArgsFlags.MapCapture.OUTPUT_DIR}", editorOutputDir,
                $"--{AppArgsFlags.MapCapture.BLOCK_SIZE}", editorBlockSize.ToString(),
                $"--{AppArgsFlags.MapCapture.PIXELS_PER_PARCEL}", editorPixelsPerParcel.ToString(),
                $"--{AppArgsFlags.MapCapture.HOUR}", editorHour.ToString(System.Globalization.CultureInfo.InvariantCulture),
                $"--{AppArgsFlags.MapCapture.CAMERA_HEIGHT}", editorCameraHeight.ToString(System.Globalization.CultureInfo.InvariantCulture),
                $"--{AppArgsFlags.MapCapture.LOAD_TIMEOUT_SEC}", editorLoadTimeoutSec.ToString(System.Globalization.CultureInfo.InvariantCulture),
            };

            if (editorClientMap)
                arguments.Add($"--{AppArgsFlags.MapCapture.CLIENT_MAP}");

            return new ApplicationParametersParser(arguments.ToArray());
        }

        private static void Quit(int exitCode)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(exitCode);
#endif
        }
    }
}
