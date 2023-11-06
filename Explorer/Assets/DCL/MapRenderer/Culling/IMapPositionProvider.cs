using UnityEngine;

namespace DCLServices.MapRenderer.Culling
{
    public interface IMapPositionProvider
    {
        Vector3 CurrentPosition { get; }
    }
}
