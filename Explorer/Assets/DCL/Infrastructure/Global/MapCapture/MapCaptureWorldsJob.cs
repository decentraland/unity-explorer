using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;

namespace Global.MapCapture
{
    /// <summary>
    ///     Captures worlds one after another in one process, each on the client's satellite grid: points the realm at the
    ///     world, resolves its scenes and generates its terrain as the client does on entering it, then renders every grid
    ///     tile its parcels touch, loaded in parts, into out/worlds/{name}/{level}/{i},{j}.jpg. A world that fails is
    ///     recorded and the run moves on; a world whose manifest is final is skipped, so a crashed run can be restarted.
    /// </summary>
    public class MapCaptureWorldsJob
    {
        private const string WORLDS_FOLDER = "worlds";
        private const string MANIFEST_FILE = "manifest.json";
        private const string RUN_SUMMARY_FILE = "run-summary.json";
        private const string INDEX_PATH = "/index";

        // Cloudflare in front of the worlds content server rejects default user agents with a 403.
        private const string USER_AGENT_HEADER = "User-Agent";
        private const string BROWSER_USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";

        private const string STATUS_COMPLETE = "complete";
        private const string STATUS_SKIPPED = "skipped";
        private const string STATUS_FAILED = "failed";
        private const string STATUS_IN_PROGRESS = "inProgress";
        private const int POLL_INTERVAL_MS = 250;
        private const char LIST_COMMENT = '#';

        private readonly MapCaptureRuntime runtime;
        private readonly MapCaptureArgs args;
        private readonly MapCaptureJob job;

        public MapCaptureWorldsJob(MapCaptureRuntime runtime, MapCaptureArgs args, MapCaptureJob job)
        {
            this.runtime = runtime;
            this.args = args;
            this.job = job;
        }

        public async UniTask<MapCaptureJob.Summary> RunAsync(CancellationToken ct)
        {
            string root = Path.Combine(args.OutputDir, WORLDS_FOLDER);
            Directory.CreateDirectory(root);

            List<string> names = await ListWorldsAsync(ct);
            ReportHub.LogProductionInfo($"[MapCapture] {names.Count} worlds to capture at level {args.Level} ({args.BlockSize}-parcel tiles, {args.OutputPixels} px) into {root}");

            var worlds = new JArray();
            var counts = new Dictionary<string, int>();
            var tiles = 0;
            var completeTiles = 0;
            var failedWorlds = 0;
            float runStarted = UnityEngine.Time.realtimeSinceStartup;

            for (var index = 0; index < names.Count; index++)
            {
                string name = names[index];
                string directory = Path.Combine(root, name);
                JObject? previous = ReadManifest(directory);
                string? previousStatus = previous?.Value<string>("status");

                if (previousStatus is STATUS_COMPLETE or STATUS_SKIPPED)
                {
                    ReportHub.LogProductionInfo($"[MapCapture] World {index + 1}/{names.Count} {name}: already {previousStatus}");
                    worlds.Add(WorldEntry(name, previous!, true));
                    Count(counts, $"already {previousStatus}");
                    continue;
                }

                ReportHub.LogProductionInfo($"[MapCapture] World {index + 1}/{names.Count} {name}: started");
                JObject manifest;

                try { manifest = await CaptureWorldAsync(name, directory, previous, ct); }
                catch (OperationCanceledException) { throw; }
                catch (Exception e)
                {
                    ReportHub.LogException(e, ReportCategory.ENGINE);
                    manifest = ReadManifest(directory) ?? NewManifest(name);
                    manifest["status"] = STATUS_FAILED;
                    manifest["error"] = e.ToString();
                    WriteManifest(directory, manifest);
                }

                try { await CleanUpWorldAsync(ct); }
                catch (OperationCanceledException) { throw; }
                catch (Exception e) { ReportHub.LogProductionInfo($"[MapCapture] World {name}: cleanup failed: {e}"); }

                var status = manifest.Value<string>("status")!;
                Count(counts, status);

                if (status == STATUS_FAILED)
                    failedWorlds++;

                foreach (JToken tile in manifest["tiles"] as JArray ?? new JArray())
                {
                    tiles++;

                    if (((JArray)tile["pendingParcels"]!).Count == 0 && ((JArray)tile["failedParcels"]!).Count == 0)
                        completeTiles++;
                }

                worlds.Add(WorldEntry(name, manifest, false));
                ReportHub.LogProductionInfo($"[MapCapture] World {index + 1}/{names.Count} {name}: {status}{ErrorSuffix(manifest)}");

                WriteRunSummary(root, worlds, counts, runStarted);
            }

            WriteRunSummary(root, worlds, counts, runStarted);
            ReportHub.LogProductionInfo($"[MapCapture] Worlds run finished: {string.Join(", ", Describe(counts))}; {completeTiles}/{tiles} tiles of the worlds processed in this run have every scene loaded");

            // A failed world counts as an incomplete tile so the exit code reports it.
            return new MapCaptureJob.Summary(tiles + failedWorlds, completeTiles);
        }

