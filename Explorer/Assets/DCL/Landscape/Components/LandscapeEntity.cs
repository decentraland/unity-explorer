using UnityEngine;

namespace DCL.Landscape.Components
{
    public struct LandscapeEntity
    {
        public readonly Vector3 Position;

        public LandscapeEntity(Vector3 basePosition)
        {
            Position = basePosition;
        }
    }
}
