using UnityEngine;

namespace Utility
{
    /// <summary>
    ///     Allows to access the player transform from the scene world
    /// </summary>
    public interface IExposedTransform
    {
        CanBeDirty<Vector3> Position { get; }
        CanBeDirty<Quaternion> Rotation { get; }
    }
}
