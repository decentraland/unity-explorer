using UnityEngine;

namespace DCL.Landscape
{
    public interface ITerrain
    {
        bool Contains(Vector2Int parcel);

        float GetHeight(float x, float z);
    }
}
