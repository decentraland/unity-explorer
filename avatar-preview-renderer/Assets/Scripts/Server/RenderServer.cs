#if DCL_RENDER_SERVER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Preview;
using Services;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Utils;
using Debug = UnityEngine.Debug;

namespace Server
{
    /// <summary>
    /// Drives the marketplace preview from the command line and writes transparent PNG stills of
    /// wearables (the item on its own, at several yaws) and emotes (worn by the avatar, at several
    /// points in time). Jobs come from a file (--jobs) or one JSON object per stdin line (--serve);
    /// one JSON result line per job goes to file descriptor 3, or to the file given with --results.
    /// </summary>
    public class RenderServer : MonoBehaviour
    {
        private const int EXIT_JOB_FAILED = 1;
        private const int EXIT_BAD_ARGUMENTS = 2;
        private const int EXIT_WEDGED = 3;

        // Game time advances exactly one step per frame however slowly the CPU renders, so spring bones
        // settle the same on every machine. The matching frame cap keeps game time close to wall time,
        // which the web retry delays are measured in.
        private const int FRAME_RATE = 30;

        // The item on its own is posed once and never animated; one frame applies the rotation and
        // the next is the one captured.
        private const int WEARABLE_SETTLE_FRAMES = 2;

        private const string LABEL_MALE = "male";
        private const string LABEL_FEMALE = "female";
        private const string LABEL_UNISEX = "unisex";

        // Transparent, with neither the shadow nor the glow drawn into the alpha. A job's params come
        // after these, so it can turn either back on.
        private const string BASE_QUERY =
            "env=prod&background=00000000&shadow=off&glow=off&disableLoader=true&disableSwitcher=true";

        private static readonly float[] DEFAULT_WEARABLE_YAWS = { 0f, 90f, 180f, 270f };
        private static readonly float[] DEFAULT_EMOTE_YAWS = { 0f };
        private static readonly float[] DEFAULT_EMOTE_TIMES = { 0.2f, 0.5f, 0.8f };

        // Set by the server for every load: they pick the view and the body shape, and the ones that
        // append (urn, base64) would add a second item to a view that shows exactly one.
        private static readonly HashSet<string> RESERVED_PARAMS = new(StringComparer.OrdinalIgnoreCase)
        {
            "mode", "type", "profile", "bodyShape", "urn", "base64", "contract", "item", "token"
        };

        private static readonly Regex SAFE_ID = new("^[A-Za-z0-9._-]{1,200}$");
        private static readonly Regex UNSAFE_ID_CHARS = new("[^A-Za-z0-9._-]");

        private readonly ConcurrentQueue<string> _stdinLines = new();
        private volatile bool _stdinClosed;

        private RenderServerOptions _options;
        private PreviewController _preview;
        private Camera _camera;
        private RenderTexture _target;
        private Texture2D _readback;
        private StreamWriter _results;

        private LoadState _loadState;
        private string _loadError;

        private enum LoadState
        {
            Pending,
            Loaded,
            Failed
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartIfRequested()
        {
            var args = Environment.GetCommandLineArgs();

            if (!RenderServerOptions.IsRequested(args)) return;

            if (!RenderServerOptions.TryParse(args, out var options, out var error))
            {
                Console.Error.WriteLine(error);
                Console.Error.WriteLine(RenderServerOptions.USAGE);
                Application.Quit(EXIT_BAD_ARGUMENTS);
                return;
            }

            var host = new GameObject(nameof(RenderServer));
            DontDestroyOnLoad(host);
            host.AddComponent<RenderServer>()._options = options;
        }

        private void Awake()
        {
            // The scene's own first load (the default profile) is already under way. Waiting on it
            // doubles as the warm-up: shaders and the body shapes are ready before the first job.
            _loadState = LoadState.Pending;

            JSBridge.NativeCalls.LoadCompleted += OnLoadCompleted;
            JSBridge.NativeCalls.ErrorReported += OnErrorReported;
        }

        private void OnDestroy()
        {
            JSBridge.NativeCalls.LoadCompleted -= OnLoadCompleted;
            JSBridge.NativeCalls.ErrorReported -= OnErrorReported;

            _results?.Dispose();
        }

        private void OnLoadCompleted()
        {
            if (_loadState == LoadState.Pending) _loadState = LoadState.Loaded;
        }

        private void OnErrorReported(string message)
        {
            _loadState = LoadState.Failed;
            _loadError = message;
        }

        private async void Start()
        {
            int exitCode;

            try
            {
                exitCode = await RunAsync();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                exitCode = EXIT_WEDGED;
            }

            Application.Quit(exitCode);
        }

