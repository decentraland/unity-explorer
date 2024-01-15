using UnityEngine;
using Utility;

namespace DCL.Character
{
    /// <summary>
    ///     Allows to access the player transform from the scene world
    /// </summary>
    public interface IExposedPlayerTransform
    {
        CanBeDirty<Vector3> Position { get; }
        CanBeDirty<Quaternion> Rotation { get; }
    }
}
