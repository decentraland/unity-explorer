using UnityEngine;

namespace DCL.MapRenderer.MapLayers.HomeMarker
{
    /// <summary>
    ///     Read-only view of the place the user set as home: a Genesis City parcel or a world, never both.
    /// </summary>
    public interface IHomePlaceSource
    {
        Vector2Int? CurrentHomeCoordinates { get; }

        string? CurrentHomeWorldName { get; }

        bool IsWorldHome { get; }
    }
}
