using UnityEngine;
using Utility;

namespace DCL.Character
{
    /// <summary>
    ///     <inheritdoc cref="IExposedPlayerTransform" />
    /// </summary>
    public class ExposedPlayerTransform : IExposedPlayerTransform
    {
        public CanBeDirty<Vector3> Position;
        public CanBeDirty<Quaternion> Rotation;

        CanBeDirty<Vector3> IExposedPlayerTransform.Position => Position;
        CanBeDirty<Quaternion> IExposedPlayerTransform.Rotation => Rotation;
    }
}
