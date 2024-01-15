using UnityEngine;

namespace DCL.Character
{
    /// <summary>
    ///     <inheritdoc cref="IExposedPlayerTransform" />
    /// </summary>
    public class ExposedPlayerTransform : IExposedPlayerTransform
    {
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public bool IsDirty { get; set; }
    }
}
