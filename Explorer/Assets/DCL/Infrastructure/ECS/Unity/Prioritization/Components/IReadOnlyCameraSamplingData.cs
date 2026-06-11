using UnityEngine;

namespace ECS.Prioritization.Components
{
    /// <summary>
    ///     Interface for consumers
    /// </summary>
    public interface IReadOnlyCameraSamplingData
    {
        Vector3 Position { get; }

        Vector3 Forward { get; }

        Vector2Int Parcel { get; }

        bool IsDirty { get; }
    }
}
