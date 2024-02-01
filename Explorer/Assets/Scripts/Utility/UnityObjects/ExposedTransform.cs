using UnityEngine;

namespace Utility
{
    /// <summary>
    ///     <inheritdoc cref="IExposedTransform" />
    /// </summary>
    public class ExposedTransform : IExposedTransform
    {
        public CanBeDirty<Vector3> Position;
        public CanBeDirty<Quaternion> Rotation;

        CanBeDirty<Vector3> IExposedTransform.Position => Position;
        CanBeDirty<Quaternion> IExposedTransform.Rotation => Rotation;
    }
}