        private async UniTask<JObject> CaptureWorldAsync(string name, string directory, JObject? previous, CancellationToken ct)
        {
            float started = UnityEngine.Time.realtimeSinceStartup;
            JObject manifest = NewManifest(name);

            await MapCaptureRealm.ConfigureWorldAsync(runtime.RealmData, runtime.StaticContainer.WebRequestsContainer.WebRequestController, runtime.Urls,
                runtime.StaticContainer.WorldManifestProvider, runtime.Environment, name, ct);

            float realmReady = UnityEngine.Time.realtimeSinceStartup;
            bool hasWorldManifest = !runtime.RealmData.WorldManifest.IsEmpty;

            runtime.Feeder.RequestWorldDefinitions();
            await WaitForWorldDefinitionsAsync(ct);

            if (runtime.Feeder.WorldDefinitionFailures > 0)
                throw new InvalidOperationException($"{runtime.Feeder.WorldDefinitionFailures} scene definition requests failed");

            float definitionsReady = UnityEngine.Time.realtimeSinceStartup;

            await MapCaptureBootstrap.LoadTerrainAsync(runtime.Landscape, ct);
            float terrainReady = UnityEngine.Time.realtimeSinceStartup;

            HashSet<Vector2Int> parcels = WorldParcels(out int parcelsWithoutScene);
            manifest["hasWorldManifest"] = hasWorldManifest;
            manifest["scenes"] = runtime.Feeder.WorldDefinitions.Count;
            manifest["parcels"] = parcels.Count;
            manifest["parcelsWithoutScene"] = parcelsWithoutScene;

            if (parcels.Count == 0)
                return Skipped(directory, manifest, "the world has no parcels");

            var tileSet = new SortedSet<(int j, int i)>();
            var outside = 0;

            foreach (Vector2Int parcel in parcels)
            {
                Vector2Int? tile = args.TileOf(parcel);

                if (tile.HasValue)
                    tileSet.Add((tile.Value.y, tile.Value.x));
                else
                    outside++;
            }

            if (outside > 0)
                return Skipped(directory, manifest, $"{outside} of its {parcels.Count} parcels lie outside the satellite grid");

            // Tiles an interrupted run already wrote are kept.
            var doneTiles = new Dictionary<string, JObject>();

            if (previous?["tiles"] is JArray previousTiles)
                foreach (JToken token in previousTiles)
                    if (token is JObject tile && File.Exists(Path.Combine(directory, tile.Value<string>("file")!)))
                        doneTiles[tile.Value<string>("file")!] = tile;

            var tiles = new JArray();
            manifest["status"] = STATUS_IN_PROGRESS;
            manifest["tileCount"] = tileSet.Count;
            manifest["tiles"] = tiles;

            string levelFolder = args.Level.ToString(CultureInfo.InvariantCulture);
            Directory.CreateDirectory(Path.Combine(directory, levelFolder));
            var pendingTotal = 0;
            var failedTotal = 0;

            foreach ((int j, int i) in tileSet)
            {
                string file = $"{levelFolder}/{i},{j}.jpg";

                if (!doneTiles.TryGetValue(file, out JObject? entry))
                {
                    float tileStarted = UnityEngine.Time.realtimeSinceStartup;
                    var pending = new JArray();
                    var failed = new JArray();
                    byte[] image = await job.RenderBlockInPartsAsync(args.TileMin(new Vector2Int(i, j)), pending, failed, ct);
                    File.WriteAllBytes(Path.Combine(directory, file), image);

                    entry = new JObject
                    {
                        ["i"] = i,
                        ["j"] = j,
                        ["file"] = file,
                        ["pendingParcels"] = pending,
                        ["failedParcels"] = failed,
                        ["seconds"] = Round(UnityEngine.Time.realtimeSinceStartup - tileStarted),
                    };

                    ReportHub.LogProductionInfo($"[MapCapture] {name} tile {i},{j} written; pending {pending.Count}, failed {failed.Count}");
                }

                tiles.Add(entry);
                pendingTotal += ((JArray)entry["pendingParcels"]!).Count;
                failedTotal += ((JArray)entry["failedParcels"]!).Count;
                WriteManifest(directory, manifest);
            }

            float finished = UnityEngine.Time.realtimeSinceStartup;
            manifest["status"] = STATUS_COMPLETE;
            manifest["pendingParcels"] = pendingTotal;
            manifest["failedParcels"] = failedTotal;
            manifest["allScenesLoaded"] = pendingTotal == 0 && failedTotal == 0;

            manifest["timings"] = new JObject
            {
                ["realmSeconds"] = Round(realmReady - started),
                ["definitionsSeconds"] = Round(definitionsReady - realmReady),
                ["terrainSeconds"] = Round(terrainReady - definitionsReady),
                ["tilesSeconds"] = Round(finished - terrainReady),
                ["totalSeconds"] = Round(finished - started),
            };

            WriteManifest(directory, manifest);
            return manifest;
        }

