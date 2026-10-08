using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.Atlas.SatelliteAtlas
{
    /// <summary>
    ///     Which satellite tiles a source publishes and where they are. <c>{baseUrl}/manifest.json</c> lists a version per
    ///     section, a section being one bundled level-3 chunk together with every finer tile under it. A section's tiles live
    ///     at <c>sections/{sx},{sy}/{version}/{level}/{i},{j}.ktx2</c>, so re-publishing a section under a new version moves
    ///     its tiles to new paths, which a client holding the previous version in its disk cache misses. A section the manifest
    ///     omits has no tiles. A source that publishes no manifest keeps its tiles at the unversioned <c>{level}/{i},{j}.ktx2</c>.
    /// </summary>
    internal class SatelliteManifest
    {
        /// <summary>The source has no manifest: its tiles are at the unversioned paths.</summary>
        public static readonly SatelliteManifest UNVERSIONED = new (null);

        private readonly Dictionary<Vector2Int, string>? sectionVersions;

        private SatelliteManifest(Dictionary<Vector2Int, string>? sectionVersions)
        {
            this.sectionVersions = sectionVersions;
        }

        /// <summary>Keeps the sections whose key parses as <c>"{sx},{sy}"</c> and whose version is not empty.</summary>
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
        ///     Path of the tile <paramref name="id" /> (x, y, level) under the source's base URL.
        ///     False when the manifest lists no version for the tile's section: the tile is not published.
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
            int shift = id.z - SatelliteDetailTiles.BASE_LEVEL;
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

        // Server schema: written by the map capture tool's pyramid scripts; layout in docs/app-arguments.md § satellite-map-url.
        public class Dto
        {
            // ReSharper disable InconsistentNaming
            public Dictionary<string, string?>? sections;
        }
    }
}