        private async Awaitable<int> RunAsync()
        {
            if (_options.JobsFile != null && !File.Exists(_options.JobsFile))
            {
                Console.Error.WriteLine($"Jobs file not found: {_options.JobsFile}");
                return EXIT_BAD_ARGUMENTS;
            }

            Directory.CreateDirectory(_options.OutputDirectory);

            // A device (/dev/fd/3, a pipe) cannot seek to its end, so only a regular file is appended to.
            var resultsMode = _options.ResultsPath.StartsWith("/dev/", StringComparison.Ordinal)
                ? FileMode.Open
                : FileMode.Append;

            try
            {
                _results = new StreamWriter(new FileStream(_options.ResultsPath, resultsMode, FileAccess.Write,
                    FileShare.ReadWrite)) { AutoFlush = true };
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"Cannot open results at {_options.ResultsPath}: {e.Message}. " +
                                        "Open file descriptor 3 (e.g. 3>&1) or pass --results <file>.");
                return EXIT_BAD_ARGUMENTS;
            }

            _preview = FindAnyObjectByType<PreviewController>(FindObjectsInactive.Include);
            _camera = _preview.RenderCamera;

            // The camera fit reads its aspect from the screen, so the screen takes the stills' shape.
            Screen.SetResolution(_options.Size, _options.Size, FullScreenMode.Windowed);

            _target = new RenderTexture(_options.Size, _options.Size, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            _target.Create();
            _readback = new Texture2D(_options.Size, _options.Size, TextureFormat.RGBA32, false, false);

            // Drawn on demand only, so nothing is rendered while wearables download or poses settle.
            _camera.enabled = false;
            _camera.targetTexture = _target;
            _preview.UseCameraCuts();

            try
            {
                await WaitForLoadAsync();
            }
            catch (RenderJobException e)
            {
                Debug.LogWarning($"[RenderServer] Warm-up load failed, continuing: {e.Message}");
            }

            // After the warm-up so it lands after Bootstrap.Start, which sets both from the web defaults.
            Time.captureFramerate = FRAME_RATE;
            Application.targetFrameRate = FRAME_RATE;

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urpAsset)
                urpAsset.renderScale = _options.RenderScale;

            var failures = 0;

            if (_options.Serve)
            {
                StartReadingStdin();

                var processed = 0;

                while (_options.MaxJobs == 0 || processed < _options.MaxJobs)
                {
                    if (_stdinLines.TryDequeue(out var line))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        if (!await RunJobAsync(line)) failures++;
                        processed++;
                    }
                    // Re-checked after reading the flag: the reader enqueues its last line before setting it.
                    else if (_stdinClosed && _stdinLines.IsEmpty)
                    {
                        break;
                    }
                    else
                    {
                        await Awaitable.NextFrameAsync();
                    }
                }

                Debug.Log($"[RenderServer] Served {processed} jobs, {failures} failed");
                return 0;
            }

            foreach (var json in ReadJobsFile(_options.JobsFile))
                if (!await RunJobAsync(json))
                    failures++;

            return failures > 0 ? EXIT_JOB_FAILED : 0;
        }

        /// <summary>
        /// Runs one job and writes its result line. False when the job failed. A load that never
        /// finishes leaves the preview in an unknown state, so that rethrows and ends the process.
        /// </summary>
        private async Awaitable<bool> RunJobAsync(string json)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new RenderJobResult();

            try
            {
                var job = JsonConvert.DeserializeObject<RenderJob>(json)
                          ?? throw new RenderJobException("Empty job");

                result.Id = job.Id;
                result.Urn = job.Urn;

                await RenderAsync(job, result);
                result.Ok = true;
            }
            catch (JsonException e)
            {
                result.Error = $"Invalid job JSON: {e.Message}";
            }
            catch (RenderJobException e)
            {
                result.Error = e.Message;
            }
            catch (TimeoutException e)
            {
                result.Error = e.Message;
                WriteResult(result, stopwatch);
                throw;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                result.Error = e.Message;
            }

            WriteResult(result, stopwatch);

            // Each load disposes the last one's models; this returns their textures and meshes too.
            await Awaitable.FromAsyncOperation(Resources.UnloadUnusedAssets());

