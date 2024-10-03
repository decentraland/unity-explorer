using UnityEngine;

namespace DCL.Landscape
{
    public interface IContainParcel
    {
        bool Contains(Vector2Int parcel);
    }
}