        /// <summary>The world's parcels as the client's terrain sees them: its manifest's occupied parcels, else every scene's parcels.</summary>
        private HashSet<Vector2Int> WorldParcels(out int parcelsWithoutScene)
        {
            var sceneParcels = new HashSet<Vector2Int>();

            foreach (SceneEntityDefinition definition in runtime.Feeder.WorldDefinitions)
            foreach (Vector2Int parcel in definition.metadata.scene.DecodedParcels)
                sceneParcels.Add(parcel);

            parcelsWithoutScene = 0;

            if (runtime.RealmData.WorldManifest.IsEmpty)
                return sceneParcels;

            var occupied = new HashSet<Vector2Int>();

            foreach (int2 parcel in runtime.RealmData.WorldManifest.GetOccupiedParcels())
            {
                var vector = new Vector2Int(parcel.x, parcel.y);
                occupied.Add(vector);

                if (!sceneParcels.Contains(vector))
                    parcelsWithoutScene++;
            }

            return occupied;
        }

        private async UniTask WaitForWorldDefinitionsAsync(CancellationToken ct)
        {
            float deadline = UnityEngine.Time.realtimeSinceStartup + args.LoadTimeoutSec;

            while (!runtime.Feeder.WorldDefinitionsResolved)
            {
                if (UnityEngine.Time.realtimeSinceStartup > deadline)
                    throw new TimeoutException($"Scene definitions not resolved after {args.LoadTimeoutSec:0}s");

                await UniTask.Delay(POLL_INTERVAL_MS, cancellationToken: ct);
            }
        }

        /// <summary>Nothing of the world stays loaded when the next one starts, even after a failure mid-tile.</summary>
        private async UniTask CleanUpWorldAsync(CancellationToken ct)
        {
            runtime.Feeder.ClearWorldDefinitions(runtime.World);
            await job.UnloadAsync(ct);
        }

        private async UniTask<List<string>> ListWorldsAsync(CancellationToken ct)
        {
            var names = new List<string>();
            var seen = new HashSet<string>();

            if (args.CapturesAllWorlds)
            {
                // The world server is {host}/world; the index sits next to it.
                string worldServer = runtime.Urls.Url(DecentralandUrl.WorldServer);
                string indexUrl = worldServer.Substring(0, worldServer.LastIndexOf('/')) + INDEX_PATH;

                string index = await runtime.StaticContainer.WebRequestsContainer.WebRequestController
                                            .GetAsync(new CommonArguments(URLAddress.FromString(indexUrl)), ct, ReportCategory.REALM,
                                                 new WebRequestHeadersInfo().Add(USER_AGENT_HEADER, BROWSER_USER_AGENT))
                                            .StoreTextAsync();

                foreach (JToken world in JObject.Parse(index)["data"] as JArray ?? new JArray())
                    AddWorld(world.Value<string>("name"), names, seen);

                ReportHub.LogProductionInfo($"[MapCapture] World index {indexUrl}: {names.Count} worlds");
                return names;
            }

            foreach (string line in File.ReadAllLines(args.Worlds!))
                if (!line.TrimStart().StartsWith(LIST_COMMENT))
                    AddWorld(line, names, seen);

            return names;
        }

