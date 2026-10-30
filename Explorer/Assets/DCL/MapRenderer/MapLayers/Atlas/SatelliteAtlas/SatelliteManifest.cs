using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.Atlas.SatelliteAtlas
{
    /// <summary>
    ///     Lists a version for each section that a source publishes. A section is one bundled level-3 chunk with every finer tile under it.
    ///     A new version of a section moves its tiles to new paths, so a disk cache that holds the old version misses.
    /// </summary>
    internal class SatelliteManifest
    {
        private const string FILE = "manifest.json";
        private const float RETRY_AFTER_SECONDS = 30f;

        /// <summary>For a source without a manifest. Its tiles are at the unversioned paths.</summary>
        public static readonly SatelliteManifest UNVERSIONED = new (null);

        private readonly Dictionary<Vector2Int, string>? sectionVersions;

        private SatelliteManifest(Dictionary<Vector2Int, string>? sectionVersions)
        {
            this.sectionVersions = sectionVersions;
        }

        /// <summary>
        ///     Fails only on cancellation. A 404 gives <see cref="UNVERSIONED" />. Any other failure is retried, and only the first failure is reported.
        /// </summary>
        public static async UniTask<SatelliteManifest> LoadAsync(IWebRequestController webRequestController, string baseUrl, CancellationToken ct)
        {
            var url = $"{baseUrl}/{FILE}";
            var failureReported = false;

            while (true)
            {
                try
                {
                    Dto dto = await webRequestController
                                   .GetAsync(new CommonArguments(URLAddress.FromString(url), RetryPolicy.NONE), ct, ReportCategory.UI, suppressErrors: true)
                                   .CreateFromJson<Dto>(WRJsonParser.Newtonsoft);

                    return From(dto);
                }
                catch (OperationCanceledException) { throw; }
                catch (UnityWebRequestException e) when (e.ResponseCode == WebRequestUtils.NOT_FOUND) { return UNVERSIONED; }
                catch (Exception e)
                {
                    if (!failureReported)
                    {
                        failureReported = true;
                        ReportHub.LogException(e, ReportCategory.UI);
                    }
                }

                await UniTask.Delay(TimeSpan.FromSeconds(RETRY_AFTER_SECONDS), ignoreTimeScale: true, cancellationToken: ct);
            }
        }

        public static SatelliteManifest From(Dto dto)
        {
            var versions = new Dictionary<Vector2Int, string>();

            if (dto.sections != null)
                foreach ((string key, string? version) in dto.sections)
                    if (!string.IsNullOrEmpty(version) && TryParseSection(key, out Vector2Int section))
                        versions[section] = version;

            return new SatelliteManifest(versions);
        }

        /// <summary>
        ///     The path of the tile relative to the base URL. The <paramref name="id" /> is (x, y, level). False when the tile is not published.
        /// </summary>
        public bool TryGetTilePath(Vector3Int id, out string path)
        {
            if (sectionVersions == null)
            {
                path = $"{id.z}/{id.x}%2C{id.y}.ktx2";
                return true;
            }

            Vector2Int section = SectionOf(id);

            if (!sectionVersions.TryGetValue(section, out string? version))
            {
                path = string.Empty;
                return false;
            }

            path = $"sections/{section.x}%2C{section.y}/{Uri.EscapeDataString(version)}/{id.z}/{id.x}%2C{id.y}.ktx2";
            return true;
        }

        /// <summary>The bundled level-3 chunk that the tile <paramref name="id" /> (x, y, level) subdivides.</summary>
        public static Vector2Int SectionOf(Vector3Int id)
        {
            int shift = id.z - SatelliteTileGrid.BASE_LEVEL;
            return new Vector2Int(id.x >> shift, id.y >> shift);
        }

        private static bool TryParseSection(string key, out Vector2Int section)
        {
            section = default(Vector2Int);
            int comma = key.IndexOf(',');

            if (comma < 0)
                return false;

            if (!int.TryParse(key.AsSpan(0, comma), NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
                || !int.TryParse(key.AsSpan(comma + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
                return false;

            section = new Vector2Int(x, y);
            return true;
        }

        // Server schema: written by the map capture tool's pyramid scripts.
        public class Dto
        {
            // ReSharper disable InconsistentNaming
            public Dictionary<string, string?>? sections;
        }
    }
}
