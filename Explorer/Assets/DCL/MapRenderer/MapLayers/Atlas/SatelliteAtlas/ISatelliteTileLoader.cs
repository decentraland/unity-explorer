using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.Atlas.SatelliteAtlas
{
    internal interface ISatelliteTileLoader : IDisposable
    {
        /// <summary>
        ///     The texture of the tile at <paramref name="url" />, owned by the caller. Null when <paramref name="cacheOnly" /> and the disk cache
        ///     doesn't hold the tile's file.
        /// </summary>
        UniTask<Texture2D?> LoadAsync(string url, bool cacheOnly, CancellationToken ct);
    }
}
