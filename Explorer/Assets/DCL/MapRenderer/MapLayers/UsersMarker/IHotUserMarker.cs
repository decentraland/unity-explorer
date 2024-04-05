using DCL.MapRenderer.Culling;
using System;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.Users
{
    /// <summary>
    /// Reusable wrap over reusable instance
    /// </summary>
    internal interface IHotUserMarker : IMapPositionProvider, IMapRendererMarker, IMapCullingListener<IHotUserMarker>, IDisposable
    {
        string CurrentPlayerId { get; }

        void UpdateMarkerPosition(string playerId, Vector3 position);
    }
}