            return result.Ok;
        }

        private async Awaitable RenderAsync(RenderJob job, RenderJobResult result)
        {
            if (string.IsNullOrWhiteSpace(job.Urn)) throw new RenderJobException("urn is required");

            var urn = URNUtils.SanitizeURN(job.Urn.Trim());
            var id = string.IsNullOrEmpty(job.Id) ? IdFromUrn(urn) : job.Id;

            if (!SAFE_ID.IsMatch(id) || id is "." or "..")
                throw new RenderJobException("id may only contain letters, digits, '.', '_' and '-'");

            result.Id = id;
            result.Urn = urn;

            var yaws = job.Yaws is { Length: > 0 } ? job.Yaws : null;
            var times = job.Times is { Length: > 0 } ? job.Times : DEFAULT_EMOTE_TIMES;

            if ((yaws ?? Array.Empty<float>()).Concat(times).Append(job.Pitch).Any(v => !float.IsFinite(v)))
                throw new RenderJobException("yaws, times and pitch must be finite numbers");

            if (times.Any(t => t is < 0f or > 1f))
                throw new RenderJobException("times are fractions of the emote's length, from 0 to 1");

            var baseQuery = BASE_QUERY + ValidatedParams(job.Params);

            // The environment picks the catalyst, so it has to be applied before resolving the urn.
            PreviewConfiguration.RecreateFrom("?" + baseQuery);

            var entities = await EntityService.GetEntities(new[] { urn });

            if (entities.Length == 0) throw new RenderJobException($"No active entity for {urn}");

            var entity = entities[0];

            if (entity.Type == EntityType.Body)
                throw new RenderJobException($"{urn} is a body shape, not a wearable or emote");

            var isEmote = entity.Type == EntityType.Emote;
            result.Type = isEmote ? "emote" : "wearable";

            var directory = Path.Combine(_options.OutputDirectory, id);
            Directory.CreateDirectory(directory);

            foreach (var (label, bodyShape) in PickBodyShapes(entity, job.BodyShapes))
            {
                var profile = bodyShape == BodyShape.Male ? _options.MaleProfile : _options.FemaleProfile;

                await LoadAsync($"{baseQuery}&mode=marketplace&profile={Uri.EscapeDataString(profile)}" +
                                $"&urn={Uri.EscapeDataString(urn)}&type={(isEmote ? "avatar" : "wearable")}");

                _preview.HoldFraming();

                if (isEmote)
                    await CaptureEmoteAsync(yaws ?? DEFAULT_EMOTE_YAWS, times, label, id, result);
                else
                    await CaptureWearableAsync(yaws ?? DEFAULT_WEARABLE_YAWS, job.Pitch, label, id, result);
            }
        }

        private async Awaitable CaptureWearableAsync(float[] yaws, float pitch, string label, string id,
            RenderJobResult result)
        {
            foreach (var yaw in yaws)
            {
                _preview.Rotate(true, yaw, pitch);
                await FramesAsync(WEARABLE_SETTLE_FRAMES);

                var file = $"{label}_yaw{Format(yaw)}.png";
                Capture(Path.Combine(_options.OutputDirectory, id, file));

                result.Files.Add(new RenderedFile
                {
                    Path = $"{id}/{file}", BodyShape = label, Yaw = yaw, Pitch = pitch
                });
            }
        }

        private async Awaitable CaptureEmoteAsync(float[] yaws, float[] times, string label, string id,
            RenderJobResult result)
        {
            var length = _preview.EmoteLength;

            if (length <= 0f) throw new RenderJobException("The emote loaded without an animation");

            foreach (var time in times)
            {
                var seconds = time * length;

                foreach (var yaw in yaws)
                {
                    _preview.Rotate(false, yaw, 0f);
                    _preview.PoseEmoteAt(seconds);
                    await FramesAsync(_options.EmoteSettleFrames);

                    var file = yaws.Length > 1
                        ? $"{label}_t{Format(time * 100f)}_yaw{Format(yaw)}.png"
                        : $"{label}_t{Format(time * 100f)}.png";
                    Capture(Path.Combine(_options.OutputDirectory, id, file));

                    result.Files.Add(new RenderedFile
                    {
                        Path = $"{id}/{file}", BodyShape = label, Yaw = yaw, Time = time, Seconds = seconds
                    });
                }
            }
        }

        private async Awaitable LoadAsync(string query)
        {
            PreviewConfiguration.RecreateFrom("?" + query);

            // Before the reload starts: a load can fail before its first await.
            _loadState = LoadState.Pending;
            _loadError = null;

            _preview.InvokeReload();

            await WaitForLoadAsync();
        }

        private async Awaitable WaitForLoadAsync()
        {
            var deadline = Time.realtimeSinceStartup + _options.TimeoutSeconds;

            while (_loadState == LoadState.Pending)
            {
                if (Time.realtimeSinceStartup > deadline)
                    throw new TimeoutException($"Load did not finish within {_options.TimeoutSeconds}s");

                await Awaitable.NextFrameAsync();
            }

            if (_loadState == LoadState.Failed)
            {
                _preview.RecoverFromFailedLoad();
                throw new RenderJobException(_loadError);
            }
        }

        // Called from the Update phase: the pose, the springs and the Cinemachine camera were all
        // applied in the previous frame, so the transforms hold exactly what gets drawn.
        private void Capture(string path)
        {
            _camera.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = _target;
            _readback.ReadPixels(new Rect(0, 0, _options.Size, _options.Size), 0, 0, false);
            RenderTexture.active = previous;

            File.WriteAllBytes(path, _readback.EncodeToPNG());
        }

        /// <summary>
        /// Male and female stills when the two representations differ, one "unisex" set when they are
        /// the same files, or only the shapes the item has. An explicit request is honoured as is.
        /// </summary>
        private static List<(string label, BodyShape bodyShape)> PickBodyShapes(EntityDefinition entity,
            string[] requested)
        {
            var shapes = new List<(string, BodyShape)>();

            if (requested is { Length: > 0 })
            {
                foreach (var name in requested.Select(r => r.Trim().ToLowerInvariant()).Distinct())
                {
                    var bodyShape = name switch
                    {
                        LABEL_MALE => BodyShape.Male,
                        LABEL_FEMALE => BodyShape.Female,
                        _ => throw new RenderJobException($"Unknown body shape {name}, use male or female")
                    };

                    if (!entity.HasRepresentation(bodyShape))
                        throw new RenderJobException($"{entity.URN} has no {name} representation");

                    shapes.Add((name, bodyShape));
                }

                return shapes;
            }

            var hasMale = entity.HasRepresentation(BodyShape.Male);
            var hasFemale = entity.HasRepresentation(BodyShape.Female);

            if (hasMale && hasFemale && SameFiles(entity[BodyShape.Male], entity[BodyShape.Female]))
            {
                shapes.Add((LABEL_UNISEX, BodyShape.Male));
                return shapes;
            }

            if (hasMale) shapes.Add((LABEL_MALE, BodyShape.Male));
            if (hasFemale) shapes.Add((LABEL_FEMALE, BodyShape.Female));

            if (shapes.Count == 0) throw new RenderJobException($"{entity.URN} has no representation");

            return shapes;
        }

        // File URLs are addressed by content hash, so equal maps mean the same bytes.
        private static bool SameFiles(EntityDefinition.Representation a, EntityDefinition.Representation b) =>
            string.Equals(a.MainFile, b.MainFile, StringComparison.OrdinalIgnoreCase)
            && a.Files.Count == b.Files.Count
            && a.Files.All(file => b.Files.TryGetValue(file.Key, out var url) && url == file.Value);

        private static string ValidatedParams(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return string.Empty;

            query = query.Trim().TrimStart('?', '&');

            foreach (var parameter in query.Split('&'))
            {
                var key = HttpUtility.UrlDecode(parameter.Split('=')[0]).Trim();

                if (RESERVED_PARAMS.Contains(key))
                    throw new RenderJobException($"params may not set {key}, the server sets it");
            }

            return "&" + query;
        }

        private static string IdFromUrn(string urn)
        {
            var id = UNSAFE_ID_CHARS.Replace(urn, "_");
            return id.Length > 200 ? id[^200..] : id;
        }

        private static string Format(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        private static async Awaitable FramesAsync(int count)
        {
            for (var i = 0; i < count; i++)
                await Awaitable.NextFrameAsync();
        }

        private static IEnumerable<string> ReadJobsFile(string path)
        {
            var text = File.ReadAllText(path).Trim();

            return text.StartsWith("[", StringComparison.Ordinal)
                ? JArray.Parse(text).Select(job => job.ToString(Formatting.None))
                : text.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line));
        }

        private void StartReadingStdin()
        {
            var reader = new Thread(() =>
            {
                try
                {
                    string line;

                    while ((line = Console.In.ReadLine()) != null)
                        _stdinLines.Enqueue(line);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
                finally
                {
                    _stdinClosed = true;
                }
            })
            {
                IsBackground = true,
                Name = "RenderServer stdin"
            };

            reader.Start();
        }

        private void WriteResult(RenderJobResult result, Stopwatch stopwatch)
        {
            result.Milliseconds = stopwatch.ElapsedMilliseconds;

            _results.WriteLine(JsonConvert.SerializeObject(result, Formatting.None));
        }
    }
}
#endif
