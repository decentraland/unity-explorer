using UnityEngine;

namespace DCL.MapRenderer.Culling
{
    public interface IMapPositionProvider
    {
        Vector3 CurrentPosition { get; }
    }
}