        private static void AddWorld(string? name, List<string> names, HashSet<string> seen)
        {
            string normalized = name?.Trim().ToLowerInvariant() ?? string.Empty;

            if (normalized.Length > 0 && seen.Add(normalized))
                names.Add(normalized);
        }

        private JObject NewManifest(string name) =>
            new ()
            {
                ["name"] = name,
                ["status"] = STATUS_IN_PROGRESS,
                ["level"] = args.Level,
                ["tileParcels"] = args.BlockSize,
                ["tilePixels"] = args.OutputPixels,
                ["renderPixelsPerParcel"] = args.PixelsPerParcel,
                ["chunkSize"] = args.ChunkSize,
                ["hour"] = args.Hour,
                ["cameraHeight"] = args.CameraHeight,
                ["tiles"] = new JArray(),
            };

        private static JObject Skipped(string directory, JObject manifest, string reason)
        {
            manifest["status"] = STATUS_SKIPPED;
            manifest["reason"] = reason;
            WriteManifest(directory, manifest);
            return manifest;
        }

        private static JObject? ReadManifest(string directory)
        {
            string path = Path.Combine(directory, MANIFEST_FILE);

            if (!File.Exists(path)) return null;

            try { return JObject.Parse(File.ReadAllText(path)); }
            catch (JsonException) { return null; }
        }

        /// <summary>Written to a temporary file first, so a crash mid-write never leaves a manifest that cannot be read back.</summary>
        private static void WriteManifest(string directory, JObject manifest) =>
            WriteJson(Path.Combine(directory, MANIFEST_FILE), manifest);

        private void WriteRunSummary(string root, JArray worlds, Dictionary<string, int> counts, float runStarted)
        {
            var countsJson = new JObject();

            foreach ((string status, int count) in counts)
                countsJson[status] = count;

            WriteJson(Path.Combine(root, RUN_SUMMARY_FILE), new JObject
            {
                ["level"] = args.Level,
                ["tilePixels"] = args.OutputPixels,
                ["hour"] = args.Hour,
                ["worldsSource"] = args.Worlds,
                ["seconds"] = Round(UnityEngine.Time.realtimeSinceStartup - runStarted),
                ["counts"] = countsJson,
                ["worlds"] = worlds,
            });
        }

        private static void WriteJson(string path, JObject json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string partial = path + ".part";
            File.WriteAllText(partial, json.ToString(Formatting.Indented));

            if (File.Exists(path))
                File.Replace(partial, path, null);
            else
                File.Move(partial, path);
        }

        private static JObject WorldEntry(string name, JObject manifest, bool fromEarlierRun) =>
            new ()
            {
                ["name"] = name,
                ["status"] = manifest.Value<string>("status"),
                ["fromEarlierRun"] = fromEarlierRun,
                ["tiles"] = (manifest["tiles"] as JArray)?.Count ?? 0,
                ["pendingParcels"] = manifest["pendingParcels"],
                ["failedParcels"] = manifest["failedParcels"],
                ["reason"] = manifest["reason"],
                ["error"] = manifest["error"],
            };

        private static string ErrorSuffix(JObject manifest) =>
            manifest.Value<string>("reason") is { } reason ? $" ({reason})"
            : manifest.Value<string>("error") is { } error ? $": {error}"
            : $" ({(manifest["tiles"] as JArray)?.Count ?? 0} tiles, pending {manifest["pendingParcels"]}, failed {manifest["failedParcels"]})";

        private static void Count(Dictionary<string, int> counts, string status) =>
            counts[status] = counts.TryGetValue(status, out int count) ? count + 1 : 1;

        private static IEnumerable<string> Describe(Dictionary<string, int> counts)
        {
            foreach ((string status, int count) in counts)
                yield return $"{count} {status}";
        }

        private static double Round(float seconds) =>
            Math.Round(seconds, 1);
    }
}
